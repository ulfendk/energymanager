module EnergyManager.Backend.Background

open System
open System.Threading
open EnergyManager.Backend.Carnot
open EnergyManager.Backend.Database
open EnergyManager.Backend.Model
open EnergyManager.Backend.Utils
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PricesBackgroundWorker(repo : IDataRepository, eds : EnergiDataService.Config, carnotSource : CarnotSource, logger : ILogger<PricesBackgroundWorker>) =
    let getCarnot() = carnotSource.GetLatest()
    let getEds() = EnergiDataService.Data.getLatest eds
    // let getTariffs() = Tariff.Data.getTariffs tariffs
    
    let mutable timer : Timer option = None
    
    let toNode (ts : UnixDateTime) =
        { From = ts
          To = ts }
    let DoWork(state : obj) =
        logger.LogInformation("Doing work...")
        let tariffs = Tariff.Data2.getTariffTree
        // let now = DateTimeOffset.Now
        // // let key = Map.tryFindKey (fun key value -> ((key :> IComparable<UnixDateTime>).CompareTo (toUnixDateTime now)) = 0) qqq
        // let now' = toNode (toUnixDateTime now)
        // let node = Map.tryFind now' qqq
        // let found =
        //     match node with
        //     | None -> 0L
        //     | Some x -> (getSecondsFromTimestamp x.Interval.From)
        // printf $"%A{found}"
        // let tariff = Tariff.Data2.getTariffForHour (Utils.toUnixDateTime DateTimeOffset.Now) qqq
//         let hours = Utils.getHoursInInterval (DateOnly(2024, 10, 26)) (DateOnly(2024, 10, 28)) |> Seq.map (fun x -> Utils.toDateTimeOffset x)
//         // let localDateTime = Utils.toLocalDateTime (DateOnly(2024, 10, 1))
//         // let localUnixTime = Utils.toUnixDateTime localDateTime
//         // printf $"%A{localDateTime}\n" |> ignore
//         // let xxx = Tariff.tree
//         printf $"%A{hours}\n" |> ignore
        let eds = getEds()
//         let carnot = getCarnot()
//         let pricePoints = SpotPrice.mergePricePoints eds carnot
//         // let tariffs = getTariffs()
        let spotPrices = eds |> Seq.map(fun x -> SpotPrice.mergePriceAndTariff x (Map.find (toNode x.Timestamp) tariffs))
        spotPrices |> repo.InsertOrUpdatePrices |> ignore 
//         // spotPrices |> Seq.iter (fun x -> printf $"%A{x}") |> ignore
//         
// //        repo.InsertPrice(spotPrice) |> ignore
//         let prices = repo.GetPrices 
//         printf $"%A{prices}" |> ignore
    
    interface IHostedService with
        member this.StartAsync cancellationToken =
            task {
                timer <- Some(new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(30)))
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