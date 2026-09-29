namespace AddonStudio.Core.Wow;

public sealed record TocDiagnostic(
    string Code,
    TocDiagnosticSeverity Severity,
    string Message,
    int? LineNumber = null,
    string? RuntimePath = null);
