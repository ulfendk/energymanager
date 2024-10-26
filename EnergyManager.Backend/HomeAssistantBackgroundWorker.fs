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

type FuturePrice =
    { Hour : string
      Price : Decimal
      IsActual : bool }

type HomeAssistantBackgroundWorker(api : HomeAssistantApi, repo : IDataRepository, spotPrices : SpotPrices, logger : ILogger<HomeAssistantBackgroundWorker>) =
    let mutable timer : Timer option = None

    // let setPrice (sensorName : string, friendlyName : string, price : decimal, date: UnixDateTime) =
    //     let spotPricePayload =
    //         ValueSensor {
    //             State = $"%M{price}"
    //             LastUpdated = Some (date.ToDateTimeOffset())
    //             Attributes = Some
    //                 { FriendlyName = Some friendlyName
    //                   Icon = Some "mdi:cash"
    //                   DeviceClass = Some "monetary"
    //                   UnitOfMeasurement = Some "DKK/kWh"
    //                   ExtraValues = None } }
    //     api.SetEntity(sensorName, spotPricePayload) |> ignore

    let setPrice2 (sensorName : string, friendlyName : string, selector : SpotPrice -> decimal, prices : SpotPrice list) =
        let price = prices |> List.head
        let futurePrices =
            prices
            |> List.map (fun x -> (x.Timestamp.ToDateTimeOffset().ToIsoString(), selector(x), x.IsPrediction))

        let spotPricePayload =
            ValueSensor {
                State = $"%M{selector(price)}"
                LastUpdated = Some (price.LastUpdated.ToDateTimeOffset())
                Attributes = Some
                    { FriendlyName = Some friendlyName
                      Icon = Some "mdi:cash"
                      DeviceClass = Some "monetary"
                      UnitOfMeasurement = Some "DKK/kWh"
                      ExtraValues = Some (futurePrices |> List.map (fun (h, p, b) -> { Hour = h; Price = p; IsActual = not b })) }}
        api.SetEntity(sensorName, spotPricePayload) |> ignore
    
    let DoWork(state : obj) =
        logger.LogInformation("Updating Home Assistant entities")

        // THIS NEEDS WORK - use MAP???
        let prices = repo.GetPrices(UnixDateTime.Now(), 24*7) |> Seq.map (_.ToDkk()) |> Seq.toList
        // let allDayprices = repo.GetPrices(UnixDateTime.Today(), 7 * 24) |> Seq.map (_.ToDkk()) |> Seq.toList
        // let price = prices |> List.head
        // let futurePrices =
        //     prices
        //     // |> List.skip 1
        //     |> List.map (fun x -> (x.Timestamp.ToDateTimeOffset().ToIsoString(), x.FullPriceVat, x.IsPrediction))
        // let spotPricePayload =
        //     ValueSensor {
        //         State = $"%M{price.FullPriceVat}"
        //         LastUpdated = Some (price.LastUpdated.ToDateTimeOffset())
        //         Attributes = Some
        //             { FriendlyName = Some "Spot Price"
        //               Icon = Some "mdi:cash"
        //               DeviceClass = Some "monetary"
        //               UnitOfMeasurement = Some "DKK/kWh"
        //               ExtraValues = Some (futurePrices |> List.map (fun (h, p, b) -> { Hour = h; Price = p; IsActual = not b })) }}
        // api.SetEntity("spot_price", spotPricePayload) |> ignore

        setPrice2("spot_price", "Spot Price", (fun (x : SpotPrice) -> x.FullPriceVat), prices)
        setPrice2("spot_price_reduced", "Spot Price Reduced", (fun (x : SpotPrice) -> x.FullPriceReducedFeeVat), prices)
        setPrice2("spot_price_base_vat", "Spot Price Base with VAT", (fun (x : SpotPrice) -> x.BasePriceVat), prices)
        setPrice2("spot_price_fees_vat", "Spot Price Fees with VAT", (fun (x : SpotPrice) -> x.AllFeesAndVat), prices)
        setPrice2("spot_price_fees_reduced_vat", "Spot Price Reduced Fees with VAT", (fun (x : SpotPrice) -> x.ReducedFeesAndVat), prices)

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
                        
        let price = prices |> List.head
        let level = spotPrices.SpotPriceLevels |> find price.FullPriceVat
        let levelReduced = spotPrices.SpotPriceLevels |> find price.FullPriceReducedFeeVat

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
                      Options = Some (spotPrices.ValidLevels |> List.map (fun x -> $"%A{x}")) } }

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