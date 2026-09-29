namespace AddonStudio.Core.Wow;

public sealed record TocLine(
    int LineNumber,
    TocLineKind Kind,
    string RawText,
    string? Key = null,
    string? Value = null);
