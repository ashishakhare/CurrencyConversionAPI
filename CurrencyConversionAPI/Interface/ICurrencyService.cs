using CurrencyConversionAPI.Models;

namespace CurrencyConversionAPI.Interface
{
    public interface ICurrencyService
    {
        Task<ResponseResult> GetLatestRatesAsync(string baseCurrency);
        Task<ResponseResult> ConvertCurrencyAsync(string from, string to, decimal amount);
        Task<ResponseResult> GetHistoricalRatesAsync(string baseCurrency, string startDate, string endDate, int page, int pageSize);
    }
}