using CivicTrace.Domain.Common;
using CivicTrace.Domain.Entities.Models;
using CivicTrace.Domain.Enums;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

public sealed class ProcurementEvent : Entity
{
    public Guid ProcurementId { get; private set; }

    /// <summary>Дата події. null — сторінка закупівлі дати не показує.</summary>
    public DateOnly? Date { get; private set; }

    public EventStage Stage { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? DateNote { get; private set; }
    public string SourceUrl { get; private set; } = string.Empty;

    /// <summary>Порядок у джерелі; потрібен, щоб події з однаковою або невідомою датою не змінювали порядок.</summary>
    public int SortOrder { get; private set; }

    private ProcurementEvent()
    { } // ef

    private ProcurementEvent(Guid id, Guid procurementId, int sortOrder, ProcurementEventCreationParams args) : base(id)
    {
        ProcurementId = procurementId;
        SortOrder = sortOrder;
        Date = args.Date;
        Stage = args.Stage;
        Title = args.Title.Trim();
        DateNote = string.IsNullOrWhiteSpace(args.DateNote) ? null : args.DateNote.Trim();
        SourceUrl = args.SourceUrl.Trim();
    }

    internal static ErrorOr<ProcurementEvent> Create(Guid procurementId, int sortOrder, ProcurementEventCreationParams args)
    {
        var validationResult = ValidateInvariants(args);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new ProcurementEvent(Guid.CreateVersion7(), procurementId, sortOrder, args);
    }

    private static ErrorOr<Success> ValidateInvariants(ProcurementEventCreationParams args)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(args.Title))
            errors.Add(Error.Validation("ProcurementEvent.TitleRequired", "Event title is required."));

        if (string.IsNullOrWhiteSpace(args.SourceUrl))
            errors.Add(Error.Validation("ProcurementEvent.SourceUrlRequired", "Event source URL is required."));

        return errors.Count > 0 ? errors : Result.Success;
    }
}