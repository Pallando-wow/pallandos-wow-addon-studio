using AddonStudio.Core.Wow;

namespace AddonStudio.Wow.Toc;

public sealed class TocQaAnalyzer
{
    public IReadOnlyList<TocDiagnostic> Analyze(
        TocDocument document,
        string runtimeAddonDirectory,
        int? expectedInterface = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            runtimeAddonDirectory);

        var runtimeRoot = Path.GetFullPath(
            runtimeAddonDirectory);
        var diagnostics =
            new List<TocDiagnostic>();

        AnalyzeRequiredMetadata(
            document,
            diagnostics);
        AnalyzeInterfaces(
            document,
            diagnostics,
            expectedInterface);
        AnalyzeDuplicateMetadata(
            document,
            diagnostics);
        AnalyzeRuntimeFiles(
            document,
            runtimeRoot,
            diagnostics);

        return diagnostics
            .OrderByDescending(
                diagnostic => diagnostic.Severity)
            .ThenBy(
                diagnostic => diagnostic.LineNumber ??
                    int.MaxValue)
            .ThenBy(
                diagnostic => diagnostic.Code,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static void AnalyzeRequiredMetadata(
        TocDocument document,
        ICollection<TocDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(
                document.GetMetadata("Interface")))
        {
            diagnostics.Add(
                new TocDiagnostic(
                    "TOC001",
                    TocDiagnosticSeverity.Error,
                    "Required metadata 'Interface' is missing."));
        }

        if (string.IsNullOrWhiteSpace(
                document.Title))
        {
            diagnostics.Add(
                new TocDiagnostic(
                    "TOC003",
                    TocDiagnosticSeverity.Warning,
                    "Metadata 'Title' is missing."));
        }

        if (string.IsNullOrWhiteSpace(
                document.Version))
        {
            diagnostics.Add(
                new TocDiagnostic(
                    "TOC004",
                    TocDiagnosticSeverity.Warning,
                    "Metadata 'Version' is missing. Releases require an authoritative TOC version."));
        }
    }

    private static void AnalyzeInterfaces(
        TocDocument document,
        ICollection<TocDiagnostic> diagnostics,
        int? expectedInterface)
    {
        var metadataLine = document.Lines
            .LastOrDefault(line =>
                line.Kind == TocLineKind.Metadata &&
                string.Equals(
                    line.Key,
                    "Interface",
                    StringComparison.OrdinalIgnoreCase));

        var interfaceValue =
            document.GetMetadata("Interface");

        if (string.IsNullOrWhiteSpace(interfaceValue))
        {
            return;
        }

        var values = interfaceValue
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (values.Length == 0 ||
            values.Any(value =>
                !int.TryParse(
                    value,
                    out var parsed) ||
                parsed <= 0))
        {
            diagnostics.Add(
                new TocDiagnostic(
                    "TOC002",
                    TocDiagnosticSeverity.Error,
                    "Metadata 'Interface' must contain one or more positive numeric interface values.",
                    metadataLine?.LineNumber));

            return;
        }

        if (expectedInterface is int expected &&
            !values.Any(value =>
                int.TryParse(
                    value,
                    out var parsed) &&
                parsed == expected))
        {
            diagnostics.Add(
                new TocDiagnostic(
                    "TOC009",
                    TocDiagnosticSeverity.Warning,
                    $"Metadata 'Interface' does not include the expected current interface {expected}. Found: {interfaceValue}.",
                    metadataLine?.LineNumber));
        }
    }

    private static void AnalyzeDuplicateMetadata(
        TocDocument document,
        ICollection<TocDiagnostic> diagnostics)
    {
        var groups = document.Lines
            .Where(line =>
                line.Kind == TocLineKind.Metadata &&
                !string.IsNullOrWhiteSpace(line.Key))
            .GroupBy(
                line => line.Key!,
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            foreach (var duplicate in group.Skip(1))
            {
                diagnostics.Add(
                    new TocDiagnostic(
                        "TOC005",
                        TocDiagnosticSeverity.Warning,
                        $"Metadata '{group.Key}' is defined more than once. The last value is effective.",
                        duplicate.LineNumber));
            }
        }
    }

    private static void AnalyzeRuntimeFiles(
        TocDocument document,
        string runtimeRoot,
        ICollection<TocDiagnostic> diagnostics)
    {
        var seenPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var line in document.Lines.Where(
                     line =>
                         line.Kind == TocLineKind.File &&
                         !string.IsNullOrWhiteSpace(
                             line.Value)))
        {
            var runtimePath = line.Value!;

            if (!TryResolveRuntimePath(
                    runtimeRoot,
                    runtimePath,
                    out var fullPath))
            {
                diagnostics.Add(
                    new TocDiagnostic(
                        "TOC007",
                        TocDiagnosticSeverity.Error,
                        $"Runtime file path '{runtimePath}' escapes the addon directory or is rooted.",
                        line.LineNumber,
                        runtimePath));

                continue;
            }

            var normalizedPath =
                NormalizeRuntimePath(runtimePath);

            if (!seenPaths.Add(normalizedPath))
            {
                diagnostics.Add(
                    new TocDiagnostic(
                        "TOC006",
                        TocDiagnosticSeverity.Warning,
                        $"Runtime file '{runtimePath}' is listed more than once.",
                        line.LineNumber,
                        runtimePath));
            }

            if (!File.Exists(fullPath))
            {
                diagnostics.Add(
                    new TocDiagnostic(
                        "TOC008",
                        TocDiagnosticSeverity.Error,
                        $"Runtime file '{runtimePath}' does not exist.",
                        line.LineNumber,
                        runtimePath));
            }
        }
    }

    private static bool TryResolveRuntimePath(
        string runtimeRoot,
        string runtimePath,
        out string fullPath)
    {
        fullPath = string.Empty;

        if (runtimePath.Length == 0 ||
            runtimePath.StartsWith(
                '/') ||
            runtimePath.StartsWith(
                '\\') ||
            LooksLikeWindowsRootedPath(runtimePath))
        {
            return false;
        }

        var relativePath = runtimePath
            .Replace(
                '\\',
                Path.DirectorySeparatorChar)
            .Replace(
                '/',
                Path.DirectorySeparatorChar);

        fullPath = Path.GetFullPath(
            Path.Combine(
                runtimeRoot,
                relativePath));

        var relativeToRoot =
            Path.GetRelativePath(
                runtimeRoot,
                fullPath);

        return !relativeToRoot.Equals(
                   "..",
                   StringComparison.Ordinal) &&
               !relativeToRoot.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !Path.IsPathRooted(relativeToRoot);
    }

    private static bool LooksLikeWindowsRootedPath(
        string value) =>
        value.Length >= 3 &&
        char.IsLetter(value[0]) &&
        value[1] == ':' &&
        (value[2] == '\\' ||
         value[2] == '/');

    private static string NormalizeRuntimePath(
        string value) =>
        value
            .Replace(
                '\\',
                '/')
            .TrimStart('/');
}
