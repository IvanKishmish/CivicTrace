namespace CivicTrace.Application.Common.Interfaces;

public interface IGeocoderService
{
    Task<(double Latitude, double Longitude)?> GeocodePoltavaAsync(string address, CancellationToken ct = default);
}