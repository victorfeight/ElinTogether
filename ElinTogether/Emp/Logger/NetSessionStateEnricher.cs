using ElinTogether.Net;
using Serilog.Core;
using Serilog.Events;

namespace ElinTogether;

internal class NetSessionStateEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // The watchdog runs off-thread with an already captured context. Never
        // query Steam or Unity through the normal session enricher on that path.
        if (logEvent.Properties.ContainsKey("NetworkTrace")) return;
        if (!NetSession.Instance.HasActiveConnection) {
            return;
        }

        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("NetSession", NetSession.Instance, true));
    }
}