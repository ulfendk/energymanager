module EnergyManager.Backend.Database

open System
open Dapper.FSharp.SQLite
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
      FullPriceVat : double }

type IDataRepository =
    abstract InsertPrice: spotPrice : SpotPrice -> int
    abstract GetPrices: SpotPrice seq

// let newPerson = { Id = Guid.NewGuid(); FirstName = "Roman"; LastName = "Provaznik"; Position = 1; DateOfBirth = None }
type DataRepository() =
    let insertPrice (spotPrice : SpotPrice) =
        use conn = new SqliteConnection("Data Source=data.db")
        conn.Open() |> ignore
        let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
        insert {
            into spotPricesTable
            value
                { Hour = spotPrice.Hour.ToUnixTimeSeconds()
                  BasePriceVat = float (decimal spotPrice.BasePriceVat)
                  AllFeesAndVat = float (decimal spotPrice.AllFeesAndVat)
                  ReducedFeesAndVat = float (decimal spotPrice.ReducedFeesAndVat)
                  FullPriceReducedFeeVat = float (decimal spotPrice.FullPriceReducedFeeVat)
                  FullPriceVat = float (decimal spotPrice.FullPriceVat) }
        } |> conn.InsertAsync
        
    let getPrices() =
        use conn = new SqliteConnection("Data Source=data.db")
        conn.Open() |> ignore
        let spotPricesTable = table'<DbSpotPrice> "SpotPrices"
        select {
            for p in spotPricesTable do
            selectAll
            // where (p.Position > 5 && p.Position < 10)
        }
        |> conn.SelectAsync<DbSpotPrice>

    interface IDataRepository with
        member this.InsertPrice(spotPrice : SpotPrice) = (insertPrice spotPrice).GetAwaiter().GetResult()
        
        member this.GetPrices =
            getPrices().GetAwaiter().GetResult()
            |> Seq.map (fun x -> { SpotPrice.Hour  = toLocalDateTimeOffset (DateTimeOffset.FromUnixTimeSeconds(x.Hour))
                                   BasePriceVat = decimal x.BasePriceVat * 1m<dkk/kWh>
                                   AllFeesAndVat = decimal x.AllFeesAndVat* 1m<dkk/kWh>
                                   ReducedFeesAndVat = decimal x.ReducedFeesAndVat* 1m<dkk/kWh>
                                   FullPriceReducedFeeVat = decimal x.FullPriceReducedFeeVat* 1m<dkk/kWh>
                                   FullPriceVat = decimal x.FullPriceVat * 1m<dkk/kWh>})