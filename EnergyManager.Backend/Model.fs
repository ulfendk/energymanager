module EnergyManager.Backend.Model

open System
open System.Text.Json
open System.Text.Json.Serialization
open Giraffe
open Microsoft.AspNetCore.Hosting

type Region =
    | DK1
    | DK2
    member this.Value =
        match this with
        | DK1 -> "dk1"
        | DK2 -> "dk2"

[<JsonConverter(typedefof<UnixDateTimeJsonConverter>)>]        
[<CustomComparison;CustomEquality>]
type UnixDateTime =
    | Utc of Timestamp
    | LocalTime of TimestampWithOffset
    with
    member this.Seconds =
        match this with
        | Utc(ts) -> ts.seconds
        | LocalTime(ts) -> ts.seconds
        
    static member FromDateTime(value : DateTimeOffset) =
        match value with
        | _ when value.Offset = TimeSpan.Zero -> Utc({ seconds = value.ToUnixTimeSeconds() })
        | _ -> LocalTime({ seconds = value.ToUnixTimeSeconds(); offset = (int64 value.Offset.TotalSeconds) })

    static member FromSeconds(seconds : int64) =
        let dto = DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime()
        UnixDateTime.FromDateTime dto

    static member Now() = UnixDateTime.FromDateTime(DateTimeOffset.Now)

    member this.ToDateTimeOffset() = DateTimeOffset.FromUnixTimeSeconds(this.Seconds).ToLocalTime()
        
    override this.Equals other =
        match other with
        | :? UnixDateTime as p -> (this :> IEquatable<_>).Equals p
        | _ -> false

    interface IEquatable<UnixDateTime> with
        member this.Equals other = (other.Seconds) = (this.Seconds)

    override this.GetHashCode () = (this.Seconds).GetHashCode()   

    interface IComparable with
        member this.CompareTo other =
            match other with
            | :? UnixDateTime as p -> (this :> IComparable<_>).CompareTo p
            | _ -> -1

    interface IComparable<UnixDateTime> with
        member this.CompareTo other =
            let diff = other.Seconds - this.Seconds
            match diff with
            | 0L -> 0
            | _ when diff > 0 -> 1
            | _ -> -1
and Timestamp = { seconds : int64 }
and TimestampWithOffset = { seconds : int64; offset : int64 }
and UnixDateTimeJsonConverter() =
    inherit JsonConverter<UnixDateTime>()
    override this.Write(writer: Utf8JsonWriter, value: UnixDateTime, options : JsonSerializerOptions) =
        let seconds =
            match value with
            | Utc ts -> ts.seconds
            | LocalTime ts -> ts.seconds
        writer.WriteStringValue(DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToIsoString())
        ()
    override this.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options : JsonSerializerOptions) =
        failwith "Not supported"

let private getSecondsFromTimestamp time =
    match time with
    | Utc({ seconds = ts }) -> ts
    | LocalTime({ seconds = ts; offset = _ }) -> ts

[<CustomComparison;CustomEquality>]
type UnixDateTimeInterval =
    { From : UnixDateTime
      To : UnixDateTime }
    
    interface IEquatable<UnixDateTimeInterval> with
        member this.Equals other = other.From.Equals(this.From) && other.To.Equals(this.To)

    override this.Equals other =
        match other with
        | :? UnixDateTimeInterval as p -> (this :> IEquatable<_>).Equals p
        | _ -> false
    override this.GetHashCode () = ((getSecondsFromTimestamp this.From) + (getSecondsFromTimestamp this.To)).GetHashCode()   

    interface IComparable with
        member this.CompareTo other =
            match other with
            | :? UnixDateTimeInterval as p -> (this :> IComparable<UnixDateTimeInterval>).CompareTo p
            | _ -> -1

    interface IComparable<UnixDateTimeInterval> with
        member this.CompareTo other = //((this.From :> IComparable<UnixDateTime>).CompareTo other.From)
            let afterStart = (getSecondsFromTimestamp this.From) >= (getSecondsFromTimestamp other.From)
            let beforeEnd = (getSecondsFromTimestamp this.To) <= (getSecondsFromTimestamp other.To)
            let result = 
                match (afterStart, beforeEnd) with
                | (true, true) -> 0
                | (true, false) -> 1
                | _ -> -1
            result


type PricePoint =
    { Timestamp : UnixDateTime
      Region : string
      Price : decimal
      IsActual : bool
      LastUpdated : UnixDateTime }

type Fee =
    { Regular: decimal
      Reduced: decimal}

[<CustomComparison;CustomEquality>]
type IntervalFeeAndTariff =
    { Interval : UnixDateTimeInterval
      Tariff: decimal
      Fee: Fee }
    
    interface IEquatable<IntervalFeeAndTariff> with
        member this.Equals other = (this.Interval :> IEquatable<UnixDateTimeInterval>).Equals(other.Interval)

    override this.Equals other =
        match other with
        | :? IntervalFeeAndTariff as n -> (this :> IEquatable<_>).Equals n
        | _ -> false
        
    override this.GetHashCode () = this.Interval.GetHashCode()   

    interface IComparable with
        member this.CompareTo other =
            match other with
            | :? IntervalFeeAndTariff as n -> (this :> IComparable<_>).CompareTo n
            | _ -> -1

    interface IComparable<IntervalFeeAndTariff> with
        member this.CompareTo other = ((this.Interval :> IComparable<UnixDateTimeInterval>).CompareTo other.Interval)

type SpotPrice =
    { Timestamp : UnixDateTime
      BasePriceVat : decimal
      AllFeesAndVat: decimal
      ReducedFeesAndVat: decimal
      FullPriceReducedFeeVat: decimal
      FullPriceVat : decimal
      IsPrediction : bool
      IsComplete : bool 
      LastUpdated : UnixDateTime } with
    member this.ToDkk() =
        { this with
            BasePriceVat = this.BasePriceVat / 100m
            AllFeesAndVat = this.AllFeesAndVat / 100m
            ReducedFeesAndVat = this.ReducedFeesAndVat / 100m
            FullPriceReducedFeeVat = this.FullPriceReducedFeeVat / 100m
            FullPriceVat = this.FullPriceVat / 100m }