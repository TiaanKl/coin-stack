using Microsoft.Extensions.DependencyInjection;

namespace CoinStack.Mobile.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoinStackMobileCore(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceContextService, DeviceContextService>();
        return services;
    }
}

public interface IDeviceContextService
{
    string PlatformName { get; }
}

internal sealed class DeviceContextService : IDeviceContextService
{
    public string PlatformName => Environment.OSVersion.Platform.ToString();
}
