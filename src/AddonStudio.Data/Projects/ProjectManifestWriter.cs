using System.Text.Json;
using System.Text.Json.Serialization;
using AddonStudio.Application.Projects;
using AddonStudio.Core.Projects;

namespace AddonStudio.Data.Projects;

public sealed class ProjectManifestWriter : IProjectManifestWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    public async Task WriteAsync(
        string path,
        ProjectManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = ProjectManifestValidator.Validate(manifest);
        var errors = issues
            .Where(issue => issue.Severity == ProjectValidationSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidDataException(
                string.Join(
                    Environment.NewLine,
                    errors.Select(error => $"{error.Code}: {error.Message}")));
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            manifest,
            SerializerOptions,
            cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }
}
