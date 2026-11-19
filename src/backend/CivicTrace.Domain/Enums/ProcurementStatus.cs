namespace CivicTrace.Domain.Enums;

/// <summary>Наш статус закупівлі. Статус із Prozorro зберігається окремо як SourceStatus.</summary>
public enum ProcurementStatus
{
    Active,
    ContractSigned,
    NotHeld,
    Cancelled
}