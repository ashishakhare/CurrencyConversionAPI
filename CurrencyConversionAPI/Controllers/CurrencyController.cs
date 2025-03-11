using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.Wrap;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using OpenTelemetry.Trace;
using CurrencyConversionAPI.Interface;

namespace CurrencyConversionAPI.Controllers
{
    [ApiController]
    [Route("api/currency")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [EnableRateLimiting("fixed")]
    public class CurrencyController : ControllerBase
    {
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<CurrencyController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AsyncPolicyWrap<HttpResponseMessage> _policy;
        private readonly Tracer _tracer;

        public CurrencyController(ICurrencyService currencyService, ILogger<CurrencyController> logger, IHttpContextAccessor httpContextAccessor, TracerProvider tracerProvider)
        {
            _currencyService = currencyService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _tracer = tracerProvider.GetTracer("CurrencyAPI");

            var retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));

            _policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        private void LogRequest(string endpoint, string method, int responseCode, long responseTime)
        {
            var clientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var jwtToken = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var clientId = "Unknown";

            if (!string.IsNullOrEmpty(jwtToken))
            {
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(jwtToken);
                clientId = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "Unknown";
            }

            _logger.LogInformation("Request: ClientIP={ClientIP}, ClientId={ClientId}, Method={Method}, Endpoint={Endpoint}, ResponseCode={ResponseCode}, ResponseTime={ResponseTime}ms", 
                clientIp, clientId, method, endpoint, responseCode, responseTime);
        }

        [HttpGet("latest")]
        [Authorize(Policy = "AdminOnly")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> GetLatestRates(string baseCurrency = "EUR")
        {
            var stopwatch = Stopwatch.StartNew();
            var span = _tracer.StartActiveSpan("GetLatestRates");
            var result = await _currencyService.GetLatestRatesAsync(baseCurrency);
            stopwatch.Stop();

            LogRequest("api/currency/latest", "GET", result.StatusCode, stopwatch.ElapsedMilliseconds);
            span.End();

            return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }

        [HttpGet("convert")]
        [Authorize(Policy = "UserOnly")]
        public async Task<IActionResult> ConvertCurrency(string from, string to, decimal amount)
        {
            var stopwatch = Stopwatch.StartNew();
            var span = _tracer.StartActiveSpan("ConvertCurrency");
            var result = await _currencyService.ConvertCurrencyAsync(from, to, amount);
            stopwatch.Stop();

            LogRequest("api/currency/convert", "GET", result.StatusCode, stopwatch.ElapsedMilliseconds);
            span.End();

            return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }

        [HttpGet("historical")]
        [Authorize(Policy = "AdminOnly")] 
        public async Task<IActionResult> GetHistoricalRates(string baseCurrency = "EUR", string startDate = "", string endDate = "", int page = 1, int pageSize = 10)
        {
            var stopwatch = Stopwatch.StartNew();
            var span = _tracer.StartActiveSpan("GetHistoricalRates");
            var result = await _currencyService.GetHistoricalRatesAsync(baseCurrency, startDate, endDate, page, pageSize);
            stopwatch.Stop();

            LogRequest("api/currency/historical", "GET", result.StatusCode, stopwatch.ElapsedMilliseconds);
            span.End();

            return result.IsSuccess ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }
    }
}
