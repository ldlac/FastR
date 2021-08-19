namespace FastR
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Primitives;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public static class FastRExtension
    {
        public static void AddFastR(this IServiceCollection services)
        {
            var endpointsDiscoverer = new EndpointsDiscoverer();

            services.AddSingleton(endpointsDiscoverer);
            services.AddSingleton<IEndpointsDiscoverer>(endpointsDiscoverer);
        }

        public static void MapFastR(this IApplicationBuilder applicationBuilder, params Type[] assemblies)
        {
            var allEndpoints = assemblies.SelectMany(GetEnpointMethodsOfAssembly);

            MapFastR(applicationBuilder, allEndpoints);
        }

        public static void MapFastR(this IApplicationBuilder applicationBuilder, Type assembly)
        {
            var allEndpoints = GetEnpointMethodsOfAssembly(assembly);

            MapFastR(applicationBuilder, allEndpoints);
        }

        private static IEnumerable<MethodInfo> GetEnpointMethodsOfAssembly(Type assembly)
        {
            return Assembly
                    .GetAssembly(assembly)
                    .GetTypes()
                    .Where(x => x.GetCustomAttribute<EndpointsAttribute>() is not null)
                    .SelectMany(x => x.GetMethods().Where(m => m.IsStatic && m.GetCustomAttribute<EndpointAttribute>() is not null))
                    .ToList();
        }

        private static void MapFastR(this IApplicationBuilder applicationBuilder, IEnumerable<MethodInfo> allEndpoints)
        {
            var endpointsDiscoverer = applicationBuilder.ApplicationServices.GetService<EndpointsDiscoverer>();

            foreach (var endpoint in allEndpoints)
            {
                endpointsDiscoverer?.AddEndpoint(endpoint);

                var attribute = endpoint.GetCustomAttribute<EndpointAttribute>();

                var parameters = endpoint.GetParameters();

                applicationBuilder.UseRouter((e) => e.MapVerb(attribute.Verb.ToString(), attribute.Path, async (h) =>
                {
                    var listParameters = await GetParameters(parameters, h, applicationBuilder);

                    dynamic endpointQuery() => endpoint.Invoke(null, listParameters.ToArray());

                    var isAwaitable = endpoint.ReturnType.GetMethod(nameof(Task.GetAwaiter)) != null;

                    object result = null;
                    if (isAwaitable)
                    {
                        if (endpoint.ReturnType.IsGenericType)
                        {
                            result = await endpointQuery();
                        }
                        else
                        {
                            await (Task)endpointQuery();
                        }
                    }
                    else
                    {
                        if (endpoint.ReturnType != typeof(void))
                        {
                            result = endpointQuery();
                        }
                        else
                        {
                            endpointQuery();
                        }
                    }

                    await h.Response.WriteAsJsonAsync(result);
                }));
            }
        }

        private static async Task<object[]> GetParameters(ParameterInfo[] parameterInfos, HttpContext httpContext, IApplicationBuilder applicationBuilder)
        {
            return await Task.WhenAll(parameterInfos.Select(parameterInfo => GetParameter(parameterInfo, httpContext, applicationBuilder)));
        }

        private static async Task<object> GetParameter(ParameterInfo parameterInfo, HttpContext httpContext, IApplicationBuilder applicationBuilder)
        {
            var serviceParameter = parameterInfo.GetCustomAttribute<DependsAttribute>();

            if (serviceParameter is not null)
            {
                var requiredService = applicationBuilder.ApplicationServices.GetRequiredService(parameterInfo.ParameterType);

                return requiredService;
            }

            if (httpContext is not null && httpContext.Request.Query.TryGetValue(parameterInfo.Name, out StringValues queryValue))
            {
                TypeConverter typeConverter = TypeDescriptor.GetConverter(parameterInfo.ParameterType);
                return typeConverter.ConvertFromString(queryValue.ToString());
            }

            if (httpContext is not null && httpContext.GetRouteValue(parameterInfo.Name) is object routeValue)
            {
                TypeConverter typeConverter = TypeDescriptor.GetConverter(parameterInfo.ParameterType);
                return typeConverter.ConvertFromString(routeValue.ToString());
            }

            if (parameterInfo.ParameterType.Equals(typeof(HttpResponse)))
            {
                return httpContext.Response;
            }

            var bodyParameter = parameterInfo.GetCustomAttribute<BodyAttribute>();

            if (bodyParameter is not null && httpContext is not null && httpContext.Request.Body.CanRead)
            {
                return await httpContext.ReadFromJson(parameterInfo.ParameterType);
            }

            if (parameterInfo.HasDefaultValue) return parameterInfo.DefaultValue;

            return null;
        }

        public static async Task<object> ReadFromJson(this HttpContext httpContext, Type @type)
        {
            using StreamReader reader = new(httpContext.Request.Body, Encoding.UTF8);

            var @object = JsonSerializer.Deserialize(await reader.ReadToEndAsync(), @type);

            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(@object, new ValidationContext(@object), results, true))
            {
                return @object;
            }

            httpContext.Response.StatusCode = 400;

            await httpContext.Response.WriteAsJsonAsync(results);

            return default;
        }

        private static async Task WriteAsJsonAsync(this HttpResponse response, object @object)
        {
            var result = JsonSerializer.Serialize(@object);
            response.ContentType = "application/json";
            await response.WriteAsync(result);
        }
    }
}