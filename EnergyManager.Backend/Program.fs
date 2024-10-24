module EnergyManager.Backend.App

open System
open System.Collections
open System.ComponentModel
open System.IO
open System.Text.Json
open System.Threading.RateLimiting
open EnergyManager.Backend.Background
open EnergyManager.Backend.Carnot
open EnergyManager.Backend.Database
open EnergyManager.Backend.HomeAssistant
open EnergyManager.Backend.HomeAssistantBackgroundWorker
open EnergyManager.Backend.Tariff
open EnergyManager.Backend.Utils
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe

open EnergyManager.Backend.Model
open Microsoft.Extensions.Logging.Console

// Config
let edsConfig = { EnergiDataService.Config.Region = DK2 }

// let tariffsConfig =
//     { TariffsAndFeePeriods  = [
//         { Start = DateOnly(2024, 10, 1)
//           End = DateOnly(2025, 3, 31)
//           Intervals =  [
//             { Interval = (0, 5); Tariff = Low}
//             { Interval = (6, 16); Tariff = High }
//             { Interval = (17, 20);Tariff = Peak }
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
//         ] }

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "EnergyManager.Backend" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "EnergyManager.Backend" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let envs = String.Join($"<br />{Environment.NewLine}", seq {
                 for entry in Environment.GetEnvironmentVariables() do
                   let de = entry :?> DictionaryEntry
                   $"%A{de.Key}: %A{de.Value}" })
                // let dic = 
                // let keys = dic.Keys |> Seq.map (fun x -> $"%A{x}")
                // Environment.GetEnvironmentVariables() |> Seq.map (fun (k, v) -> $"%s{k}: %s{v}")
            return! Successful.OK envs next ctx
            // let repo = ctx.GetService<IDataRepository>()
            // let spotPrice = repo.GetPrice(UnixDateTime.Now())
            // let json = JsonSerializer.Serialize(spotPrice)
            // let greetings = $"Current spotprice is %s{json}"
            // let model     = { Text = greetings }
            // let view      = Views.index model
            // let data = json// htmlView view
            // return! Successful.OK data next ctx
        }

let carnotGetLatestHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let carnotConfig = ctx.GetService<Carnot.Config>()
            let data = Carnot.Data.getLatest carnotConfig
            return! Successful.OK data next ctx
        }

let carnotGetConfigHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let carnotConfig = ctx.GetService<Carnot.Config>()
            return! Successful.OK carnotConfig next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler
                // routef "/hello/%s" indexHandler
                route "/carnot" >=> carnotGetLatestHandler // warbler (fun _ -> json (Carnot.Data.getLatest carnotConfig))
                route "/carnot/config" >=> carnotGetConfigHandler // json carnotConfig
                route "/eds" >=> warbler (fun _ -> json (EnergiDataService.Data.getLatest edsConfig))
                route "/eds/config" >=> json edsConfig
                //route "/tariffs" >=> warbler (fun _ -> json (Tariff.Data.getTariffs tariffsConfig))
                // route "/tariffs/config" >=> json tariffsConfig
                //route "/spotprice" >=> warbler (fun _ -> json (SpotPrice.getPriceAndTariff (EnergiDataService.Data.getLatest edsConfig) (Tariff.Data.getTariffs tariffsConfig)))
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5099",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler))
            // .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    services.AddTransient<IDataRepository, DataRepository>() |> ignore
    services.AddHostedService<PricesBackgroundWorker>() |> ignore
    services.AddSingleton<Carnot.Config>(
        fun s ->
            let config = s.GetService<IConfiguration>()
            { Carnot.Config.Region = config["Carnot:Region"]
              Carnot.Config.Username = config["Carnot:User"]
              Carnot.Config.ApiKey = config["Carnot:ApiKey"] }) |> ignore
    services.AddSingleton<EnergiDataService.Config>(edsConfig)  |> ignore
    // services.AddSingleton<Tariff.Config>(tariffsConfig) |> ignore
    services.AddSingleton<CarnotSource>() |> ignore
    services.AddSingleton<HomeAssistant.Config>(
        fun s ->
            { HomeAssistant.Config.Url = "http://supervisor/core"
              Token = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") }) |> ignore
    services.AddSingleton<HomeAssistantApi>() |> ignore
    services.AddHostedService<HomeAssistantBackgroundWorker>() |> ignore
    services.AddTransient<ConsoleFormatterOptions, HassAddOnConsoleFormatterOptions>() |> ignore

let configureAppConfiguration  (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
    config
        .AddJsonFile("appsettings.json",false,true)
        .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName ,true)
        .AddUserSecrets(configureServices.GetType().Assembly)
        .AddEnvironmentVariables() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
            .AddConsoleFormatter<HassAddOnConsoleFormatter, HassAddOnConsoleFormatterOptions>(fun x -> ())
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    // if not (Directory.Exists "/config/ha/energy_manager") then
    //     Directory.CreateDirectory("/config/ha/energy_manager") |> ignore
    //
    // Directory.GetFiles("ConfigFiles")
    // |> Seq.map (fun x -> FileInfo(x))
    // |> Seq.iter (fun x ->
    //     let destinationFile = $"/config/ha/energy_manager/%s{x.Name}"
    //     if not (File.Exists(destinationFile)) then
    //         File.Copy(x.FullName, destinationFile))

    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureAppConfiguration(configureAppConfiguration)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0