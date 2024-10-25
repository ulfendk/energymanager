module EnergyManager.Backend.SpotPrice

open System
open System.Collections.Generic
open EnergyManager.Backend.Carnot
open EnergyManager.Backend.Model
open EnergyManager.Backend.Tariff
open EnergyManager.Backend.Utils
open FSharp.Data
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core

type SpotPriceLevel =
    | Free
    | Cheap
    | Normal
    | Expensive
    | Extreme
    | Unknown

module Data =
    [<Literal>]
    let private spotPriceLevelsSample = "DataFiles/spotpricelevels.json"
    type SpotPriceLevelsProvider = JsonProvider<spotPriceLevelsSample>

type Config = { SpotPriceLevelsFilePath : string }

type SpotPrices(config : Config) =
    let configJson = System.IO.File.ReadAllText(config.SpotPriceLevelsFilePath);
    let data = Data.SpotPriceLevelsProvider.Parse(configJson)

    let validLevels = [ Free; Cheap; Normal; Expensive; Extreme; ] 

    let spotPriceLevels =
        validLevels
        |> Seq.zip [ data.Free; data.Cheap; data.Normal; data.Expensive; data.Extreme ]
        |> Seq.toList

    let mergePricePoints (primary : PricePoint seq) (secondary : PricePoint seq) =
        let prices = Dictionary<int64, PricePoint>()
        primary |> Seq.iter (fun x -> prices.Add(x.Timestamp.Seconds, x))
        secondary |> Seq.iter (
            fun x ->
                let isNew = prices.TryAdd(x.Timestamp.Seconds, x) 
                if not isNew  then
                    let existing = prices[x.Timestamp.Seconds]
                    if not existing.IsActual then
                        prices[x.Timestamp.Seconds] <- x
            )
        prices.Values
        |> Seq.toArray

    let getLocalHour (timestamp : DateTimeOffset) =
        let localTimeStamp = timestamp.ToLocalTime()
        DateTimeOffset(localTimeStamp.Year, localTimeStamp.Month, localTimeStamp.Day, localTimeStamp.Hour, 0, 0, localTimeStamp.Offset)
        |> UnixDateTime.FromDateTime
        
    let getCurrentPrice (timestamp : DateTimeOffset) (prices : PricePoint seq) =
        let localHour = getLocalHour timestamp
        prices |> Seq.tryFind (fun x -> x.Timestamp = localHour)

    let mergePriceAndTariff (price : PricePoint) (tariff : IntervalFeeAndTariff) =
        let getFee fee isReduced =
            match isReduced with
            | true -> fee.Reduced
            | false -> fee.Regular
        let basePriceVat = price.Price * 1.25m 
        let fullPriceVat = (price.Price + tariff.Tariff + (getFee tariff.Fee false)) * 1.25m
        let fullPriceReducedVat = (price.Price + tariff.Tariff + (getFee tariff.Fee true)) * 1.25m
        { Timestamp = price.Timestamp
          BasePriceVat = basePriceVat
          ReducedFeesAndVat =  fullPriceReducedVat - basePriceVat
          AllFeesAndVat = fullPriceVat - basePriceVat
          FullPriceReducedFeeVat = fullPriceReducedVat
          FullPriceVat = fullPriceVat
          IsPrediction = not price.IsActual
          IsComplete = true
          LastUpdated = price.LastUpdated }
        
    let getSpotPrice (prices: PricePoint seq) (tariffs : Map<UnixDateTimeInterval, IntervalFeeAndTariff>) (time : DateTimeOffset) =
        let toNode (ts : UnixDateTime) =
            { From = ts
              To = ts }
        let localHour = getLocalHour time
        let key = toNode localHour 
        let tariff = Map.tryFind key tariffs
        let price = getCurrentPrice time prices
        match (price, tariff) with
        | (Some p, Some t) -> mergePriceAndTariff p t
        | (Some p, None) ->
            let basePrice = p.Price * 1.25m 
            { Timestamp = localHour
              BasePriceVat =  basePrice
              ReducedFeesAndVat = basePrice
              AllFeesAndVat = basePrice
              FullPriceReducedFeeVat = basePrice
              FullPriceVat = basePrice
              IsPrediction = not p.IsActual
              IsComplete = false
              LastUpdated = UnixDateTime.Now() }
        | _ ->
            { Timestamp = localHour
              BasePriceVat =  0m
              ReducedFeesAndVat = 0m
              AllFeesAndVat = 0m
              FullPriceReducedFeeVat = 0m
              FullPriceVat = 0m
              IsPrediction = false
              IsComplete = false
              LastUpdated = UnixDateTime.Now() }

    member this.MergePriceAndTariff (price : PricePoint) (tariff : IntervalFeeAndTariff) =
        mergePriceAndTariff (price) (tariff)
    
    member this.ValidLevels = validLevels
    member this.SpotPriceLevels = spotPriceLevels
   