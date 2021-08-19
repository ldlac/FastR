namespace FastR.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;

    // Based on Swagger Implementation https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGenerator.cs

    public class FastROpenApiGenerator : ISwaggerProvider
    {
        private readonly ISchemaGenerator _schemaGenerator;
        private readonly SwaggerGeneratorOptions _options;
        private readonly IEndpointsDiscoverer _endpointsDiscoverer;

        public FastROpenApiGenerator(
            SwaggerGeneratorOptions options,
            IEndpointsDiscoverer endpointsDiscoverer,
            ISchemaGenerator schemaGenerator)
        {
            _options = options ?? new SwaggerGeneratorOptions();
            _endpointsDiscoverer = endpointsDiscoverer;
            _schemaGenerator = schemaGenerator;
        }

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null)
        {
            if (!_options.SwaggerDocs.TryGetValue(documentName, out OpenApiInfo info))
                throw new UnknownSwaggerDocument(documentName, _options.SwaggerDocs.Select(d => d.Key));

            //var applicableApiDescriptions = _apiDescriptionsProvider.ApiDescriptionGroups.Items
            //    .SelectMany(group => group.Items)
            //    .Where(apiDesc => !(_options.IgnoreObsoleteActions && apiDesc.CustomAttributes().OfType<ObsoleteAttribute>().Any()))
            //    .Where(apiDesc => _options.DocInclusionPredicate(documentName, apiDesc));

            var applicableApiDescriptions = _endpointsDiscoverer.Discover();

            var schemaRepository = new SchemaRepository(documentName);

            var swaggerDoc = new OpenApiDocument
            {
                Info = info,
                Servers = GenerateServers(host, basePath),
                Paths = GeneratePaths(applicableApiDescriptions, schemaRepository),
                Components = new OpenApiComponents
                {
                    Schemas = schemaRepository.Schemas,
                    SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>(_options.SecuritySchemes)
                },
                SecurityRequirements = new List<OpenApiSecurityRequirement>(_options.SecurityRequirements)
            };

            //var filterContext = new DocumentFilterContext(applicableApiDescriptions, _schemaGenerator, schemaRepository);
            //foreach (var filter in _options.DocumentFilters)
            //{
            //    filter.Apply(swaggerDoc, filterContext);
            //}

            swaggerDoc.Components.Schemas = new SortedDictionary<string, OpenApiSchema>(swaggerDoc.Components.Schemas);

            return swaggerDoc;
        }

        private IList<OpenApiServer> GenerateServers(string host, string basePath)
        {
            if (_options.Servers.Any())
            {
                return new List<OpenApiServer>(_options.Servers);
            }

            return (host == null && basePath == null)
                ? new List<OpenApiServer>()
                : new List<OpenApiServer> { new OpenApiServer { Url = $"{host}{basePath}" } };
        }

        private OpenApiPaths GeneratePaths(IEnumerable<EndpointDiscovered> endpointsDiscovered, SchemaRepository schemaRepository)
        {
            //var apiDescriptionsByPath = apiDescriptions
            //    .OrderBy(_options.SortKeySelector)
            //    .GroupBy(apiDesc => apiDesc.RelativePathSansQueryString());

            var paths = new OpenApiPaths();
            foreach (var group in endpointsDiscovered.OrderBy(x => x.Path).GroupBy(x => x.Path))
            {
                paths.Add($"/{group.Key}",
                    new OpenApiPathItem
                    {
                        Operations = GenerateOperations(group, schemaRepository)
                    });
            };

            return paths;
        }

        private IDictionary<OperationType, OpenApiOperation> GenerateOperations(
            IEnumerable<EndpointDiscovered> endpointsDiscovered,
            SchemaRepository schemaRepository)
        {
            //var apiDescriptionsByMethod = apiDescriptions
            //    .OrderBy(_options.SortKeySelector)
            //    .GroupBy(apiDesc => apiDesc.HttpMethod);

            var operations = new Dictionary<OperationType, OpenApiOperation>();

            foreach (var group in endpointsDiscovered.OrderBy(x => x.Verb).GroupBy(x => ((OperationType)(int)x.Verb).ToString()))
            {
                var httpMethod = group.Key;

                //if (httpMethod == null)
                //    throw new SwaggerGeneratorException(string.Format(
                //        "Ambiguous HTTP method for action - {0}. " +
                //        "Actions require an explicit HttpMethod binding for Swagger/OpenAPI 3.0",
                //        group.First().ActionDescriptor.DisplayName));

                if (group.Count() > 1 && _options.ConflictingActionsResolver == null)
                    throw new SwaggerGeneratorException(string.Format(
                        "Conflicting method/path combination \"{0} {1}\" for actions - {2}. " +
                        "Actions require a unique method/path combination for Swagger/OpenAPI 3.0. Use ConflictingActionsResolver as a workaround",
                        httpMethod,
                        group.First().Path,
                        string.Join(",", group.Select(x => x.Verb))));

                //var apiDescription = (group.Count() > 1) ? _options.ConflictingActionsResolver(group) : group.Single();

                var apiDescription = group.First();

                operations.Add(OperationTypeMap[httpMethod.ToUpper()], GenerateOperation(apiDescription, schemaRepository));
            };

            return operations;
        }

        private OpenApiOperation GenerateOperation(EndpointDiscovered apiDescription, SchemaRepository schemaRepository)
        {
            try
            {
                var operation = new OpenApiOperation
                {
                    Tags = GenerateOperationTags(apiDescription),
                    OperationId = apiDescription.MethodInfo.Name,
                    Parameters = GenerateParameters(apiDescription, schemaRepository),
                    RequestBody = GenerateRequestBody(apiDescription, schemaRepository),
                    Responses = GenerateResponses(apiDescription, schemaRepository),
                    //Deprecated = apiDescription.CustomAttributes().OfType<ObsoleteAttribute>().Any()
                };

                //apiDescription.TryGetMethodInfo(out MethodInfo methodInfo);
                //var filterContext = new OperationFilterContext(apiDescription, _schemaGenerator, schemaRepository, apiDescription.MethodInfo);
                //foreach (var filter in _options.OperationFilters)
                //{
                //    filter.Apply(operation, filterContext);
                //}

                return operation;
            }
            catch (Exception ex)
            {
                throw new SwaggerGeneratorException(
                    message: $"Failed to generate Operation for action - {apiDescription.MethodInfo.Name}. See inner exception",
                    innerException: ex);
            }
        }

        private IList<OpenApiTag> GenerateOperationTags(EndpointDiscovered apiDescription)
        {
            return new List<OpenApiTag>();
            //return _options.TagsSelector(apiDescription)
            //    .Select(tagName => new OpenApiTag { Name = tagName })
            //    .ToList();
        }

        private IList<OpenApiParameter> GenerateParameters(EndpointDiscovered apiDescription, SchemaRepository schemaRespository)
        {
            var parameterInfos = apiDescription.MethodInfo
                .GetParameters()
                .Where(x => x.GetCustomAttribute<BodyAttribute>() == null
                    && x.GetCustomAttribute<DependsAttribute>() == null
                    && x.GetCustomAttribute<BindNeverAttribute>() == null
                    && !x.ParameterType.Equals(typeof(HttpResponse)))
                .ToList();

            //var applicableApiParameters = apiDescription.ParameterDescriptions
            //    .Where(apiParam =>
            //    {
            //        return (!apiParam.IsFromBody() && !apiParam.IsFromForm())
            //            && (!apiParam.CustomAttributes().OfType<BindNeverAttribute>().Any())
            //            && (apiParam.ModelMetadata == null || apiParam.ModelMetadata.IsBindingAllowed);
            //    });

            return parameterInfos
                .Select(apiParam => GenerateParameter(apiDescription, apiParam, schemaRespository))
                .ToList();
        }

        private OpenApiParameter GenerateParameter(
            EndpointDiscovered apiDescription,
            ParameterInfo apiParameter,
            SchemaRepository schemaRepository)
        {
            var name = _options.DescribeAllParametersInCamelCase
                ? apiParameter.Name.ToCamelCase()
                : apiParameter.Name;

            var path = apiDescription.Path;

            //var location = (apiParameter.Source != null && ParameterLocationMap.ContainsKey(apiParameter.Source))
            //    ? ParameterLocationMap[apiParameter.Source]
            //    : ParameterLocation.Query;

            // TODO: HANDLE HEADER AND COOKIE
            var location = path.ToLower().Contains($"{{{apiParameter.Name.ToLower()}}}") ? ParameterLocation.Path : ParameterLocation.Query;

            var isRequired = location == ParameterLocation.Path
                || apiParameter.CustomAttributes.Any(attr => RequiredAttributeTypes.Contains(attr.GetType()));

            var schema = GenerateSchema(
                    apiParameter.ParameterType,
                    schemaRepository,
                    null,
                    apiParameter);

            var parameter = new OpenApiParameter
            {
                Name = name,
                In = location,
                Required = isRequired,
                Schema = schema
            };

            //var filterContext = new ParameterFilterContext(
            //    apiParameter,
            //    _schemaGenerator,
            //    schemaRepository,
            //    apiParameter.PropertyInfo(),
            //    apiParameter.ParameterInfo());

            //foreach (var filter in _options.ParameterFilters)
            //{
            //    filter.Apply(parameter, filterContext);
            //}

            return parameter;
        }

        private OpenApiSchema GenerateSchema(
            Type type,
            SchemaRepository schemaRepository,
            PropertyInfo propertyInfo = null,
            ParameterInfo parameterInfo = null)
        {
            try
            {
                return _schemaGenerator.GenerateSchema(type, schemaRepository, propertyInfo, parameterInfo);
            }
            catch (Exception ex)
            {
                throw new SwaggerGeneratorException(
                    message: $"Failed to generate schema for type - {type}. See inner exception",
                    innerException: ex);
            }
        }

        private OpenApiRequestBody GenerateRequestBody(
            EndpointDiscovered apiDescription,
            SchemaRepository schemaRepository)
        {
            OpenApiRequestBody requestBody = null;
            RequestBodyFilterContext filterContext = null;

            var bodyParameter = apiDescription.MethodInfo.GetParameters().FirstOrDefault(x => x.GetCustomAttribute<BodyAttribute>() != null);

            //var bodyParameter = apiDescription.ParameterDescriptions
            //    .FirstOrDefault(paramDesc => paramDesc.IsFromBody());

            var formParameters = new List<ApiParameterDescription>();

            //var formParameters = apiDescription.ParameterDescriptions
            //    .Where(paramDesc => paramDesc.IsFromForm());

            if (bodyParameter != null)
            {
                requestBody = GenerateRequestBodyFromBodyParameter(apiDescription, schemaRepository, bodyParameter);

                //filterContext = new RequestBodyFilterContext(
                //    bodyParameterDescription: bodyParameter,
                //    formParameterDescriptions: null,
                //    schemaGenerator: _schemaGenerator,
                //    schemaRepository: schemaRepository);
            }
            else if (formParameters.Any())
            {
                requestBody = GenerateRequestBodyFromFormParameters(null, schemaRepository, formParameters);

                filterContext = new RequestBodyFilterContext(
                    bodyParameterDescription: null,
                    formParameterDescriptions: formParameters,
                    schemaGenerator: _schemaGenerator,
                    schemaRepository: schemaRepository);
            }

            //if (requestBody != null)
            //{
            //    foreach (var filter in _options.RequestBodyFilters)
            //    {
            //        filter.Apply(requestBody, filterContext);
            //    }
            //}

            return requestBody;
        }

        private OpenApiRequestBody GenerateRequestBodyFromBodyParameter(
            EndpointDiscovered apiDescription,
            SchemaRepository schemaRepository,
            ParameterInfo bodyParameter)
        {
            //var contentTypes = InferRequestContentTypes(apiDescription);

            var contentTypes = new List<string>() { "application/json" };

            //var isRequired = bodyParameter.CustomAttributes().Any(attr => RequiredAttributeTypes.Contains(attr.GetType()));
            var isRequired = false;

            var schema = GenerateSchema(
                bodyParameter.ParameterType,
                schemaRepository,
                null,
                bodyParameter);

            return new OpenApiRequestBody
            {
                Content = contentTypes
                    .ToDictionary(
                        contentType => contentType,
                        contentType => new OpenApiMediaType
                        {
                            Schema = schema
                        }
                    ),
                Required = isRequired
            };
        }

        private IEnumerable<string> InferRequestContentTypes(ApiDescription apiDescription)
        {
            // If there's content types explicitly specified via ConsumesAttribute, use them
            var explicitContentTypes = apiDescription.CustomAttributes().OfType<ConsumesAttribute>()
                .SelectMany(attr => attr.ContentTypes)
                .Distinct();
            if (explicitContentTypes.Any()) return explicitContentTypes;

            // If there's content types surfaced by ApiExplorer, use them
            var apiExplorerContentTypes = apiDescription.SupportedRequestFormats
                .Select(format => format.MediaType)
                .Distinct();
            if (apiExplorerContentTypes.Any()) return apiExplorerContentTypes;

            return Enumerable.Empty<string>();
        }

        private OpenApiRequestBody GenerateRequestBodyFromFormParameters(
            ApiDescription apiDescription,
            SchemaRepository schemaRepository,
            IEnumerable<ApiParameterDescription> formParameters)
        {
            var contentTypes = InferRequestContentTypes(apiDescription);
            contentTypes = contentTypes.Any() ? contentTypes : new[] { "multipart/form-data" };

            var schema = GenerateSchemaFromFormParameters(formParameters, schemaRepository);

            return new OpenApiRequestBody
            {
                Content = contentTypes
                    .ToDictionary(
                        contentType => contentType,
                        contentType => new OpenApiMediaType
                        {
                            Schema = schema,
                            Encoding = schema.Properties.ToDictionary(
                                entry => entry.Key,
                                entry => new OpenApiEncoding { Style = ParameterStyle.Form }
                            )
                        }
                    )
            };
        }

        private OpenApiSchema GenerateSchemaFromFormParameters(
            IEnumerable<ApiParameterDescription> formParameters,
            SchemaRepository schemaRepository)
        {
            var properties = new Dictionary<string, OpenApiSchema>();
            var requiredPropertyNames = new List<string>();

            foreach (var formParameter in formParameters)
            {
                var name = _options.DescribeAllParametersInCamelCase
                    ? formParameter.Name.ToCamelCase()
                    : formParameter.Name;

                var schema = (formParameter.ModelMetadata != null)
                    ? GenerateSchema(
                        formParameter.ModelMetadata.ModelType,
                        schemaRepository,
                        formParameter.PropertyInfo(),
                        formParameter.ParameterInfo())
                    : new OpenApiSchema { Type = "string" };

                properties.Add(name, schema);

                var isFromPath = true;

                if (formParameter.IsFromPath() || formParameter.CustomAttributes().Any(attr => RequiredAttributeTypes.Contains(attr.GetType())))
                    requiredPropertyNames.Add(name);
            }

            return new OpenApiSchema
            {
                Type = "object",
                Properties = properties,
                Required = new SortedSet<string>(requiredPropertyNames)
            };
        }

        private OpenApiResponses GenerateResponses(
            EndpointDiscovered apiDescription,
            SchemaRepository schemaRepository)
        {
            //var supportedResponseTypes = apiDescription.SupportedResponseTypes
            //    .DefaultIfEmpty(new ApiResponseType { StatusCode = 200 });

            var supportedResponseTypes = new List<ApiResponseType>() { new ApiResponseType { StatusCode = 200, Type = apiDescription.MethodInfo.ReturnType } };

            var responses = new OpenApiResponses();
            foreach (var responseType in supportedResponseTypes)
            {
                var statusCode = responseType.IsDefaultResponse() ? "default" : responseType.StatusCode.ToString();
                responses.Add(statusCode, GenerateResponse(apiDescription, schemaRepository, statusCode, responseType));
            }
            return responses;
        }

        private OpenApiResponse GenerateResponse(
            EndpointDiscovered apiDescription,
            SchemaRepository schemaRepository,
            string statusCode,
            ApiResponseType apiResponseType)
        {
            var description = ResponseDescriptionMap
                .FirstOrDefault((entry) => Regex.IsMatch(statusCode, entry.Key))
                .Value;

            var responseContentTypes = new List<string>() { "application/json" };
            //var responseContentTypes = InferResponseContentTypes(apiDescription, apiResponseType);

            var isAwaitable = apiResponseType.Type.GetMethod(nameof(Task.GetAwaiter)) != null;

            Type responseType = null;
            if (isAwaitable)
            {
                responseType = apiResponseType.Type.GetGenericArguments().FirstOrDefault();
            }
            else
            {
                responseType = apiResponseType.Type;
            }

            if (responseType is null)
            {
                return new OpenApiResponse
                {
                    Description = description,
                };
            }

            return new OpenApiResponse
            {
                Description = description,
                Content = responseContentTypes.ToDictionary(
                    contentType => contentType,
                    contentType => CreateResponseMediaType(responseType, schemaRepository)
                )
            };
        }

        private IEnumerable<string> InferResponseContentTypes(ApiDescription apiDescription, ApiResponseType apiResponseType)
        {
            // If there's no associated model, return an empty list (i.e. no content)
            if (apiResponseType.ModelMetadata == null) return Enumerable.Empty<string>();

            // If there's content types explicitly specified via ProducesAttribute, use them
            var explicitContentTypes = apiDescription.CustomAttributes().OfType<ProducesAttribute>()
                .SelectMany(attr => attr.ContentTypes)
                .Distinct();
            if (explicitContentTypes.Any()) return explicitContentTypes;

            // If there's content types surfaced by ApiExplorer, use them
            var apiExplorerContentTypes = apiResponseType.ApiResponseFormats
                .Select(responseFormat => responseFormat.MediaType)
                .Distinct();
            if (apiExplorerContentTypes.Any()) return apiExplorerContentTypes;

            return Enumerable.Empty<string>();
        }

        private OpenApiMediaType CreateResponseMediaType(Type type, SchemaRepository schemaRespository)
        {
            return new OpenApiMediaType
            {
                Schema = GenerateSchema(type, schemaRespository)
            };
        }

        private static readonly Dictionary<string, OperationType> OperationTypeMap = new Dictionary<string, OperationType>
        {
            { "GET", OperationType.Get },
            { "PUT", OperationType.Put },
            { "POST", OperationType.Post },
            { "DELETE", OperationType.Delete },
            { "OPTIONS", OperationType.Options },
            { "HEAD", OperationType.Head },
            { "PATCH", OperationType.Patch },
            { "TRACE", OperationType.Trace }
        };

        private static readonly Dictionary<BindingSource, ParameterLocation> ParameterLocationMap = new Dictionary<BindingSource, ParameterLocation>
        {
            { BindingSource.Query, ParameterLocation.Query },
            { BindingSource.Header, ParameterLocation.Header },
            { BindingSource.Path, ParameterLocation.Path }
        };

        private static readonly IEnumerable<Type> RequiredAttributeTypes = new[]
        {
            typeof(BindRequiredAttribute),
            typeof(RequiredAttribute)
        };

        private static readonly Dictionary<string, string> ResponseDescriptionMap = new Dictionary<string, string>
        {
            { "1\\d{2}", "Information" },
            { "2\\d{2}", "Success" },
            { "304", "Not Modified" },
            { "3\\d{2}", "Redirect" },
            { "400", "Bad Request" },
            { "401", "Unauthorized" },
            { "403", "Forbidden" },
            { "404", "Not Found" },
            { "405", "Method Not Allowed" },
            { "406", "Not Acceptable" },
            { "408", "Request Timeout" },
            { "409", "Conflict" },
            { "4\\d{2}", "Client Error" },
            { "5\\d{2}", "Server Error" },
            { "default", "Error" }
        };
    }

    internal static class ApiDescriptionExtensions
    {
        internal static string RelativePathSansQueryString(this ApiDescription apiDescription)
        {
            return apiDescription.RelativePath?.Split('?').First();
        }
    }

    internal static class ApiParameterDescriptionExtensions
    {
        internal static bool IsFromPath(this ApiParameterDescription apiParameter)
        {
            return (apiParameter.Source == BindingSource.Path);
        }

        internal static bool IsFromBody(this ApiParameterDescription apiParameter)
        {
            return (apiParameter.Source == BindingSource.Body);
        }

        internal static bool IsFromForm(this ApiParameterDescription apiParameter)
        {
            var source = apiParameter.Source;
            var elementType = apiParameter.ModelMetadata?.ElementType;

            return (source == BindingSource.Form || source == BindingSource.FormFile)
                || (elementType != null && typeof(IFormFile).IsAssignableFrom(elementType));
        }
    }

    internal static class StringExtensions
    {
        internal static string ToCamelCase(this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var cameCasedParts = value.Split('.')
                .Select(part => char.ToLowerInvariant(part[0]) + part.Substring(1));

            return string.Join(".", cameCasedParts);
        }
    }

    internal static class ApiResponseTypeExtensions
    {
        internal static bool IsDefaultResponse(this ApiResponseType apiResponseType)
        {
            var propertyInfo = apiResponseType.GetType().GetProperty("IsDefaultResponse");
            if (propertyInfo != null)
            {
                return (bool)propertyInfo.GetValue(apiResponseType);
            }

            // ApiExplorer < 2.1.0 does not support default response.
            return false;
        }
    }
}