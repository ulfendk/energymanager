module EnergyManager.Backend.HomeAssistantBackgroundWorker

open System
open System.Text.Json
open System.Threading
open EnergyManager.Backend.Database
open EnergyManager.Backend.HomeAssistant
open EnergyManager.Backend.Model
open EnergyManager.Backend.SpotPrice
open Giraffe
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type HomeAssistantBackgroundWorker(api : HomeAssistantApi, repo : IDataRepository, logger : ILogger<HomeAssistantBackgroundWorker>) =
    let mutable timer : Timer option = None

    let setPrice (sensorName : string, friendlyName : string, price : decimal, date: UnixDateTime) =
        let spotPricePayload =
            ValueSensor {
                State = $"%M{price}"
                LastUpdated = Some (date.ToDateTimeOffset())
                Attributes = Some
                    { FriendlyName = Some friendlyName
                      Icon = Some "mdi:cash"
                      DeviceClass = Some "monetary"
                      UnitOfMeasurement = Some "DKK/kWh"
                      ExtraValues = None } }
        api.SetEntity(sensorName, spotPricePayload) |> ignore
    
    let DoWork(state : obj) =
        logger.LogInformation("Updating Home Assistant entities")

        let prices = repo.GetPrices(UnixDateTime.Now(), 7 * 24) |> Seq.map (_.ToDkk()) |> Seq.toList
        let price = prices |> List.head
        let futurePrices =
            prices
            |> List.skip 1
            |> List.map (fun x -> (x.Timestamp.ToDateTimeOffset().ToIsoString(), x.FullPriceVat))
        let spotPricePayload =
            ValueSensor {
                State = $"%M{price.FullPriceVat}"
                LastUpdated = Some (price.LastUpdated.ToDateTimeOffset())
                Attributes = Some
                    { FriendlyName = Some "Spot Price"
                      Icon = Some "mdi:cash"
                      DeviceClass = Some "monetary"
                      UnitOfMeasurement = Some "DKK/kWh"
                      ExtraValues = Some (JsonSerializer.Serialize(futurePrices)) }}
        api.SetEntity("spot_price", spotPricePayload) |> ignore

        setPrice("spot_price_reduced", "Spot Price Reduced", price.FullPriceReducedFeeVat, price.LastUpdated)
        setPrice("spot_price_base_vat", "Spot Price Base with VAT", price.BasePriceVat, price.LastUpdated)
        setPrice("spot_price_fees_vat", "Spot Price Fees with VAT", price.AllFeesAndVat, price.LastUpdated)
        setPrice("spot_price_fees_reduced_vat", "Spot Price Reduced Fees with VAT", price.ReducedFeesAndVat, price.LastUpdated)

        let find (value: decimal) (lst : (decimal * SpotPriceLevel) list ) =
            let rec findRec (value: decimal) (acc : decimal * SpotPriceLevel) (lst : (decimal * SpotPriceLevel) list ) =
                let (accK, accV) = acc
                match lst with
                | head :: tail ->
                    let (k, v) = head
                    if value >= k then
                        findRec value head tail
                    else
                        accV
                | [] -> accV
                    
            findRec value (0m, Unknown) lst
                        
        let level = SpotPrice.spotPriceLevels |> find price.FullPriceVat
        let levelReduced = SpotPrice.spotPriceLevels |> find price.FullPriceReducedFeeVat

        let icon(level) =
            match level with
            | Free -> "mdi:gauge-empty"
            | Cheap -> "mdi:gauge-low"
            | Normal -> "mdi:gauge"
            | Expensive -> "mdi:gauge-full"
            | Extreme -> "mdi:alert-decagram"
            | _ -> "mdi:help"
            
        let levelPayload(level, label) =
            EnumSensor {
                State = $"%A{level}"
                LastUpdated =  Some (price.LastUpdated.ToDateTimeOffset())
                Attributes = Some
                    { FriendlyName = Some $"Spot Price Level%s{label}"
                      Icon = Some (icon level)
                      DeviceClass = Some "enum"
                      Options = Some (SpotPrice.levels |> List.map (fun x -> $"%A{x}")) } }

        api.SetEntity("spot_price_level", levelPayload(level, "")) |> ignore
        api.SetEntity("spot_price_reduced_level", levelPayload(levelReduced," Reduced")) |> ignore
        
        let cheapest = prices |> Seq.truncate 24 |> Seq.sortBy (_.FullPriceVat) |> Seq.head
        let cheapestNextDayPayload =
            TextSensor {
                State = cheapest.Timestamp.ToDateTimeOffset().ToIsoString()
                LastUpdated =  Some (price.LastUpdated.ToDateTimeOffset())
                Attributes = Some
                    { FriendlyName = Some "Cheapest Hour (24h)"
                      Icon = Some "mdi:clock-in"
                      DeviceClass = Some "timestamp"
                      UnitOfMeasurement = None
                      ExtraValues = None } }
        api.SetEntity("cheapest_24h", cheapestNextDayPayload) |> ignore

        let cheapestNextDayPricePayload =
            TextSensor {
                State = $"%M{cheapest.FullPriceVat}"
                LastUpdated =  Some (price.LastUpdated.ToDateTimeOffset())
                Attributes = Some
                    { FriendlyName = Some "Price at Cheapest Hour (24h)"
                      Icon = Some "mdi:cash"
                      DeviceClass = Some "monetary"
                      UnitOfMeasurement = Some "DKK/kWh"
                      ExtraValues = None } }
        api.SetEntity("cheapest_price_24h", cheapestNextDayPricePayload) |> ignore
        
        logger.LogInformation("Done updating Home Assistant entities.")
    
    interface IHostedService with
        member this.StartAsync cancellationToken =
            task {
                timer <- Some(new Timer(DoWork, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1)))
            }
        member this.StopAsync cancellationToken =
            task {
                timer <-
                    match timer with
                    | None -> None
                    | Some(timer) ->
                        timer.Change(Timeout.Infinite, 0) |> ignore
                        None
            }
        
    interface IDisposable with
        member this.Dispose() =
            match timer with
            | None -> ()
            | Some(timer) -> timer.Dispose()