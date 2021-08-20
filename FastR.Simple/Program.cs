using FastR.Runnable;
using FastR.Simple.Services;
using Microsoft.Extensions.DependencyInjection;

Runnable.Run(args, s => s.AddTransient<WeatherForecastService>());