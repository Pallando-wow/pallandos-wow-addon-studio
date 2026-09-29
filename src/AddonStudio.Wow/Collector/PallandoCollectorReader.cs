using System.Globalization;
using System.Text;
using AddonStudio.Application.WowData;
using AddonStudio.Core.Wow.Collector;

namespace AddonStudio.Wow.Collector;

public sealed class PallandoCollectorReader : IPallandoCollectorReader
{
    private const long MaxFileSize = 32L * 1024L * 1024L;
    private const int SupportedStorageSchemaVersion = 1;

    public async Task<PallandoCollectorSnapshot> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Select a PallandoDataCollector SavedVariables file.",
                nameof(filePath));
        }

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Collector SavedVariables file was not found.",
                fullPath);
        }

        var fileInfo = new FileInfo(fullPath);

        if (fileInfo.Length > MaxFileSize)
        {
            throw new InvalidDataException(
                "Collector SavedVariables file is larger than the 32 MB import limit.");
        }

        var text = await File.ReadAllTextAsync(
            fullPath,
            Encoding.UTF8,
            cancellationToken);

        LuaTable root;

        try
        {
            var parser = new LuaSavedVariablesParser(text);
            var assignment = parser.ParseAssignment();

            if (!string.Equals(
                    assignment.VariableName,
                    "PallandoDataCollectorDB",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The file does not contain PallandoDataCollectorDB.");
            }

            root = assignment.Table;
        }
        catch (LuaParseException exception)
        {
            throw new InvalidDataException(
                $"Collector SavedVariables could not be parsed: {exception.Message}",
                exception);
        }

        var storageSchemaVersion =
            RequireInt(root, "schemaVersion");

        if (storageSchemaVersion != SupportedStorageSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported collector storage schema version {storageSchemaVersion}.");
        }

        var collector = RequireTable(root, "collector");

        if (!string.Equals(
                RequireString(collector, "name"),
                "PallandoDataCollector",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The SavedVariables file was not produced by PallandoDataCollector.");
        }

        var collectorVersion =
            RequireString(collector, "version");
        var exportSchemaVersion =
            RequireInt(collector, "exportSchemaVersion");

        var clientTable = RequireTable(root, "client");

        var client = new PallandoCollectorClient(
            RequireString(clientTable, "id"),
            RequireString(clientTable, "version"),
            RequirePositiveInt(clientTable, "build"),
            RequirePositiveInt(clientTable, "interface"));

        var locale = GetString(root, "locale") ?? string.Empty;
        var stats = GetTable(root, "stats");
        var sessions = GetInt(stats, "sessions") ?? 0;
        var totalObservations =
            GetInt(stats, "totalObservations") ?? 0;
        var observations =
            RequireTable(root, "observations");

        return new PallandoCollectorSnapshot(
            fullPath,
            storageSchemaVersion,
            collectorVersion,
            exportSchemaVersion,
            locale,
            client,
            sessions,
            totalObservations,
            ReadApis(observations),
            ReadEvents(observations),
            ReadMaps(observations));
    }

    private static IReadOnlyList<PallandoCollectorApiObservation>
        ReadApis(LuaTable observations)
    {
        var bucket = GetTable(observations, "api");

        if (bucket is null)
        {
            return [];
        }

        var result =
            new List<PallandoCollectorApiObservation>();

        foreach (var value in bucket.Values)
        {
            if (value is not LuaTable record)
            {
                continue;
            }

            var key = GetEntityKey(record);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var facts = GetTable(record, "facts");

            result.Add(
                new PallandoCollectorApiObservation(
                    key,
                    GetBool(facts, "available") ?? false,
                    GetString(facts, "valueType") ?? string.Empty,
                    GetInt(record, "observationCount") ?? 0,
                    ReadBuilds(record)));
        }

        return result
            .OrderBy(
                item => item.Key,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PallandoCollectorEventObservation>
        ReadEvents(LuaTable observations)
    {
        var bucket = GetTable(observations, "event");

        if (bucket is null)
        {
            return [];
        }

        var result =
            new List<PallandoCollectorEventObservation>();

        foreach (var value in bucket.Values)
        {
            if (value is not LuaTable record)
            {
                continue;
            }

            var key = GetEntityKey(record);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var facts = GetTable(record, "facts");

            result.Add(
                new PallandoCollectorEventObservation(
                    key,
                    GetBool(facts, "supported") ?? false,
                    GetBool(facts, "observed") ?? false,
                    GetInt(record, "observationCount") ?? 0,
                    ReadBuilds(record)));
        }

        return result
            .OrderBy(
                item => item.Key,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PallandoCollectorMapObservation>
        ReadMaps(LuaTable observations)
    {
        var bucket = GetTable(observations, "map");

        if (bucket is null)
        {
            return [];
        }

        var result =
            new List<PallandoCollectorMapObservation>();

        foreach (var value in bucket.Values)
        {
            if (value is not LuaTable record)
            {
                continue;
            }

            var id = GetEntityId(record);

            if (id is null or <= 0)
            {
                continue;
            }

            var facts = GetTable(record, "facts");

            result.Add(
                new PallandoCollectorMapObservation(
                    id.Value,
                    GetString(facts, "name") ?? string.Empty,
                    GetInt(facts, "mapType"),
                    GetInt(facts, "parentMapId"),
                    GetInt(facts, "flags"),
                    GetInt(facts, "playerMinLevel"),
                    GetInt(facts, "playerMaxLevel"),
                    GetInt(facts, "petMinLevel"),
                    GetInt(facts, "petMaxLevel"),
                    GetInt(facts, "mapArtId"),
                    GetDouble(facts, "worldWidth"),
                    GetDouble(facts, "worldHeight"),
                    GetInt(record, "observationCount") ?? 0,
                    ReadBuilds(record)));
        }

        return result
            .OrderBy(item => item.Id)
            .ToArray();
    }

    private static IReadOnlyList<int> ReadBuilds(
        LuaTable record)
    {
        var builds = GetTable(record, "builds");

        if (builds is null)
        {
            return [];
        }

        return builds
            .Fields
            .Where(pair => pair.Value is true)
            .Select(pair =>
                int.TryParse(
                    pair.Key,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var build)
                    ? build
                    : 0)
            .Where(build => build > 0)
            .Distinct()
            .Order()
            .ToArray();
    }

    private static string? GetEntityKey(
        LuaTable record) =>
        GetString(
            GetTable(record, "entity"),
            "key");

    private static int? GetEntityId(
        LuaTable record) =>
        GetInt(
            GetTable(record, "entity"),
            "id");

    private static LuaTable RequireTable(
        LuaTable table,
        string name) =>
        GetTable(table, name) ??
        throw new InvalidDataException(
            $"Collector field '{name}' is missing or invalid.");

    private static LuaTable? GetTable(
        LuaTable? table,
        string name) =>
        table is not null &&
        table.Fields.TryGetValue(name, out var value)
            ? value as LuaTable
            : null;

    private static string RequireString(
        LuaTable table,
        string name) =>
        GetString(table, name) ??
        throw new InvalidDataException(
            $"Collector field '{name}' is missing or invalid.");

    private static string? GetString(
        LuaTable? table,
        string name) =>
        table is not null &&
        table.Fields.TryGetValue(name, out var value)
            ? value as string
            : null;

    private static int RequireInt(
        LuaTable table,
        string name) =>
        GetInt(table, name) ??
        throw new InvalidDataException(
            $"Collector field '{name}' is missing or invalid.");

    private static int RequirePositiveInt(
        LuaTable table,
        string name)
    {
        var value = RequireInt(table, name);

        if (value <= 0)
        {
            throw new InvalidDataException(
                $"Collector field '{name}' must be greater than zero.");
        }

        return value;
    }

    private static int? GetInt(
        LuaTable? table,
        string name)
    {
        var number = GetDouble(table, name);

        if (number is null ||
            number < int.MinValue ||
            number > int.MaxValue ||
            Math.Truncate(number.Value) != number.Value)
        {
            return null;
        }

        return (int)number.Value;
    }

    private static double? GetDouble(
        LuaTable? table,
        string name) =>
        table is not null &&
        table.Fields.TryGetValue(name, out var value) &&
        value is double number
            ? number
            : null;

    private static bool? GetBool(
        LuaTable? table,
        string name) =>
        table is not null &&
        table.Fields.TryGetValue(name, out var value) &&
        value is bool boolean
            ? boolean
            : null;

    private sealed class LuaTable
    {
        public Dictionary<string, object?> Fields { get; } =
            new(StringComparer.Ordinal);

        public IEnumerable<object?> Values =>
            Fields.Values;
    }

    private sealed class LuaSavedVariablesParser(string text)
    {
        private int index;

        public (string VariableName, LuaTable Table)
            ParseAssignment()
        {
            SkipTrivia();

            var variableName =
                ReadIdentifier();

            SkipTrivia();
            Expect('=');

            var value = ParseValue();

            if (value is not LuaTable table)
            {
                throw Error(
                    "Root assignment must contain a table.");
            }

            SkipTrivia();

            if (!IsEnd)
            {
                throw Error(
                    "Unexpected content after root table.");
            }

            return (variableName, table);
        }

        private object? ParseValue()
        {
            SkipTrivia();

            if (IsEnd)
            {
                throw Error("Unexpected end of file.");
            }

            var current = text[index];

            if (current == '{')
            {
                return ParseTable();
            }

            if (current is '"' or '\'')
            {
                return ParseString();
            }

            if (current == '-' ||
                char.IsDigit(current))
            {
                return ParseNumber();
            }

            if (IsIdentifierStart(current))
            {
                var identifier = ReadIdentifier();

                return identifier switch
                {
                    "true" => true,
                    "false" => false,
                    "nil" => null,
                    _ => throw Error(
                        $"Unsupported Lua value '{identifier}'.")
                };
            }

            throw Error(
                $"Unsupported Lua token '{current}'.");
        }

        private LuaTable ParseTable()
        {
            Expect('{');

            var table = new LuaTable();
            var sequentialIndex = 1;

            while (true)
            {
                SkipTrivia();

                if (TryConsume('}'))
                {
                    return table;
                }

                string key;
                object? value;

                if (TryConsume('['))
                {
                    var keyValue = ParseValue();

                    SkipTrivia();
                    Expect(']');
                    SkipTrivia();
                    Expect('=');

                    key = ConvertKey(keyValue);
                    value = ParseValue();
                }
                else if (!IsEnd &&
                         IsIdentifierStart(text[index]))
                {
                    var savedIndex = index;
                    var identifier = ReadIdentifier();

                    SkipTrivia();

                    if (TryConsume('='))
                    {
                        key = identifier;
                        value = ParseValue();
                    }
                    else
                    {
                        index = savedIndex;
                        key = sequentialIndex
                            .ToString(
                                CultureInfo.InvariantCulture);
                        sequentialIndex++;
                        value = ParseValue();
                    }
                }
                else
                {
                    key = sequentialIndex
                        .ToString(
                            CultureInfo.InvariantCulture);
                    sequentialIndex++;
                    value = ParseValue();
                }

                table.Fields[key] = value;

                SkipTrivia();

                if (TryConsume(',') ||
                    TryConsume(';'))
                {
                    continue;
                }

                if (!IsEnd && text[index] == '}')
                {
                    continue;
                }

                throw Error(
                    "Expected ',', ';' or '}' in table.");
            }
        }

        private string ParseString()
        {
            var quote = text[index++];
            var builder = new StringBuilder();

            while (!IsEnd)
            {
                var current = text[index++];

                if (current == quote)
                {
                    return builder.ToString();
                }

                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (IsEnd)
                {
                    throw Error(
                        "Unterminated string escape.");
                }

                var escaped = text[index++];

                switch (escaped)
                {
                    case 'a':
                        builder.Append('\a');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'v':
                        builder.Append('\v');
                        break;
                    case '\\':
                    case '"':
                    case '\'':
                        builder.Append(escaped);
                        break;
                    case 'x':
                        builder.Append(
                            (char)ReadHexEscape());
                        break;
                    default:
                        if (char.IsDigit(escaped))
                        {
                            builder.Append(
                                (char)ReadDecimalEscape(
                                    escaped));
                        }
                        else
                        {
                            builder.Append(escaped);
                        }
                        break;
                }
            }

            throw Error("Unterminated string.");
        }

        private int ReadHexEscape()
        {
            if (index + 2 > text.Length)
            {
                throw Error(
                    "Incomplete hexadecimal string escape.");
            }

            var value = text.Substring(index, 2);

            if (!int.TryParse(
                    value,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                throw Error(
                    "Invalid hexadecimal string escape.");
            }

            index += 2;
            return parsed;
        }

        private int ReadDecimalEscape(char first)
        {
            var digits = new StringBuilder();
            digits.Append(first);

            while (digits.Length < 3 &&
                   !IsEnd &&
                   char.IsDigit(text[index]))
            {
                digits.Append(text[index++]);
            }

            if (!int.TryParse(
                    digits.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var value) ||
                value > 255)
            {
                throw Error(
                    "Invalid decimal string escape.");
            }

            return value;
        }

        private double ParseNumber()
        {
            var start = index;

            if (text[index] == '-')
            {
                index++;
            }

            while (!IsEnd &&
                   char.IsDigit(text[index]))
            {
                index++;
            }

            if (!IsEnd &&
                text[index] == '.')
            {
                index++;

                while (!IsEnd &&
                       char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            if (!IsEnd &&
                (text[index] == 'e' ||
                 text[index] == 'E'))
            {
                index++;

                if (!IsEnd &&
                    (text[index] == '+' ||
                     text[index] == '-'))
                {
                    index++;
                }

                while (!IsEnd &&
                       char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            var token = text[start..index];

            if (!double.TryParse(
                    token,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                throw Error(
                    $"Invalid number '{token}'.");
            }

            return value;
        }

        private string ReadIdentifier()
        {
            SkipTrivia();

            if (IsEnd ||
                !IsIdentifierStart(text[index]))
            {
                throw Error("Expected identifier.");
            }

            var start = index++;

            while (!IsEnd &&
                   IsIdentifierPart(text[index]))
            {
                index++;
            }

            return text[start..index];
        }

        private void SkipTrivia()
        {
            while (!IsEnd)
            {
                if (char.IsWhiteSpace(text[index]) ||
                    text[index] == '\uFEFF')
                {
                    index++;
                    continue;
                }

                if (index + 1 >= text.Length ||
                    text[index] != '-' ||
                    text[index + 1] != '-')
                {
                    return;
                }

                index += 2;

                if (index + 1 < text.Length &&
                    text[index] == '[' &&
                    text[index + 1] == '[')
                {
                    index += 2;
                    var end =
                        text.IndexOf(
                            "]]",
                            index,
                            StringComparison.Ordinal);

                    if (end < 0)
                    {
                        throw Error(
                            "Unterminated block comment.");
                    }

                    index = end + 2;
                    continue;
                }

                while (!IsEnd &&
                       text[index] != '\n')
                {
                    index++;
                }
            }
        }

        private bool TryConsume(char expected)
        {
            SkipTrivia();

            if (IsEnd ||
                text[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private void Expect(char expected)
        {
            SkipTrivia();

            if (IsEnd ||
                text[index] != expected)
            {
                throw Error(
                    $"Expected '{expected}'.");
            }

            index++;
        }

        private string ConvertKey(object? value) =>
            value switch
            {
                string stringValue => stringValue,
                double number
                    when Math.Truncate(number) == number =>
                    ((long)number).ToString(
                        CultureInfo.InvariantCulture),
                double number =>
                    number.ToString(
                        "R",
                        CultureInfo.InvariantCulture),
                _ => throw Error(
                    "Table keys must be strings or numbers.")
            };

        private LuaParseException Error(
            string message) =>
            new(
                $"{message} (position {index}).");

        private bool IsEnd =>
            index >= text.Length;

        private static bool IsIdentifierStart(
            char value) =>
            char.IsLetter(value) ||
            value == '_';

        private static bool IsIdentifierPart(
            char value) =>
            char.IsLetterOrDigit(value) ||
            value == '_';
    }

    private sealed class LuaParseException(
        string message) : Exception(message);
}
