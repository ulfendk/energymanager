module EnergyManager.Backend.Utils

open EnergyManager.Backend.Model
open System
open Microsoft.Extensions.Logging.Console
// open Microsoft.FSharp.Data.UnitSystems.SI.UnitNames

// [<Measure>] type dkk
// [<Measure>] type ore
// [<Measure>] type kWh

let getLocalOffset (timestamp : DateTime) =
    let timezone = TimeZoneInfo.Local
    timezone.GetUtcOffset(timestamp)
//
let asLocalDateTimeOffset (timestamp : DateTime) =
    let offset  = getLocalOffset timestamp
    DateTimeOffset(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, timestamp.Second, offset)    
//
// let withLocalOffset (timestamp : DateTimeOffset) =
//     asLocalDateTimeOffset timestamp.DateTime
//    
let toLocalDateTimeOffset (timestamp : DateTimeOffset) =
    let offset  = getLocalOffset timestamp.DateTime
    (asLocalDateTimeOffset timestamp.DateTime).Add(offset)
//     
//     
//
// let getSecondsFromTimestamp time =
//     match time with
//     | Utc({ seconds = ts }) -> ts
//     | LocalTime({ seconds = ts; offset = _ }) -> ts
//
// type IntervalComparison =
//     | Before
//     | Included
//     | After
//
//             // let from' = (this.From :> IComparable<UnixDateTime>).CompareTo other.From
//             // from'
//             // let to' = (this.To :> IComparable<UnixDateTime>).CompareTo other.To
//             // from' + to'
//
//     // interface IComparable<UnixDateTime> with
//     //     member this.CompareTo other =
//     //         let __from = (UnixDateTime.getSeconds this.From)
//     //         let __to = (UnixDateTime.getSeconds this.To)
//     //         let other' = (UnixDateTime.getSeconds other)
//     //         let diffFrom = other' - __from
//     //         let diffTo = other' - __to
//     //         let from' = (this.From :> IComparable<UnixDateTime>).CompareTo other
//     //         let to' = (this.To :> IComparable<UnixDateTime>).CompareTo other
//     //         from' + to'
//
//     // member x.IsIncluded (value : UnixDateTime) =
//     //     (UnixDateTime.GreaterThanOrEqual x.From value) && (UnixDateTime.LessThanOrEqual x.To value)
//     // member x.Compare (value : UnixDateTime) =
//     //     let isBefore = UnixDateTime.GreaterThan x.From value
//     //     let isAfter = UnixDateTime.LessThan x.To value
//     //     let isIncluded = x.IsIncluded value
//     //     match value with
//     //     | _ when (UnixDateTime.GreaterThan x.To value) -> Before
//     //     | _ when (UnixDateTime.LessThan x.From value) -> After
//     //     | _ -> Included
//     // member x.Compare (value : UnixDateTimeInterval) =
//     //     match value with
//     //     | _ when (getSecondsFromTimestamp x.From) >= (getSecondsFromTimestamp value.To) -> Before
//     //     | _ when (getSecondsFromTimestamp x.To) <= (getSecondsFromTimestamp value.From) -> After
//     //     | _ -> Included
//     // interface IComparable with
//     //     member this.CompareTo other =
//     //         match other with
//     //         | :? UnixDateTimeInterval i -> (this :>)
//
// // let seconds time = time * 1L<second>
// // let unixSeconds (dateTimeOffset : DateTimeOffset) =
// //     seconds (dateTimeOffset.ToUnixTimeSeconds())
// // let minutes time = time * 1L<minute>
// // let minutes (timeSpan : TimeSpan) =
// //     minutes (int64 timeSpan.TotalSeconds)
//
// // let toUnixDateTime (dateTimeOffset : DateTimeOffset) =
// //     match dateTimeOffset with
// //     | _ when dateTimeOffset.Offset = TimeSpan.Zero -> Utc({ seconds = dateTimeOffset.ToUnixTimeSeconds() })
// //     | _ -> LocalTime({ seconds = dateTimeOffset.ToUnixTimeSeconds(); offset = (int64 dateTimeOffset.Offset.TotalSeconds) })
//
// let toDateTimeOffset unixDateTime =
//     match unixDateTime with
//     | Utc(ts) -> DateTimeOffset.FromUnixTimeSeconds(ts.seconds)
//     | LocalTime(ts) -> DateTimeOffset.FromUnixTimeSeconds(ts.seconds).ToOffset(TimeSpan.FromSeconds(float ts.offset))
//
let toLocalDateTime (date: DateOnly) (hour: int) =
    let localTime = date.ToDateTime(TimeOnly(hour, 0), DateTimeKind.Local)
    DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime))
//     
let toLocalUnixTimeFromDateAndHour (date: DateOnly) (hour: int) (includeFullHour : bool) =
    let dateTimeOffset = toLocalDateTime date hour
    let correctedDateTimeOffset =
        match includeFullHour with
        | true -> dateTimeOffset.AddHours(1).AddSeconds(-1)
        | _ -> dateTimeOffset
    UnixDateTime.FromDateTime correctedDateTimeOffset

let toLocalDate (date : DateOnly) = toLocalDateTime date 0
    
let getHoursInInterval (startDate : DateOnly) (endDate : DateOnly) =
    let startUnixTime = UnixDateTime.FromDateTime (toLocalDate startDate)
    let endUnixTime = UnixDateTime.FromDateTime (toLocalDate endDate)
    let startTs = startUnixTime.Seconds
    let endTs = endUnixTime.Seconds
    seq {
        for hour in startTs..3600L..(endTs-1L) ->
            let localDateTime = DateTimeOffset.FromUnixTimeSeconds(hour).LocalDateTime 
            let offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime)
            LocalTime({ seconds = hour; offset = int64 offset.TotalSeconds }) }
        
let getDaysInInterval (startDate : DateOnly) (endDate : DateOnly) =
    let startDate = startDate.ToDateTime(TimeOnly(0))
    let diff = endDate.ToDateTime(TimeOnly(0)) - startDate
    let days = int diff.TotalDays
    seq { for i in 0..days ->
            let date = startDate.AddDays(i)
            DateOnly(date.Year, date.Month, date.Day) }
    |> Seq.toArray

type HassAddOnConsoleFormatterOptions() =
    inherit ConsoleFormatterOptions()

type HassAddOnConsoleFormatter(options : ConsoleFormatterOptions) =
    inherit ConsoleFormatter("hass")
    
    override this.Write(logEntry, scopeProvider, textWriter) =
        textWriter.Write("{0:yyyy/MM/dd HH:mm:ss} ", DateTime.Now)
        textWriter.Write("[{0}] ", logEntry.LogLevel)
        textWriter.Write("{0}: ", logEntry.Category)
        let message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception)
        textWriter.Write("{0}", message)