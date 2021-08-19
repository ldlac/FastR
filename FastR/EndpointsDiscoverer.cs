namespace FastR
{
    using System.Collections.Generic;
    using System.Reflection;

    public class EndpointsDiscoverer : IEndpointsDiscoverer
    {
        private readonly List<EndpointDiscovered> _endpoints = new List<EndpointDiscovered>();

        public IEnumerable<EndpointDiscovered> Discover()
        {
            return _endpoints;
        }

        internal void AddEndpoint(MethodInfo endpoint)
        {
            var attribute = endpoint.GetCustomAttribute<EndpointAttribute>();

            _endpoints.Add(new EndpointDiscovered()
            {
                MethodInfo = endpoint,
                Path = attribute.Path,
                Verb = attribute.Verb
            });
        }
    }
}