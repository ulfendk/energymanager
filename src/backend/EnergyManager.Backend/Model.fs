module EnergyManager.Backend.Model

open System
open EnergyManager.Backend.Utils

type Region =
    | DK1
    | DK2
    member this.Value =
        match this with
        | DK1 -> "dk1"
        | DK2 -> "dk2"

type PricePoint =
    { Timestamp : DateTimeOffset
      Region : string
      Price : decimal<ore/kWh>
      IsActual : bool }

