namespace CivicTrace.Application.Features.Export.Queries.ExportProcurements;

public sealed record ExportFile(byte[] Content, string ContentType, string FileName);