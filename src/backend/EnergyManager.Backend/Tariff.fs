module EnergyManager.Backend.Tariff

open System
open System.Collections.Generic
open EnergyManager.Backend.Utils
open Microsoft.FSharp.Core

type Tariff =
    | Low
    | High
    | Peak
    
type Fee =
    { Regular: decimal<ore/kWh>
      Reduced: decimal<ore/kWh>}

type Interval =
    { Hours: int list
      Tariff: Tariff }

type Period =
    { Start: DateOnly
      End: DateOnly
      Intervals: Interval list
      Tariffs: IDictionary<Tariff, decimal<ore/kWh>>
      Fee : Fee }
    
type HourlyFeeAndTariff =
    { Hour: DateTimeOffset
      Tariff: decimal<ore/kWh>
      Fee: Fee }
    
type Config =
    { TariffsAndFeePeriods: Period list }
    
module Data =
    let private periods = [
        { Start = DateOnly(2024, 10, 1)
          End = DateOnly(2025, 3, 31)
          Intervals =  [
            { Hours = [0..6]; Tariff = Low}
            { Hours = [6..16]; Tariff = High }
            { Hours = [17..21]; Tariff = Peak }
            { Hours = [21..24]; Tariff = High }
          ]
          Tariffs = dict [
              Low, 11.45m<ore/kWh>
              High, 34.34m<ore/kWh>
              Peak, 103.02m<ore/kWh>
          ]
          Fee =
              { Regular = 76.10m<ore/kWh>
                Reduced = 0.8m<ore/kWh> } }
    ]

    let private tariffsDict(tariffIntervals, tariffs: IDictionary<Tariff, decimal<ore/kWh>>) =
        tariffIntervals
        |> Seq.map (fun x -> seq { for i in (x.Hours |> Seq.take (x.Hours.Length - 1)) -> (i, tariffs[x.Tariff]) })
        |> Seq.collect (fun x -> x |> Seq.map (fun y -> y ))
        |> dict

    let private daysInPeriod (startDate : DateOnly) (endDate: DateOnly) =
        let startDateTime = startDate.ToDateTime(TimeOnly(0))
        let endDateTime = endDate.ToDateTime(TimeOnly(0))
        let timespan = endDateTime - startDateTime
        timespan.Days
        
    let private dateOnlyWithOffset (days : int) (date : DateOnly) =
        let dateTime = date.ToDateTime(TimeOnly(0))
        let offsetDateTime = dateTime.AddDays(days)
        DateOnly.FromDateTime(offsetDateTime)

    let private dateTimeOffset (date : DateOnly) (hour : int) =
        let timeZone = TimeZoneInfo.Local
        let offset = timeZone.GetUtcOffset(date.ToDateTime(TimeOnly(hour, 0)))
        DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, offset)

    let private hoursWithTariffsAndFee (periods: Period list) =
        periods
        |> Seq.map (fun x -> seq { for i in 0..(daysInPeriod x.Start x.End) -> ((x.Start |> dateOnlyWithOffset i), x.Tariffs, x.Intervals, x.Fee) })
        |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
        |> Seq.map (fun (day, tariffs, intervals, fee) -> (day, tariffsDict (intervals, tariffs), fee))
        |> Seq.map (fun (day, intervals, fee) -> seq { for (hour, tariff) in (intervals |> Seq.map (fun x -> (x.Key, x.Value))) -> (day, hour, tariff, fee) })
        |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
        |> Seq.map (fun (day, hour, tariff, fee) -> { Hour = dateTimeOffset day hour; Tariff = tariff; Fee = fee })
    
    let getTariffs (config : Config) = //( periods : Period list) =
        hoursWithTariffsAndFee config.TariffsAndFeePeriods
