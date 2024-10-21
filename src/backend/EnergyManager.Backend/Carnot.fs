module EnergyManager.Backend.Carnot

open EnergyManager.Backend.Utils
open FSharp.Data
open EnergyManager.Backend.Model
open Microsoft.Extensions.Logging

type Config =
    { Username : string
      ApiKey : string
      Region : string }

module Data =
    [<Literal>]
    let private carnotSample = "DataFiles/carnot.json"
    type private CarnotProvider = JsonProvider<carnotSample>

    let private carnotUrl (region : string) =
        $"https://whale-app-dquqw.ondigitalocean.app/openapi/get_predict?energysource=spotprice&region=%s{region}&daysahead=7"

    let private carnotJson (config : Config) = Http.RequestString(carnotUrl config.Region,[], [ ("accept", "application/json"); ("apikey", config.ApiKey); ("username", config.Username) ])
    let private data (config : Config) = CarnotProvider.Parse(carnotJson config)

    let getLatest( config : Config ) =
        let predictions = data config
        predictions.Predictions |> Seq.map(fun x ->
        { Timestamp  = x.Dktime |> Utils.toLocalDateTimeOffset
          Region = x.Pricearea
          Price = (x.Prediction / 1000m<kWh/ore>)
          IsActual = false }) |> Seq.toList

type CarnotSource(config : Config, logger : ILogger<CarnotSource>) =
    member this.GetLatest() =
        logger.LogInformation("Attempting to download the latest data from Carnot.dk")
        let data = Data.getLatest config
        logger.LogInformation("Downloaded the latest data from Carnot.dk")
        data
