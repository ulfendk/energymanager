module EnergyManager.Backend.Utils

open System

[<Measure>] type dkk
[<Measure>] type ore
[<Measure>] type kWh

let getLocalOffset (timestamp : DateTime) =
    let timezone = TimeZoneInfo.Local
    timezone.GetUtcOffset(timestamp)

let asLocalDateTimeOffset (timestamp : DateTime) =
    let offset  = getLocalOffset timestamp
    DateTimeOffset(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, timestamp.Second, offset)    

let withLocalOffset (timestamp : DateTimeOffset) =
    asLocalDateTimeOffset timestamp.DateTime
   
let toLocalDateTimeOffset (timestamp : DateTimeOffset) =
    let offset  = getLocalOffset timestamp.DateTime
    (asLocalDateTimeOffset timestamp.DateTime).Add(offset)
