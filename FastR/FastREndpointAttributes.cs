namespace FastR
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EndpointsAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EndpointAttribute : Attribute
    {
        public EndpointAttribute(EndpointVerb verb, string path)
        {
            Verb = verb;
            Path = path;
        }

        public EndpointVerb Verb { get; }
        public string Path { get; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class DependsAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class BodyAttribute : Attribute
    {
    }

    public enum EndpointVerb
    {
        GET,
        PUT,
        POST,
        DELETE
    }
}