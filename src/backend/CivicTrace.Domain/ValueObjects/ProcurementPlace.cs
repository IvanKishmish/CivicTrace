using CivicTrace.Domain.Enums;

namespace CivicTrace.Domain.ValueObjects;

/// <summary>
/// Місце за сторінкою закупівлі. Координат на сторінках Prozorro немає: якщо вони є, вони додані окремо
/// й мають стан перевірки.
/// </summary>
public sealed record ProcurementPlace(
    string? Address,
    PlaceType Type,
    double? Latitude,
    double? Longitude,
    CoordinatesStatus? CoordinatesStatus);