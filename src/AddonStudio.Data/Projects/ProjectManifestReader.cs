using System.Text.Json;
using System.Text.Json.Serialization;
using AddonStudio.Application.Projects;
using AddonStudio.Core.Projects;

namespace AddonStudio.Data.Projects;

public sealed class ProjectManifestReader : IProjectManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<ProjectManifest> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);

        var manifest = await JsonSerializer.DeserializeAsync<ProjectManifest>(
            stream,
            SerializerOptions,
            cancellationToken);

        return manifest ?? throw new InvalidDataException(
            $"Project manifest '{path}' does not contain a valid JSON object.");
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }
}
