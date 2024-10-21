module EnergyManager.Backend.App

open System
open System.IO
open EnergyManager.Backend.Background
open EnergyManager.Backend.Carnot
open EnergyManager.Backend.Database
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

// Config
let edsConfig = { EnergiDataService.Config.Region = DK2 }

let tariffsConfig =
    { TariffsAndFeePeriods  = [
        { Start = DateOnly(2024, 10, 1)
          End = DateOnly(2025, 3, 31)
          Intervals =  [
            { Hours = [0..6]; Tariff = Low}
            { Hours = [6..17]; Tariff = High }
            { Hours = [17..21]; Tariff = Peak }
            { Hours = [21..24]; Tariff = High }
          ]
          Tariffs = dict [
              Low, 11.45m<ore/kWh>
              High, 34.34m<ore/kWh>
              Peak, 103.02m<ore/kWh>
          ]
          Fee =
              { Regular = 76.10m<ore/kWh>
                Reduced = 0.8m<ore/kWh> } }
        ] }

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

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

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
                route "/" >=> indexHandler "world"
                routef "/hello/%s" indexHandler
                route "/carnot" >=> carnotGetLatestHandler // warbler (fun _ -> json (Carnot.Data.getLatest carnotConfig))
                route "/carnot/config" >=> carnotGetConfigHandler // json carnotConfig
                route "/eds" >=> warbler (fun _ ->json (EnergiDataService.Data.getLatest edsConfig))
                route "/eds/config" >=> json edsConfig
                route "/tariffs" >=> warbler (fun _ ->json (Tariff.Data.getTariffs tariffsConfig))
                route "/tariffs/config" >=> json tariffsConfig
                route "/spotprice" >=> warbler (fun _ -> json (SpotPrice.getPriceAndTariff DateTimeOffset.Now (EnergiDataService.Data.getLatest edsConfig) (Tariff.Data.getTariffs tariffsConfig)))
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
            "http://localhost:5000",
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
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let rec configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    // services.AddHostedService<PricesBackgroundWorker>() |> ignore
    services.AddScoped<IDataRepository, DataRepository>() |> ignore
    services.AddSingleton<Carnot.Config>(
        fun s ->
            let config = s.GetService<IConfiguration>()
            { Carnot.Config.Region = config["Carnot:Region"]
              Carnot.Config.Username = config["Carnot:User"]
              Carnot.Config.ApiKey = config["Carnot:ApiKey"] }) |> ignore
    services.AddSingleton<EnergiDataService.Config>(edsConfig)  |> ignore
    services.AddSingleton<Tariff.Config>(tariffsConfig) |> ignore
    services.AddSingleton<CarnotSource>() |> ignore

let configureAppConfiguration  (context: WebHostBuilderContext) (config: IConfigurationBuilder) =
    config
        .AddJsonFile("appsettings.json",false,true)
        .AddJsonFile(sprintf "appsettings.%s.json" context.HostingEnvironment.EnvironmentName ,true)
        .AddUserSecrets(configureServices.GetType().Assembly)
        .AddEnvironmentVariables() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
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