module EnergyManager.Backend.Background

open System
open System.Threading
open EnergyManager.Backend.Carnot
open EnergyManager.Backend.Database
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type PricesBackgroundWorker(repo : IDataRepository, carnot : Carnot.Config, eds : EnergiDataService.Config, tariffs : Tariff.Config, carnotSource : CarnotSource, logger : ILogger<PricesBackgroundWorker>) =
    let getCarnot() = carnotSource.GetLatest()// Carnot.Data.getLatest carnot
    let getEds() = EnergiDataService.Data.getLatest eds
    let getTariffs() = Tariff.Data.getTariffs tariffs
    
    let mutable timer : Timer option = None
    
    let DoWork(state : obj) =
        //let carnot = getCarnot()
        let eds = getEds()
        let tariffs = getTariffs()
        let spotPrice = SpotPrice.getPriceAndTariff DateTimeOffset.Now eds tariffs
        printf $"%A{spotPrice}" |> ignore
        
        repo.InsertPrice(spotPrice) |> ignore
        let prices = repo.GetPrices 
        printf $"%A{prices}" |> ignore
    
    interface IHostedService with
        member this.StartAsync cancellationToken =
            task {
                timer <- Some(new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(5)))
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