module EnergyManager.Backend.Carnot

open EnergyManager.Backend.Utils
open FSharp.Data
open EnergyManager.Backend.Model
open Microsoft.Extensions.Logging

type Config =
    { Username : string option
      ApiKey : string option
      Region : string }

module Data =
    [<Literal>]
    let private carnotSample = "DataFiles/carnot.json"
    type private CarnotProvider = JsonProvider<carnotSample>

    let private carnotUrl (region : string) =
        $"https://whale-app-dquqw.ondigitalocean.app/openapi/get_predict?energysource=spotprice&region=%s{region}&daysahead=7"

    let private carnotJson (region: string, username : string, apiKey : string) =
        let json = Http.RequestString(carnotUrl region,[], [ ("accept", "application/json"); ("apikey", apiKey); ("username", username) ])
        json
    let private data (region: string, username : string, apiKey : string) = CarnotProvider.Parse(carnotJson (region, username, apiKey))

    let getLatest( config : Config ) =
        match (config.Username, config.ApiKey) with
        | Some username, Some apiKey ->
            let predictions = data (config.Region, username, apiKey) 
            predictions.Predictions |> Seq.map(fun x ->
            { Timestamp  = x.Dktime |> Utils.toLocalDateTimeOffset |> UnixDateTime.FromDateTime
              Region = x.Pricearea
              Price = (x.Prediction / 10m)
              IsActual = false
              LastUpdated = UnixDateTime.Now() })
            |> Seq.toList
        | _ -> List.empty<PricePoint>

type CarnotSource(config : Config, logger : ILogger<CarnotSource>) =
    member this.GetLatest() =
        logger.LogInformation("Attempting to download the latest data from Carnot.dk")
        let data = Data.getLatest config
        match data with
        | [] -> logger.LogInformation("Could not download data from Carnot.dk")
        | _ -> logger.LogInformation("Downloaded the latest data from Carnot.dk")
        data
