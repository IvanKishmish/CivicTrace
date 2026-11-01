namespace CivicTrace.Domain.Enums;

/// <summary>Спосіб закупівлі.</summary>
public enum ProcurementMethod
{
    //Закупівля без використання електронної системи.
    NoElectronicSystem,

    //Запит (ціни) пропозицій.
    RequestForQuotations,

    //Відкриті торги з особливостями.
    OpenTendersWithSpecifics
}
