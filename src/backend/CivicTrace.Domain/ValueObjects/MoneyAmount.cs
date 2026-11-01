namespace CivicTrace.Domain.ValueObjects;

// Сума разом із позначкою ПДВ. VatIncluded = null означає «на сторінці не вказано».
public sealed record MoneyAmount(decimal Value, bool? VatIncluded, string Currency = "UAH");