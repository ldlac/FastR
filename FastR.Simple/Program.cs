using System;
using FastR.Runnable;
using FastR.Simple.Services;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Hello World!");
Runnable.Run(args, s => s.AddTransient<WeatherForecastService>());