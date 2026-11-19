using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Common.Options;
using CivicTrace.Persistence.Geocoding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Persistence.Prozorro;

public static class ProzorroServiceCollectionExtensions
{
    public static IServiceCollection AddProzorroClient(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ProzorroOptions.SectionName).Get<ProzorroOptions>() ?? new ProzorroOptions();

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));

        services.AddHttpClient<IProzorroClient, ProzorroClient>(client =>
        {
            var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CivicTrace-hackathon/1.0");
        });

        services.AddHttpClient<IProzorroSaleClient, ProzorroSaleClient>(client =>
        {
            client.BaseAddress = new Uri("https://procedure.prozorro.sale/api/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CivicTrace-hackathon/1.0");
        });

        services.AddHttpClient<IGeocoderService, NominatimGeocoderService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CivicTrace-hackathon/1.0");
        });

        services.AddHostedService<ProzorroSyncHostedService>();
        services.AddHostedService<RentalSyncHostedService>();

        return services;
    }
}
