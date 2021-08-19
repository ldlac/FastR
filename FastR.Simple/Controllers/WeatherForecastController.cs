using FastR.Simple.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FastR.Simple.Controllers
{
    [Endpoints]
    public static partial class Endpoints
    {
        [Endpoint(EndpointVerb.GET, "weather-forecasts")]
        public static async Task<int> GetWeatherForecasts([Depends] WeatherForecastService service, HttpResponse response, int id = 1)
        {
            response.StatusCode = 422;

            await service.Forecast();

            return id;
        }

        [Endpoint(EndpointVerb.GET, "weather-forecasts/{id}")]
        public static async Task<WeatherForecast[]> GetWeatherForecast(int id, [Depends] WeatherForecastService service, HttpResponse response)
        {
            response.StatusCode = 422;

            return await service.Forecast();
        }

        [Endpoint(EndpointVerb.POST, "weather-forecasts")]
        public static async Task<WeatherForecast[]> PostWeatherForecast([Body] WeatherForecast weatherForecast, [Depends] WeatherForecastService service)
        {
            return await service.Forecast();
        }
    }
}