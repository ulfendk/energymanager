module EnergyManager.Backend.EnergiDataService

open System
open EnergyManager.Backend.Model
open EnergyManager.Backend.Utils
open FSharp.Data
open Model

type Config =
    { Region : Region }

module Data =
    [<Literal>]
    let private energiSample = "DataFiles/energidataservice.json"
    type private EnergiDataService = JsonProvider<energiSample>
    
    let private energiUrl (timestamp : DateTimeOffset) (region: Region) =
        let dateString = timestamp.UtcDateTime.ToString("yyyy-MM-dd")
        $"https://api.energidataservice.dk/dataset/elspotprices?start=%s{dateString}&sort=HourUTC asc&filter={{\"PriceArea\":[\"%s{region.Value}\"]}}&limit=48"

    let private energiJson (config : Config) = Http.RequestString(energiUrl DateTimeOffset.Now config.Region)
    let private data (config : Config) = EnergiDataService.Parse(energiJson config)

    let getLatest(config : Config) =
        let actual = data config
        actual.Records |> Seq.map(fun x->
            { Timestamp  = DateTimeOffset(x.HourUtc.Year, x.HourUtc.Month, x.HourUtc.Day, x.HourUtc.Hour, x.HourUtc.Minute, x.HourUtc.Second, TimeSpan(0)) |>   UnixDateTime.FromDateTime
            // { Timestamp  = x.HourDk |> Utils.asLocalDateTimeOffset |> UnixDateTime.FromDateTime
              Region = config.Region.Value
              Price = x.SpotPriceDkk / 10m
              IsActual = true
              LastUpdated = UnixDateTime.Now() })
        |> Seq.toList
