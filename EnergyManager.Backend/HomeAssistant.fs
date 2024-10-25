module EnergyManager.Backend.HomeAssistant

open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core

type Config =
    { Url : string
      Token : string }

type EntityAttributes =
    { [<JsonPropertyName("friendly_name")>]
      FriendlyName : string option
      
      [<JsonPropertyName("icon")>]
      Icon : string option
      
      [<JsonPropertyName("device_class")>]
      DeviceClass : string option
      
      [<JsonPropertyName("unit_of_measurement")>]
      UnitOfMeasurement : string option
      
      [<JsonPropertyName("extra_values")>]
      ExtraValues : (obj list) option }

type EnumEntityAttributes =
    { [<JsonPropertyName("friendly_name")>]
      FriendlyName : string option
      
      [<JsonPropertyName("icon")>]
      Icon : string option
      
      [<JsonPropertyName("device_class")>]
      DeviceClass : string option
                  
      [<JsonPropertyName("options")>]
      Options : string list option}
    
type SensorPayload<'TAttributes> =
    { [<JsonPropertyName("state")>]
      State : string
      
      [<JsonPropertyName("last_updated")>]
      LastUpdated : DateTimeOffset option
      
      [<JsonPropertyName("attributes")>]
      Attributes : 'TAttributes option }
    
type EntityPayload =
    | ValueSensor of SensorPayload<EntityAttributes>
    | TextSensor of SensorPayload<EntityAttributes>
    | EnumSensor of SensorPayload<EnumEntityAttributes>
    
 type HomeAssistantApi(config : Config, logger : ILogger<HomeAssistantApi>) =
    let jsonOptions =
        let options = JsonSerializerOptions()
        options.DefaultIgnoreCondition <- Serialization.JsonIgnoreCondition.WhenWritingNull
        options
        
    let jsonContent obj = JsonContent.Create(obj, ?options = Some jsonOptions)
    
    let getClient() = 
        let client = new HttpClient()
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer %s{config.Token}")
        client
        
    member this.SetEntity(entityName : string, payload : EntityPayload) =
        use client = getClient() 

        let state =
            match payload with
            | TextSensor(s) -> s.State
            | ValueSensor(s) -> s.State
            | EnumSensor(s) -> s.State

        logger.LogInformation("Setting entity state for {entityName} to {state}", entityName, state)
        let (entityType, jsonPayload) =
            match payload with
            | ValueSensor s -> ("sensor", jsonContent s)
            | TextSensor t -> ("sensor", jsonContent t)
            | EnumSensor e -> ("sensor", jsonContent e)

        let url = $"%s{config.Url}/api/states/%s{entityType}.energy_manager_%s{entityName}"
        
        // logger.LogInformation("Calling {url} with payload: {payload}", url, jsonPayload)
        async {
            let! result = client.PostAsync(url, jsonPayload) |> Async.AwaitTask
            if not result.IsSuccessStatusCode then
                logger.LogError("Failed to update {entityName} state with error code {errorCode}", entityName, result.StatusCode)

            return result.StatusCode
        } |> Async.RunSynchronously

    member this.GetHistory(entityId: string) = 
        use client = getClient()
        
        async {
            let! result = client.GetAsync($"%s{config.Url}/api/history/period?filter_entity_ids=%s{entityId}") |> Async.AwaitTask

            if not result.IsSuccessStatusCode then
                logger.LogError("Failed to fetch history for {entityName} with error code {errorCode}", entityId, result.StatusCode)

            return! result.Content.ReadFromJsonAsync<SensorPayload<EntityAttributes> array array>() |> Async.AwaitTask
        } |> Async.RunSynchronously
        
    member this.GetEntities() =
        use client = getClient()
        
        async {
            let! result = client.GetAsync($"%s{config.Url}/api/states") |> Async.AwaitTask

            if not result.IsSuccessStatusCode then
                logger.LogError("Failed to fetch states")

            return! result.Content.ReadFromJsonAsync<SensorPayload<EntityAttributes> array>() |> Async.AwaitTask
        } |> Async.RunSynchronously
