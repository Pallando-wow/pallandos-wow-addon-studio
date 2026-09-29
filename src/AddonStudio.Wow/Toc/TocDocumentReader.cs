using AddonStudio.Core.Wow;

namespace AddonStudio.Wow.Toc;

public sealed class TocDocumentReader
{
    public async Task<TocDocument> ReadAsync(
        string tocPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tocPath);

        var fullPath = Path.GetFullPath(tocPath);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".toc",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"File '{fullPath}' is not a WoW .toc file.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "WoW .toc file does not exist.",
                fullPath);
        }

        var sourceLines = await File.ReadAllLinesAsync(
            fullPath,
            cancellationToken);

        var lines = new TocLine[sourceLines.Length];

        for (var index = 0;
             index < sourceLines.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lines[index] = ParseLine(
                sourceLines[index],
                index + 1);
        }

        return new TocDocument(
            fullPath,
            lines);
    }

    private static TocLine ParseLine(
        string rawText,
        int lineNumber)
    {
        var trimmed = rawText.Trim();

        if (trimmed.Length == 0)
        {
            return new TocLine(
                lineNumber,
                TocLineKind.Blank,
                rawText);
        }

        if (trimmed.StartsWith(
                "##",
                StringComparison.Ordinal))
        {
            var body = trimmed[2..].TrimStart();
            var separatorIndex = body.IndexOf(':');

            if (separatorIndex > 0)
            {
                var key = body[..separatorIndex].Trim();
                var value = body[(separatorIndex + 1)..].Trim();

                if (key.Length > 0)
                {
                    return new TocLine(
                        lineNumber,
                        TocLineKind.Metadata,
                        rawText,
                        key,
                        value);
                }
            }

            return new TocLine(
                lineNumber,
                TocLineKind.Comment,
                rawText);
        }

        if (trimmed.StartsWith(
                '#'))
        {
            return new TocLine(
                lineNumber,
                TocLineKind.Comment,
                rawText);
        }

        return new TocLine(
            lineNumber,
            TocLineKind.File,
            rawText,
            Value: trimmed);
    }
}
