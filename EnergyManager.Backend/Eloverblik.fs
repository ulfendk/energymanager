module EnergyManager.Backend.Eloverblik

type Config =
    { ApiKey : string }
    
type Data(config : Config) =
    member this.GetConsumptionBeforeReduced() = ()