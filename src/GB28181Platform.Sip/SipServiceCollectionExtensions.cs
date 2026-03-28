using GB28181Platform.Sip.Handlers;
using GB28181Platform.Sip.Server;
using GB28181Platform.Sip.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace GB28181Platform.Sip;

public static class SipServiceCollectionExtensions
{
    public static IServiceCollection AddGb28181Sip(this IServiceCollection services, Action<SipServerOptions>? configure = null)
    {
        var options = new SipServerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<DeviceSessionManager>();
        services.AddSingleton<RegisterHandler>();
        services.AddSingleton<KeepaliveHandler>();
        services.AddSingleton<Gb28181SipServer>();

        return services;
    }
}
