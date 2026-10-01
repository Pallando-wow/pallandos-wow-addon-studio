using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeUploadApiClient
{
    private const string DefaultBaseAddress =
        "https://wow.curseforge.com/";

    private static readonly JsonSerializerOptions
        SerializerOptions =
            new()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            };

    private readonly HttpClient httpClient;

    public CurseForgeUploadApiClient(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);

        this.httpClient =
            httpClient;

        if (this.httpClient.BaseAddress is null)
        {
            this.httpClient.BaseAddress =
                new Uri(
                    DefaultBaseAddress);
        }
    }

    public async Task<
        IReadOnlyList<CurseForgeUploadGameVersion>>
        GetGameVersionsAsync(
            string apiToken,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiToken);

        using var request =
            CreateRequest(
                HttpMethod.Get,
                "api/game/versions",
                apiToken);

        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        await EnsureSuccessAsync(
            response,
            cancellationToken);

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        var versions =
            await JsonSerializer.DeserializeAsync<
                List<GameVersionDto>>(
                stream,
                SerializerOptions,
                cancellationToken)
            ?? [];

        return versions
            .Select(version =>
                new CurseForgeUploadGameVersion(
                    version.Id,
                    version.GameVersionTypeId,
                    version.Name ?? string.Empty,
                    version.Slug ?? string.Empty))
            .OrderBy(
                version => version.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<CurseForgeUploadResult>
        UploadFileAsync(
            string apiToken,
            CurseForgeUploadPlan plan,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiToken);
        ArgumentNullException.ThrowIfNull(
            plan);

        if (!File.Exists(
                plan.PackagePath))
        {
            throw new FileNotFoundException(
                "CurseForge upload package does not exist.",
                plan.PackagePath);
        }

        await VerifyPlannedPackageAsync(
            plan,
            cancellationToken);

        var metadata =
            new UploadMetadata
            {
                Changelog =
                    plan.Changelog,
                ChangelogType =
                    plan.ChangelogType.ToWireValue(),
                DisplayName =
                    plan.DisplayName,
                GameVersions =
                    plan.GameVersionIds,
                ReleaseType =
                    plan.ReleaseType.ToWireValue(),
                IsMarkedForManualRelease =
                    plan.IsMarkedForManualRelease
            };

        var metadataJson =
            JsonSerializer.Serialize(
                metadata);

        using var request =
            CreateRequest(
                HttpMethod.Post,
                $"api/projects/{plan.ProjectId}/upload-file",
                apiToken);

        using var multipart =
            new MultipartFormDataContent();

        using var metadataContent =
            new StringContent(
                metadataJson,
                Encoding.UTF8,
                "application/json");

        multipart.Add(
            metadataContent,
            "metadata");

        await using var packageStream =
            new FileStream(
                plan.PackagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        using var fileContent =
            new StreamContent(
                packageStream);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/zip");

        multipart.Add(
            fileContent,
            "file",
            plan.FileName);

        request.Content =
            multipart;

        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        await EnsureSuccessAsync(
            response,
            cancellationToken);

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        var result =
            await JsonSerializer.DeserializeAsync<
                UploadResponse>(
                responseStream,
                SerializerOptions,
                cancellationToken)
            ?? throw new InvalidDataException(
                "CurseForge upload returned an empty response.");

        if (result.Id <= 0)
        {
            throw new InvalidDataException(
                "CurseForge upload returned an invalid file id.");
        }

        return new CurseForgeUploadResult(
            result.Id);
    }

    private static async Task VerifyPlannedPackageAsync(
        CurseForgeUploadPlan plan,
        CancellationToken cancellationToken)
    {
        var fileInfo =
            new FileInfo(
                plan.PackagePath);

        if (fileInfo.Length !=
            plan.FileLength)
        {
            throw new InvalidDataException(
                $"CurseForge upload package size changed after planning. Expected {plan.FileLength}, actual {fileInfo.Length}.");
        }

        await using var stream =
            new FileStream(
                plan.PackagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

        var hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        var sha256 =
            Convert.ToHexString(
                hash)
                .ToLowerInvariant();

        if (!string.Equals(
                sha256,
                plan.ArtifactSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "CurseForge upload package SHA-256 changed after planning.");
        }
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativeUrl,
        string apiToken)
    {
        var request =
            new HttpRequestMessage(
                method,
                relativeUrl);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        request.Headers.TryAddWithoutValidation(
            "X-Api-Token",
            apiToken.Trim());

        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details =
            response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(
                    cancellationToken);

        var suffix =
            string.IsNullOrWhiteSpace(
                details)
                ? string.Empty
                : $" Response: {details.Trim()}";

        throw new HttpRequestException(
            $"CurseForge Upload API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).{suffix}",
            inner: null,
            response.StatusCode);
    }

    private sealed class GameVersionDto
    {
        public int Id { get; init; }

        public int GameVersionTypeId { get; init; }

        public string? Name { get; init; }

        public string? Slug { get; init; }
    }

    private sealed class UploadMetadata
    {
        public required string Changelog { get; init; }

        public required string ChangelogType { get; init; }

        public required string DisplayName { get; init; }

        public required IReadOnlyList<int>
            GameVersions { get; init; }

        public required string ReleaseType { get; init; }

        public bool IsMarkedForManualRelease { get; init; }
    }

    private sealed class UploadResponse
    {
        public int Id { get; init; }
    }
}
