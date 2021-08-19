namespace FastR
{
    using System.Collections.Generic;
    using System.Reflection;

    public interface IEndpointsDiscoverer
    {
        public IEnumerable<EndpointDiscovered> Discover();
    }

    public class EndpointDiscovered
    {
        public MethodInfo MethodInfo { get; init; }

        public string Path { get; init; }

        public EndpointVerb Verb { get; init; }
    }
}