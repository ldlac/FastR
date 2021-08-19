using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using FastR;
using System.Reflection;

namespace FastR.Runnable
{
    public class Startup
    {
        private readonly Action<IServiceCollection> _cfgServiceBuilder;

        public Startup(IConfiguration configuration, Action<IServiceCollection> cfgServiceBuilder)
        {
            Configuration = configuration;
            _cfgServiceBuilder = cfgServiceBuilder;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            if (_cfgServiceBuilder is not null)
                _cfgServiceBuilder(services);

            //services.AddTransient<WeatherForecastService>();

            //services.AddFastR();
            //services.AddSwaggerGen();
            //services.AddFastROpenApiGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseSwagger();

            //app.UseSwaggerUI(c =>
            //{
            //    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API");
            //});

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseEndpoints(e =>
            {
                e.MapControllers();
            });

            app.MapFastR(Assembly.GetEntryAssembly().GetTypes());
        }
    }
}