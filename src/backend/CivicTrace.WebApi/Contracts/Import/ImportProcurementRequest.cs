namespace CivicTrace.WebApi.Contracts.Import;

/// <summary>
/// ApiId — внутрішній ідентифікатор закупівлі в Prozorro (32 hex-символи; показаний на сторінці закупівлі поруч
/// із UA-... і збережений у полі apiId). DryRun за замовчуванням true: нічого не зберігається, лише показується результат.
/// </summary>
public sealed record ImportProcurementRequest(string ApiId, bool DryRun = true, bool ReplaceExisting = false);