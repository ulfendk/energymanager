module EnergyManager.Backend.Tariff

open System
open System.Collections.Generic
open System.Globalization
open EnergyManager.Backend.Model
open EnergyManager.Backend.Utils
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core
open FSharp.Data

// type Calendar =
//     | Empty
//     | Node of PointInTime
// and PointInTime = { Time : UnixDateTime; Lower : Calendar; Higher : Calendar }
//
//
// let period =
//     Node({Time = Utc({ seconds = 1L }); Lower = Empty; Higher = Empty;}) 


module Config =
    type Tariff =
        | Low
        | High
        | Peak
    //     

    type Interval =
        { Interval: int * int
          Tariff: Tariff }

    type Period =
        { Start: DateOnly
          End: DateOnly
          Intervals: Interval seq
          Tariffs: IDictionary<Tariff, decimal>
          Fee : Fee }

    
module Data2 =
    [<Literal>]
    let private tariffSample = "DataFiles/tariffconfig.json"
    let private tariffConfigFile = "/config/ha/energy_manager/tariffconfig.json"
    type private TariffProvider = JsonProvider<tariffSample>
    let private configJson = System.IO.File.ReadAllText(tariffConfigFile);
    // let private configJson = System.IO.File.ReadAllText(tariffSample) //tariffConfigFile);
    let private data = TariffProvider.Parse(configJson)

    open Config
    
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
    
    // type TariffCalendar =
    //     | Empty
    //     | Node of value: NodeFeeAndTariff * before: TariffCalendar * after: TariffCalendar

    // module Tree =
    //     let hd = function
    //         | Empty -> failwith "empty"
    //         | Node(hd, l, r) -> hd
    //     
    //     let rec exists value = function
    //         | Empty -> false
    //         | Node(hd, l, r) ->
    //             if hd.Interval.IsIncluded(value) then true
    //             elif hd.Interval.Compare value = Before then exists value l
    //             else exists value r
    //             
    //     let rec insert value = function
    //         | Empty -> Node(value, Empty, Empty)
    //         | Node(hd, l, r) as node ->
    //             let comparison = hd.Interval.Compare(value.Interval) 
    //             if comparison = Included then node
    //             elif comparison = Before then Node(hd, insert value l, r)
    //             else Node(hd, l, insert value r)
    //
    // let rec insert newValue (tree : TariffCalendar) =
    //   match tree with
    //   | Empty -> Node (newValue, Empty, Empty)
    //   | Node (value, before, after) ->
    //       let comparison = value.Interval.Compare newValue.Interval
    //       let isBefore = (getSecondsFromTimestamp value.Interval.From) > (getSecondsFromTimestamp newValue.Interval.To)
    //       let isAfter = (getSecondsFromTimestamp value.Interval.To) < (getSecondsFromTimestamp newValue.Interval.From)
    //       match comparison with
    //       | After ->
    //           let before' = insert newValue before
    //           Node (value, before', after)
    //       | Before ->
    //           let after' = insert newValue after
    //           Node (value, before, after')
    //       | Included -> tree
    //   // | Node (value, before, after) when value.Interval.Compare newValue.Interval = After ->
    //   //       let before' = insert newValue before
    //   //       Node (value, before', after)
    //   // | Node (value, before, right) when value.Interval.Compare newValue.Interval = Before ->
    //   //       let after' = insert newValue right
    //   //       Node (value, before, after')
      // | _ -> tree

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
            // |> Seq.toArray
            // |> Array.randomShuffle
            |> Seq.map (fun x -> (x.Interval, x))
        // let startNode =
        //     match nodes.Length with
        //     | 0 -> Empty
        //     | _ -> Node(nodes[nodes.Length/2], Empty, Empty)
        // nodes |> Seq.fold (fun acc x -> Tree.insert x acc ) Empty
        Map(nodes)
       
    // let rec getTariffForHour (hour : UnixDateTime) (tree : TariffCalendar) =
    //     match tree with
    //     | Empty -> None
    //     | Node(value, before, after) ->
    //         let comparison = value.Interval.Compare (hour)
    //         match comparison with
    //         | Included -> Some value
    //         | Before -> getTariffForHour hour before
    //         | After -> getTariffForHour hour after
    
// type HourlyFeeAndTariff =
//     { Hour: DateTimeOffset
//       Tariff: decimal<ore/kWh>
//       Fee: Fee }
//
//    
// type Config =
//     { TariffsAndFeePeriods: Period list }


// let tree =
//     Empty
//     |> insert
//            { Interval =
//                  { From = Utc({seconds = 10 })
//                    To = Utc({seconds = 30})} 
//              Fee =
//                  { Regular = 0m<ore/kWh>
//                    Reduced = 0m<ore/kWh> }
//              Tariff = 0m}
    
// module Data =
//     let private periods = [
//         { Start = DateOnly(2024, 10, 1)
//           End = DateOnly(2025, 3, 31)
//           Intervals =  [
//             { Interval =  (0, 6); Tariff = Low}
//             { Interval = (6, 16); Tariff = High }
//             { Interval = (17, 20); Tariff = Peak }
//             { Interval = (21, 24); Tariff = High }
//           ]
//           Tariffs = dict [
//               Low, 11.45m<ore/kWh>
//               High, 34.34m<ore/kWh>
//               Peak, 103.02m<ore/kWh>
//           ]
//           Fee =
//               { Regular = 76.10m<ore/kWh>
//                 Reduced = 0.8m<ore/kWh> } }
//     ]
//
//     let private tariffsDict(tariffIntervals, tariffs: IDictionary<Tariff, decimal<ore/kWh>>) =
//         tariffIntervals
//         |> Seq.map (fun x -> seq { for i in (x.Hours |> Seq.take (x.Hours.Length - 1)) -> (i, tariffs[x.Tariff]) })
//         |> Seq.collect (fun x -> x |> Seq.map (fun y -> y ))
//         |> dict
//
//     // let private hoursInPeriod (startDate : DateOnly) (endDate: DateOnly) =
//     //     let startDateTime = LocalDateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0)
//     //     let endDateTime = LocalDateTime(endDate.Year, endDate.Month, endDate.Day, 0, 0)
//     //     // let startInstant = Instant.From
//     //     // let period = endDateTime.Minus(startDateTime)
//     //     let cph = DateTimeZoneProviders.Tzdb["Europe/Copenhagen"]
//     //     let startAt = cph.MapLocal(startDateTime).EarlyInterval.Start
//     //     let endAt = cph.MapLocal(endDateTime).LateInterval.End
//     //     
//     //     let p = endAt - startAt //NodaTime.Period(startAt, endAt)
//     //     
//     //     let hours = seq { for i in 0..(int p.TotalHours) -> startDateTime.Plus(Period.FromHours(i)).InZoneLeniently(cph) }
//     //     
//     //     hours            
//         
//         // duration
//         
//     
//     let private daysInPeriod (startDate : DateOnly) (endDate: DateOnly) =
//         let startDateTime = startDate.ToDateTime(TimeOnly(0))
//         let endDateTime = endDate.ToDateTime(TimeOnly(0))
//         let timespan = endDateTime - startDateTime
//         timespan.Days
//         
//     let private dateOnlyWithOffset (days : int) (date : DateOnly) =
//         let dateTime = date.ToDateTime(TimeOnly(0))
//         let offsetDateTime = dateTime.AddDays(days)
//         DateOnly.FromDateTime(offsetDateTime)
//
//     let private dateTimeOffsetFromStart (hours : int) (date: DateOnly) =
//         let tz = TimeZoneInfo.Local
//         let dateTime = date.ToDateTime(TimeOnly(0), DateTimeKind.Local)
//         let hour = dateTime.AddHours(hours)
//         DateTimeOffset(hour, tz.GetUtcOffset(hour))
//         
//         
//         //DateOnly.FromDateTime(offsetDateTime)
//         
//     
//     let private dateTimeOffset (date : DateOnly) (hour : int) =
//         let timeZone = TimeZoneInfo.Local
//         let adjustments = timeZone.GetAdjustmentRules()
//         let dstStart = adjustments |> Seq.tryFind (fun x -> x.DateStart.Year = date.Year && x.DaylightTransitionStart.Month = date.Month && x.DaylightTransitionStart.Day = date.Day)
//         let dstEnd = adjustments |> Seq.tryFind (fun x -> x.DateEnd.Year = date.Year && x.DaylightTransitionEnd.Month = date.Month && x.DaylightTransitionEnd.Day = date.Day)
//         
//         let hoursInDay =
//             match (dstStart, dstEnd) with
//             | Some startOf, None -> (startOf.DaylightTransitionStart.TimeOfDay, startOf.BaseUtcOffsetDelta.Hours)
//             | None, Some endOf -> (endOf.DaylightTransitionEnd.TimeOfDay, -endOf.BaseUtcOffsetDelta.Hours)
//             | _ -> (DateTime.MinValue, 0)
//
//         let offset = timeZone.GetUtcOffset(date.ToDateTime(TimeOnly(hour, 0)))
//         DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, offset)
//
//     let private hoursWithTariffsAndFee (periods: Period list) =
//         periods
//         |> Seq.map (fun x -> seq { for i in 0..(daysInPeriod x.Start x.End) -> ((x.Start |> dateOnlyWithOffset i), x.Tariffs, x.Intervals, x.Fee) })
//         |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
//         |> Seq.map (fun (day, tariffs, intervals, fee) -> (day, tariffsDict (intervals, tariffs), fee))
//         |> Seq.map (fun (day, intervals, fee) -> seq { for (hour, tariff) in (intervals |> Seq.map (fun x -> (x.Key, x.Value))) -> (day, hour, tariff, fee) })
//         |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
//         |> Seq.map (fun (day, hour, tariff, fee) -> { Hour = dateTimeOffset day hour; Tariff = tariff; Fee = fee })
//
//     // let private hoursWithTariffsAndFeeUtcFix (periods: Period list) =
//     //     periods
//     //     |> Seq.map (fun x -> seq { for i in 0..(hoursInPeriod x.Start x.End) -> ((x.Start |> dateTimeOffsetFromStart i), x.Tariffs, x.Intervals, x.Fee) })
//     //     |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
//     //     |> Seq.map (fun (day, tariffs, intervals, fee) -> (day, tariffsDict (intervals, tariffs), fee))
//     //     |> Seq.map (fun (day, intervals, fee) -> seq { for (hour, tariff) in (intervals |> Seq.map (fun x -> (x.Key, x.Value))) -> (day, hour, tariff, fee) })
//     //     |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
//     //     |> Seq.map (fun (day, hour, tariff, fee) -> { Hour = dateTimeOffset day hour; Tariff = tariff; Fee = fee })
//     
//     // let getTariffs (config : Config) =
//     //     let hours = config.TariffsAndFeePeriods
//     //                 |> Seq.map (fun x -> hoursInPeriod x.Start x.End)// -> (x.Start, i)})
//     //                 |> Seq.collect (fun x -> x |> Seq.map (fun y -> y))
//     //                 |> Seq.map (fun x -> x.ToString())
//     //                 |> Seq.toArray
//     //                 // |> Seq.map (fun (start, hour) -> dateTimeOffsetFromStart hour start)
//     //     hoursWithTariffsAndFee config.TariffsAndFeePeriods
