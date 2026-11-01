using CivicTrace.Domain.Common;
using CivicTrace.Domain.Enums;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

/// <summary>Прогалина в даних: що не знайдено (Missing) або що потребує перевірки (NeedsCheck).</summary>
public sealed class DataGap : Entity
{
    public Guid ProcurementId { get; private set; }

    public DataGapKind Kind { get; private set; }
    public string Text { get; private set; } = string.Empty;

    private DataGap()
    { } // ef

    private DataGap(Guid id, Guid procurementId, DataGapKind kind, string text) : base(id)
    {
        ProcurementId = procurementId;
        Kind = kind;
        Text = text.Trim();
    }

    internal static ErrorOr<DataGap> Create(Guid procurementId, DataGapKind kind, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Error.Validation("DataGap.TextRequired", "Data gap text is required.");

        return new DataGap(Guid.CreateVersion7(), procurementId, kind, text);
    }
}