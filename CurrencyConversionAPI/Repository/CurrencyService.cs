using System.Text.Json;
using System.Diagnostics;
using CurrencyConversionAPI.Interface;
using CurrencyConversionAPI.Models;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Wrap;
using OpenTelemetry.Trace;

namespace CurrencyConversionAPI.Repository
{
    public class CurrencyService : ICurrencyService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CurrencyService> _logger;
        private readonly Tracer _tracer;
        private static readonly HashSet<string> ExcludedCurrencies = new() { "TRY", "PLN", "THB", "MXN" };
        private readonly AsyncPolicyWrap<HttpResponseMessage> _policy;

        public CurrencyService(IHttpClientFactory factory, IMemoryCache cache, ILogger<CurrencyService> logger, TracerProvider tracerProvider)
        {
            _cache = cache;
            _httpClientFactory = factory;
            _logger = logger;
            _tracer = tracerProvider.GetTracer("CurrencyAPI");

            var retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));

            _policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        public async Task<ResponseResult> GetLatestRatesAsync(string baseCurrency)
        {
            var cacheKey = $"latest_rates_{baseCurrency}";
            if (_cache.TryGetValue(cacheKey, out JsonElement cachedRates))
            {
                return new ResponseResult { IsSuccess = true, StatusCode = 200, Data = cachedRates };
            }

            var client = _httpClientFactory.CreateClient();
            var stopwatch = Stopwatch.StartNew();
            var span = _tracer.StartActiveSpan("GetLatestRates");
            var response = await _policy.ExecuteAsync(() => client.GetAsync($"https://api.frankfurter.app/latest?base={baseCurrency}"));
            stopwatch.Stop();
            span.End();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch latest exchange rates. StatusCode: {StatusCode}", response.StatusCode);
                return new ResponseResult { IsSuccess = false, StatusCode = (int)response.StatusCode, Message = "Error fetching latest exchange rates." };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseBody);
            _cache.Set(cacheKey, jsonData, TimeSpan.FromHours(1));

            return new ResponseResult { IsSuccess = true, StatusCode = 200, Data = jsonData };
        }

        public async Task<ResponseResult> ConvertCurrencyAsync(string from, string to, decimal amount)
        {
            if (ExcludedCurrencies.Contains(from) || ExcludedCurrencies.Contains(to))
            {
                return new ResponseResult { IsSuccess = false, StatusCode = 400, Message = "Conversion involving restricted currencies is not allowed." };
            }

            var cacheKey = $"exchange_{from}_{to}";
            if (_cache.TryGetValue(cacheKey, out decimal exchangeRate))
            {
                return new ResponseResult { IsSuccess = true, StatusCode = 200, Data = JsonSerializer.SerializeToElement(new { from, to, amount, convertedAmount = amount * exchangeRate, exchangeRate }) };
            }

            var client = _httpClientFactory.CreateClient();
            var stopwatch = Stopwatch.StartNew();
            var span = _tracer.StartActiveSpan("ConvertCurrency");
            var response = await _policy.ExecuteAsync(() => client.GetAsync($"https://api.frankfurter.app/latest?base={from}"));
            stopwatch.Stop();
            span.End();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch exchange rate for {From} to {To}. StatusCode: {StatusCode}", from, to, response.StatusCode);
                return new ResponseResult { IsSuccess = false, StatusCode = (int)response.StatusCode, Message = "Error fetching exchange rate." };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseBody);
            exchangeRate = jsonData.GetProperty("rates").GetProperty(to).GetDecimal();

            _cache.Set(cacheKey, exchangeRate, TimeSpan.FromHours(1));
            return new ResponseResult { IsSuccess = true, StatusCode = 200, Data = JsonSerializer.SerializeToElement(new { from, to, amount, convertedAmount = amount * exchangeRate, exchangeRate }) };
        }

        public async Task<ResponseResult> GetHistoricalRatesAsync(string baseCurrency, string startDate, string endDate, int page, int pageSize)
        {
            var client = _httpClientFactory.CreateClient();
            var stopwatch = Stopwatch.StartNew();
            var span = _tracer.StartActiveSpan("GetHistoricalRates");
            var response = await _policy.ExecuteAsync(() => client.GetAsync($"https://api.frankfurter.app/{startDate}..{endDate}?base={baseCurrency}"));
            stopwatch.Stop();
            span.End();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch historical exchange rates. StatusCode: {StatusCode}", response.StatusCode);
                return new ResponseResult { IsSuccess = false, StatusCode = (int)response.StatusCode, Message = "Error fetching historical exchange rates." };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonData = JsonSerializer.Deserialize<JsonElement>(responseBody);

            return new ResponseResult { IsSuccess = true, StatusCode = 200, Data = jsonData };
        }
    }
}
