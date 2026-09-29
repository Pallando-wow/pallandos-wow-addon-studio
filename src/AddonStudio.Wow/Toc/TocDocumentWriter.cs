using System.Text;

namespace AddonStudio.Wow.Toc;

public sealed class TocDocumentWriter
{
    private readonly TocDocumentReader reader = new();

    public async Task ApplyMetadataAsync(
        string tocPath,
        IReadOnlyDictionary<string, string?> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tocPath);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return;
        }

        foreach (var change in changes)
        {
            ValidateMetadataChange(
                change.Key,
                change.Value);
        }

        var fullPath = Path.GetFullPath(tocPath);
        var sourceText = await File.ReadAllTextAsync(
            fullPath,
            cancellationToken);
        var document = await reader.ReadAsync(
            fullPath,
            cancellationToken);

        var lines = document.Lines
            .Select(line => line.RawText)
            .ToList();

        foreach (var change in changes)
        {
            ApplyChange(
                lines,
                change.Key,
                change.Value);
        }

        var newLine = DetectNewLine(sourceText);
        var endsWithNewLine = EndsWithNewLine(sourceText);
        var output = string.Join(
            newLine,
            lines);

        if (endsWithNewLine &&
            lines.Count > 0)
        {
            output += newLine;
        }

        await File.WriteAllTextAsync(
            fullPath,
            output,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    public Task SetMetadataAsync(
        string tocPath,
        string key,
        string value,
        CancellationToken cancellationToken = default) =>
        ApplyMetadataAsync(
            tocPath,
            new Dictionary<string, string?>
            {
                [key] = value
            },
            cancellationToken);

    public Task RemoveMetadataAsync(
        string tocPath,
        string key,
        CancellationToken cancellationToken = default) =>
        ApplyMetadataAsync(
            tocPath,
            new Dictionary<string, string?>
            {
                [key] = null
            },
            cancellationToken);

    private static void ApplyChange(
        List<string> lines,
        string key,
        string? value)
    {
        var matchingIndexes = lines
            .Select(
                (line, index) => new
                {
                    Index = index,
                    Metadata = TryParseMetadata(line)
                })
            .Where(item =>
                item.Metadata is not null &&
                string.Equals(
                    item.Metadata.Value.Key,
                    key,
                    StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Index)
            .ToArray();

        if (value is null)
        {
            for (var index = matchingIndexes.Length - 1;
                 index >= 0;
                 index--)
            {
                lines.RemoveAt(
                    matchingIndexes[index]);
            }

            return;
        }

        if (matchingIndexes.Length > 0)
        {
            var targetIndex =
                matchingIndexes[^1];
            var targetMetadata =
                TryParseMetadata(
                    lines[targetIndex])!.Value;
            var leadingWhitespace =
                GetLeadingWhitespace(
                    lines[targetIndex]);

            lines[targetIndex] =
                $"{leadingWhitespace}## " +
                $"{targetMetadata.Key}: {value}";

            for (var index = matchingIndexes.Length - 2;
                 index >= 0;
                 index--)
            {
                lines.RemoveAt(
                    matchingIndexes[index]);
            }

            return;
        }

        var insertionIndex =
            FindMetadataInsertionIndex(lines);

        lines.Insert(
            insertionIndex,
            $"## {key}: {value}");
    }

    private static int FindMetadataInsertionIndex(
        IReadOnlyList<string> lines)
    {
        var lastMetadataIndex = -1;

        for (var index = 0;
             index < lines.Count;
             index++)
        {
            if (TryParseMetadata(lines[index]) is not null)
            {
                lastMetadataIndex = index;
            }
        }

        if (lastMetadataIndex >= 0)
        {
            return lastMetadataIndex + 1;
        }

        for (var index = 0;
             index < lines.Count;
             index++)
        {
            var trimmed = lines[index].Trim();

            if (trimmed.Length > 0 &&
                !trimmed.StartsWith(
                    '#'))
            {
                return index;
            }
        }

        return lines.Count;
    }

    private static (string Key, string Value)? TryParseMetadata(
        string rawText)
    {
        var trimmed = rawText.Trim();

        if (!trimmed.StartsWith(
                "##",
                StringComparison.Ordinal))
        {
            return null;
        }

        var body = trimmed[2..].TrimStart();
        var separatorIndex = body.IndexOf(':');

        if (separatorIndex <= 0)
        {
            return null;
        }

        var key = body[..separatorIndex].Trim();

        if (key.Length == 0)
        {
            return null;
        }

        return (
            key,
            body[(separatorIndex + 1)..].Trim());
    }

    private static string GetLeadingWhitespace(
        string value)
    {
        var length = 0;

        while (length < value.Length &&
               char.IsWhiteSpace(value[length]))
        {
            length++;
        }

        return value[..length];
    }

    private static void ValidateMetadataChange(
        string key,
        string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (key.Contains(
                ':',
                StringComparison.Ordinal) ||
            ContainsLineBreak(key))
        {
            throw new ArgumentException(
                "TOC metadata keys cannot contain ':' or line breaks.",
                nameof(key));
        }

        if (value is not null &&
            ContainsLineBreak(value))
        {
            throw new ArgumentException(
                "TOC metadata values cannot contain line breaks.",
                nameof(value));
        }
    }

    private static bool ContainsLineBreak(
        string value) =>
        value.Contains('\r') ||
        value.Contains('\n');

    private static string DetectNewLine(
        string sourceText)
    {
        var crlf = sourceText.IndexOf(
            "\r\n",
            StringComparison.Ordinal);

        if (crlf >= 0)
        {
            return "\r\n";
        }

        if (sourceText.Contains('\n'))
        {
            return "\n";
        }

        if (sourceText.Contains('\r'))
        {
            return "\r";
        }

        return Environment.NewLine;
    }

    private static bool EndsWithNewLine(
        string value) =>
        value.EndsWith(
            "\r\n",
            StringComparison.Ordinal) ||
        value.EndsWith(
            '\n') ||
        value.EndsWith(
            '\r');
}
