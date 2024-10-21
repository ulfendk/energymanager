module EnergyManager.Backend.SpotPrice

open System
open EnergyManager.Backend.Model
open EnergyManager.Backend.Tariff
open EnergyManager.Backend.Utils

type SpotPrice =
    { Hour : DateTimeOffset
      BasePriceVat : decimal<dkk/kWh>
      AllFeesAndVat: decimal<dkk/kWh>
      ReducedFeesAndVat: decimal<dkk/kWh>
      FullPriceReducedFeeVat: decimal<dkk/kWh>
      FullPriceVat : decimal<dkk/kWh> }

let private getLocalHour (timestamp : DateTimeOffset) =
    let localTimeStamp = timestamp.ToLocalTime()
    DateTimeOffset(localTimeStamp.Year, localTimeStamp.Month, localTimeStamp.Day, localTimeStamp.Hour, 0, 0, localTimeStamp.Offset)

let getCurrentTariff (timestamp : DateTimeOffset) (tariffs : HourlyFeeAndTariff seq) =
    let localHour = getLocalHour timestamp
    tariffs |> Seq.find (fun x-> x.Hour = localHour)
    
let getCurrentPrice (timestamp : DateTimeOffset) (prices : PricePoint seq) =
    let localHour = getLocalHour timestamp
    prices |> Seq.find (fun x -> x.Timestamp = localHour)
    
let getPriceAndTariff (timestamp : DateTimeOffset) (prices : PricePoint seq) (tariffs : HourlyFeeAndTariff seq) =
    let getFee fee isReduced =
        match isReduced with
        | true -> fee.Reduced
        | false -> fee.Regular
    let price = prices |> getCurrentPrice timestamp
    let tariff = tariffs |> getCurrentTariff timestamp
    let basePrice = (price.Price / 100m<ore/dkk>) * 1.25m 
    let fullPriceVat = ((price.Price + tariff.Tariff + (getFee tariff.Fee false))/100m<ore/dkk>) * 1.25m
    let fullPriceReducedVat = ((price.Price + tariff.Tariff + (getFee tariff.Fee true))/100m<ore/dkk>) * 1.25m
    { Hour = price.Timestamp
      BasePriceVat = basePrice
      ReducedFeesAndVat =  fullPriceReducedVat - basePrice
      AllFeesAndVat = fullPriceVat - basePrice
      FullPriceReducedFeeVat = fullPriceReducedVat
      FullPriceVat = fullPriceVat}