using Jellyfin.Plugin.HistraNet.Api;
using Jellyfin.Plugin.HistraNet.Service;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HistraNet;

/// <summary>
/// Registers plugin services with the Jellyfin host.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<HistraClient>();

        // Token source is selected at call time by RoutingUserTokenProvider based
        // on the configured TokenSource ("config" per-user map, or "manager" HTTP
        // lookup). Both concrete providers are registered; the router picks one.
        serviceCollection.AddSingleton<ConfigUserTokenProvider>();
        serviceCollection.AddSingleton<ManagerUserTokenProvider>();
        serviceCollection.AddSingleton<IUserTokenProvider, RoutingUserTokenProvider>();

        serviceCollection.AddHostedService<ScrobblerService>();
        serviceCollection.AddHostedService<WatchStateExporter>();
    }
}
