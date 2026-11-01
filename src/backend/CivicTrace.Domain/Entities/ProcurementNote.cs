using CivicTrace.Domain.Common;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

/// <summary>Нейтральний факт із посиланням на джерело, без оцінок.</summary>
public sealed class ProcurementNote : Entity
{
    public Guid ProcurementId { get; private set; }

    public string Text { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;

    private ProcurementNote()
    { } // ef

    private ProcurementNote(Guid id, Guid procurementId, string text, string sourceUrl) : base(id)
    {
        ProcurementId = procurementId;
        Text = text.Trim();
        SourceUrl = sourceUrl.Trim();
    }

    internal static ErrorOr<ProcurementNote> Create(Guid procurementId, string text, string sourceUrl)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(text))
            errors.Add(Error.Validation("ProcurementNote.TextRequired", "Note text is required."));

        if (string.IsNullOrWhiteSpace(sourceUrl))
            errors.Add(Error.Validation("ProcurementNote.SourceUrlRequired", "Note source URL is required."));

        if (errors.Count > 0)
            return errors;

        return new ProcurementNote(Guid.CreateVersion7(), procurementId, text, sourceUrl);
    }
}