using System.Text.Json;

namespace CurrencyConversionAPI.Models
{
    public class ResponseResult
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public JsonElement Data { get; set; }
    }
}