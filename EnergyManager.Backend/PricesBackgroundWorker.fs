module EnergyManager.Backend.Background

open System
open System.Threading
open EnergyManager.Backend.Carnot
open EnergyManager.Backend.Database
open EnergyManager.Backend.Model
open EnergyManager.Backend.SpotPrice
open EnergyManager.Backend.Tariff
open EnergyManager.Backend.Utils
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PricesBackgroundWorker(repo : IDataRepository, eds : EnergiDataService.Config, carnotSource : CarnotSource, tariffs : TariffConfig, spotPrices : SpotPrices, logger : ILogger<PricesBackgroundWorker>) =
    let getCarnot() = carnotSource.GetLatest()
    let getEds() = EnergiDataService.Data.getLatest eds
    
    let mutable timer : Timer option = None
    
    let toNode (ts : UnixDateTime) =
        { From = ts
          To = ts }
    let DoWork(state : obj) =
        logger.LogInformation("Updating data in the background...")
        let eds = getEds()

        let spotPrices = eds |> Seq.map(fun x -> spotPrices.MergePriceAndTariff x (Map.find (toNode x.Timestamp) tariffs.Configured))
        spotPrices |> repo.InsertOrUpdatePrices |> ignore 
        logger.LogInformation("DONE updating data in the background.")
    
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