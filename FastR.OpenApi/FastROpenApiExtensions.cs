namespace FastR.OpenApi
{
    using Microsoft.Extensions.DependencyInjection;
    using Swashbuckle.AspNetCore.Swagger;

    public static class FastROpenApiExtensions
    {
        public static IServiceCollection AddFastROpenApiGen(this IServiceCollection services)
        {
            services.AddTransient<ISwaggerProvider, FastROpenApiGenerator>();

            return services;
        }
    }
}