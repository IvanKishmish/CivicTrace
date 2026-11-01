using CivicTrace.Domain.Enums;

namespace CivicTrace.Domain.Entities.Models;

public sealed record ProcurementEventCreationParams(
    DateOnly? Date,
    EventStage Stage,
    string Title,
    string? DateNote,
    string SourceUrl);