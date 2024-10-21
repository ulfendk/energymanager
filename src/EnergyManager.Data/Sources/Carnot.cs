using System.Net.Http.Json;
using EnergyManager.Data.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnergyManager.Data.Sources;

public class CarnotDataPrediction
{
    public int? Id { get; set; }

    public string? Pricearea { get; set; }

    public string? Energysource { get; set; }

    public string? Utctime { get; set; }

    public string? Dktime { get; set; }

    public float? Prediction { get; set; }

    public bool? Latest { get; set; }

    public string? Addedtime { get; set; }
}

public class CarnotData
{
    public CarnotDataPrediction[]? Predictions { get; set; }
}

public class CarnotSettings
{
    public bool IsEnabled { get; set; }
    public string BaseUrl { get; set; }
    public string Username { get; set; }
    public string ApiKey { get; set; }
}

public class CarnotDataLoader
{
    // private readonly EnergyAssistantRepository _repository;
    private readonly CarnotSettings _settings;
    private readonly ILogger<CarnotDataLoader> _logger;

    public async Task<bool> GetIsEnabledAsync()
    {
        // var settings = await _repository.GetCarnotSettingsAsync();
        //
        // return settings.IsEnabled;
    }

    public CarnotDataLoader(IOptions<CarnotSettings> config, ILogger<CarnotDataLoader> logger)// EnergyAssistantRepository repository) 
    {
        _settings = config.Value;
        // _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SpotPrice[]> GetPredictionsAsync(CancellationToken cancellationToken)
    {
        // var generalSettings = await _repository.GetGeneralSettingsAsync();
        // var settings = await _repository.GetCarnotSettingsAsync();
        
        if (!_settings.IsEnabled) throw new InvalidOperationException($"{nameof(CarnotDataLoader)} is not enabled.");
        
        try
        {
            _logger.LogInformation("Downloading predictions from Carnot.dk");
            string uri = $"https://whale-app-dquqw.ondigitalocean.app/openapi/get_predict?energysource=spotprice&region={generalSettings.Region}&daysahead=7";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("username", settings.User);
            client.DefaultRequestHeaders.Add("apikey", settings.ApiKey);

            var now = DateTimeOffset.Now;
            
            var result = await client.GetFromJsonAsync<CarnotData>(uri, cancellationToken);

            var spotPrices = result?.Predictions?.Select(x => new SpotPrice
            {
                Hour = DateTimeOffset.Parse(x.Utctime ?? throw new InvalidDataException(nameof(x.Utctime)))
                    .ToOffset(now.Offset),
                Region = (x.Pricearea ?? generalSettings.Region).ToLowerInvariant(),
                Source = "Carnot.dk",
                RawPrice = (decimal?)x.Prediction / 1000m ?? throw new InvalidDataException(nameof(x.Prediction)),
                IsPrediction = true
            }).ToArray() ?? Array.Empty<SpotPrice>();

            if (spotPrices.Length > 0)
            {
                _logger.LogInformation("Downloaded {count} prices.", spotPrices.Length);
            }
            else
            {
                _logger.LogError("Failed to download any prices.");
            }

            return spotPrices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed with message: {message}.", ex.Message);

            return Array.Empty<SpotPrice>();
        }
    }
}

public class Carnot
{
    
}