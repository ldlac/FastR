using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace FastR.Runnable
{
    public static class Runnable
    {
        public static void Run(string[] args, Action<IServiceCollection> cfgServiceBuilder = null, Func<IWebHostBuilder, IWebHostBuilder> cfgWebHostBuilder = null)
        {
            CreateHostBuilder(args, cfgServiceBuilder, cfgWebHostBuilder).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, Action<IServiceCollection> cfgServiceBuilder, Func<IWebHostBuilder, IWebHostBuilder> cfgWebHostBuilder) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    if (cfgWebHostBuilder is not null)
                        webBuilder = cfgWebHostBuilder(webBuilder);

                    webBuilder.UseStartup(c => new Startup(c.Configuration, cfgServiceBuilder));
                });
    }
}