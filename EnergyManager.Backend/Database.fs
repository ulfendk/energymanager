module EnergyManager.Backend.Database

open System
open System.Runtime.CompilerServices
open Dapper.FSharp.SQLite
open EnergyManager.Backend.Model
open EnergyManager.Backend.SpotPrice
open EnergyManager.Backend.Utils
open Microsoft.Data.Sqlite
open Microsoft.FSharp.Core

Dapper.FSharp.SQLite.OptionTypes.register()

type DbSpotPrice =
    { Hour : int64
      BasePriceVat : double
      AllFeesAndVat: double
      ReducedFeesAndVat: double
      FullPriceReducedFeeVat: double
      FullPriceVat : double
      IsActual : int64
      IsComplete : int64
      LastUpdated : int64 }
    
type IDataRepository =
    abstract InsertPrice : spotPrice : SpotPrice -> int
    
    abstract InsertOrUpdatePrices : spotPrices : SpotPrice seq -> int
    
    abstract GetPrice : hour : UnixDateTime -> SpotPrice

    abstract GetPrices : hour : UnixDateTime * count : int -> SpotPrice seq

type DataRepository() =
    // let getConnection() = new SqliteConnection("Data Source=ConfigFiles/data.db")
    let getConnection() = new SqliteConnection("Data Source=/config/ha/energy_manager/data.db")
    let mapToDb (spotPrice : SpotPrice) =
        { Hour = spotPrice.Timestamp.Seconds
          BasePriceVat = float (decimal spotPrice.BasePriceVat)
          AllFeesAndVat = float (decimal spotPrice.AllFeesAndVat)
          ReducedFeesAndVat = float (decimal spotPrice.ReducedFeesAndVat)
          FullPriceReducedFeeVat = float (decimal spotPrice.FullPriceReducedFeeVat)
          FullPriceVat = float (decimal spotPrice.FullPriceVat)
          IsActual =
              match spotPrice.IsPrediction with
              | true -> 0
              | false -> 1
          IsComplete =
              match spotPrice.IsComplete with
              | true -> 1
              | false -> 0
          LastUpdated = spotPrice.LastUpdated.Seconds }

    let mapFromDb (spotPrice : DbSpotPrice) =
        { Timestamp = UnixDateTime.FromSeconds spotPrice.Hour
          BasePriceVat = decimal spotPrice.BasePriceVat
          AllFeesAndVat = decimal spotPrice.AllFeesAndVat
          ReducedFeesAndVat = decimal spotPrice.ReducedFeesAndVat
          FullPriceReducedFeeVat = decimal spotPrice.FullPriceReducedFeeVat
          FullPriceVat = decimal spotPrice.FullPriceVat
          IsPrediction = spotPrice.IsActual = 0
          IsComplete = spotPrice.IsComplete <> 0
          LastUpdated = UnixDateTime.FromSeconds spotPrice.LastUpdated }
    
    let insertPrice (spotPrice : SpotPrice) =
        async {
            use conn = getConnection()
            conn.Open() |> ignore
            let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
            let! inserted =
                insert {
                    into spotPricesTable
                    value (mapToDb spotPrice) }
                |> conn.InsertOrReplaceAsync
                |> Async.AwaitTask
                
            return inserted
        } |> Async.RunSynchronously
        
    let insertOrUpdatePrices (spotPrices : SpotPrice seq) =
        async {
            use conn = getConnection()
            conn.Open() |> ignore
            let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
            let! inserted =
                insert {
                    into spotPricesTable
                    values (spotPrices |> Seq.map mapToDb |> Seq.toList) }
                |> conn.InsertOrReplaceAsync
                |> Async.AwaitTask
            return inserted
        } |> Async.RunSynchronously
        
    let getPrice(hour : UnixDateTime) =
        async {
            use conn = getConnection()
            conn.Open() |> ignore
            let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
            return! select {
                for p in spotPricesTable do
                where (p.Hour = hour.Seconds)
            } |> conn.SelectAsync<DbSpotPrice> |> Async.AwaitTask
        } |> Async.RunSynchronously

    let getAllPrices() =
        async {
            use conn = getConnection()
            conn.Open() |> ignore
            let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
            return! select {
                for p in spotPricesTable do
                selectAll
            } |> conn.SelectAsync<DbSpotPrice> |> Async.AwaitTask 
        } |> Async.RunSynchronously

    let getPrices(hour : UnixDateTime, count : int) =
        async {
            use conn = getConnection()
            conn.Open() |> ignore
            let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
            return! select {
                for p in spotPricesTable do
                where (p.Hour >= hour.Seconds)
                orderBy p.Hour
            } |> conn.SelectAsync<DbSpotPrice> |> Async.AwaitTask
        }
        |> Async.RunSynchronously
        |> Seq.truncate count

    interface IDataRepository with
        member this.InsertPrice(spotPrice : SpotPrice) = insertPrice spotPrice
        
        member this.InsertOrUpdatePrices(spotPrices : SpotPrice seq) = insertOrUpdatePrices spotPrices
        
        member this.GetPrice(timestamp : UnixDateTime) =
            let hour =
                let dto = timestamp.ToDateTimeOffset()
                let hourDto = dto.Add(TimeSpan(0, -dto.Minute, -dto.Second))
                UnixDateTime.FromDateTime hourDto
            (getPrice hour)
            |> Seq.map mapFromDb
            |> Seq.head

        member this.GetPrices(timestamp : UnixDateTime, count : int) =
            let hour =
                let dto = timestamp.ToDateTimeOffset()
                let hourDto = dto.Add(TimeSpan(0, -dto.Minute, -dto.Second))
                UnixDateTime.FromDateTime hourDto
            (getPrices (hour, count))
            |> Seq.map mapFromDb
