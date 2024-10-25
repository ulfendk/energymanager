module EnergyManager.Backend.Tariff

open System
open System.Collections.Generic
open Microsoft.Extensions.Logging
open EnergyManager.Backend.Model
open EnergyManager.Backend.Utils
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core
open FSharp.Data

type Tariff =
    | Low
    | High
    | Peak

type Interval =
    { Interval: int * int
      Tariff: Tariff }

type Period =
    { Start: DateOnly
      End: DateOnly
      Intervals: Interval seq
      Tariffs: IDictionary<Tariff, decimal>
      Fee : Fee }
    
type Config = { ConfigFile : string }

module Data =
    [<Literal>]
    let tariffSample = "DataFiles/tariffconfig.json"
    
    type TariffProvider = JsonProvider<tariffSample>

type TariffConfig(config : Config, logger : ILogger<TariffConfig>) =
    let configJson = System.IO.File.ReadAllText(config.ConfigFile)
    let data = Data.TariffProvider.Parse(configJson)

    let config =
        data
        |> Seq.map (fun x ->
            { Start = DateOnly.FromDateTime(x.Start)
              End = DateOnly.FromDateTime(x.End)
              Intervals =
                  x.Intervals
                  |> Seq.map (fun y ->
                      { Interval  =  (y.From, y.To)
                        Tariff =
                            match y.Tariff.ToLowerInvariant() with
                            | "peak" -> Peak
                            | "high" -> High
                            | "low" -> Low
                            | _ -> failwith "Invalid tariff naming - valid values are: Peak, High and Low" })
              Tariffs = (dict [
                  (Peak, x.Tariffs.Peak)
                  (High, x.Tariffs.High)
                  (Low, x.Tariffs.Low) ])
              Fee =
                  { Regular = x.Fee.Regular
                    Reduced = x.Fee.Reduced } })
        |> Seq.toArray
    
    let getTariffTree =
        let nodes = 
            config
            |> Seq.map (fun x ->
                getDaysInInterval x.Start x.End
                |> Seq.map (fun y ->
                    x.Intervals
                    |> Seq.map (fun z ->
                        let (startHour, endHour) = z.Interval
                        { Interval =
                            { From = toLocalUnixTimeFromDateAndHour y startHour false
                              To =  toLocalUnixTimeFromDateAndHour y endHour true }
                          Tariff = x.Tariffs[z.Tariff]
                          Fee = x.Fee })))
            |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
            |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
            |> Seq.map (fun x -> (x.Interval, x))
        Map(nodes)
        
    member this.Configured = getTariffTree