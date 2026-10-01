using System.Net.Http.Headers;
using System.Text.Json;

namespace AddonStudio.Platforms.CurseForge;

public sealed class CurseForgeDataSourceClient
{
    private const string ApiBaseAddress =
        "https://api.curseforge.com/";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient httpClient;

    public CurseForgeDataSourceClient(
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        this.httpClient = httpClient;

        if (this.httpClient.BaseAddress is null)
        {
            this.httpClient.BaseAddress =
                new Uri(ApiBaseAddress);
        }
    }

    public async Task<CurseForgeDataSnapshot>
        LoadWorldOfWarcraftAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var game = await FindWorldOfWarcraftAsync(
            apiKey.Trim(),
            cancellationToken);

        var categoriesResponse =
            await GetAsync<ApiResponse<CategoryDto>>(
                $"v1/categories?gameId={game.Id}",
                apiKey.Trim(),
                cancellationToken);

        var categories =
            categoriesResponse.Data
                .Select(category =>
                    new CurseForgeCategory(
                        category.Id,
                        category.GameId,
                        category.Name ?? string.Empty,
                        category.Slug ?? string.Empty,
                        category.IsClass,
                        category.ClassId,
                        category.ParentCategoryId,
                        category.DisplayIndex))
                .OrderBy(category =>
                    category.DisplayIndex)
                .ThenBy(category =>
                    category.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new CurseForgeDataSnapshot(
            game,
            categories);
    }

    public async Task<CurseForgeGameVersionCatalog>
        LoadGameVersionCatalogAsync(
            string apiKey,
            int gameId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        if (gameId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gameId));
        }

        var normalizedApiKey =
            apiKey.Trim();

        var typeResponse =
            await GetAsync<
                ApiResponse<GameVersionTypeDto>>(
                $"v1/games/{gameId}/version-types",
                normalizedApiKey,
                cancellationToken);

        var versionsResponse =
            await GetAsync<
                GameVersionsDetailedResponse>(
                $"v2/games/{gameId}/versions",
                normalizedApiKey,
                cancellationToken);

        var types =
            typeResponse.Data
                .Select(type =>
                    new CurseForgeGameVersionType(
                        type.Id,
                        type.GameId,
                        type.Name ?? string.Empty,
                        type.Slug ?? string.Empty))
                .OrderBy(type =>
                    type.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var typeLookup =
            types.ToDictionary(
                type => type.Id);

        var versions =
            versionsResponse.Data
                .SelectMany(group =>
                    group.Versions.Select(version =>
                    {
                        typeLookup.TryGetValue(
                            group.Type,
                            out var type);

                        return new CurseForgeGameVersion(
                            version.Id,
                            version.Name ?? string.Empty,
                            version.Slug ?? string.Empty,
                            group.Type,
                            type?.Name ?? $"Type {group.Type}",
                            type?.Slug ?? string.Empty);
                    }))
                .OrderBy(version =>
                    version.TypeName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(version =>
                    version.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new CurseForgeGameVersionCatalog(
            types,
            versions);
    }

    public async Task<CurseForgeProject> GetProjectAsync(
        string apiKey,
        int projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        if (projectId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectId));
        }

        var response =
            await GetAsync<SingleApiResponse<ModDto>>(
                $"v1/mods/{projectId}",
                apiKey.Trim(),
                cancellationToken);

        return MapProject(
            response.Data);
    }

    public async Task<CurseForgeProject?> FindProjectBySlugAsync(
        string apiKey,
        int gameId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (gameId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gameId));
        }

        var encodedSlug =
            Uri.EscapeDataString(
                slug.Trim());

        var response =
            await GetAsync<ModSearchResponse>(
                $"v1/mods/search?gameId={gameId}&slug={encodedSlug}&pageSize=50",
                apiKey.Trim(),
                cancellationToken);

        var match =
            response.Data.FirstOrDefault(mod =>
                string.Equals(
                    mod.Slug,
                    slug.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        return match is null
            ? null
            : MapProject(match);
    }

    private static CurseForgeProject MapProject(
        ModDto mod) =>
        new(
            mod.Id,
            mod.GameId,
            mod.Name ?? string.Empty,
            mod.Slug ?? string.Empty,
            mod.Summary ?? string.Empty,
            mod.Status,
            mod.PrimaryCategoryId,
            mod.Categories
                .Select(category =>
                    new CurseForgeProjectCategory(
                        category.Id,
                        category.Name ?? string.Empty,
                        category.Slug ?? string.Empty,
                        category.ClassId,
                        category.ParentCategoryId))
                .DistinctBy(category =>
                    category.Id)
                .ToArray());

    private async Task<CurseForgeGame>
        FindWorldOfWarcraftAsync(
            string apiKey,
            CancellationToken cancellationToken)
    {
        const int pageSize = 50;

        for (var index = 0;
             index < 10_000;
             index += pageSize)
        {
            var response =
                await GetAsync<GameListResponse>(
                    $"v1/games?index={index}&pageSize={pageSize}",
                    apiKey,
                    cancellationToken);

            var match =
                response.Data.FirstOrDefault(game =>
                    string.Equals(
                        game.Slug,
                        "world-of-warcraft",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        game.Name,
                        "World of Warcraft",
                        StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return new CurseForgeGame(
                    match.Id,
                    match.Name ?? "World of Warcraft",
                    match.Slug ?? "world-of-warcraft");
            }

            if (response.Pagination is null ||
                index + response.Data.Count >=
                response.Pagination.TotalCount)
            {
                break;
            }
        }

        throw new InvalidDataException(
            "World of Warcraft is not available to this CurseForge API key.");
    }

    private async Task<T> GetAsync<T>(
        string relativeUrl,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                relativeUrl);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        request.Headers.TryAddWithoutValidation(
            "x-api-key",
            apiKey);

        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"CurseForge API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                inner: null,
                response.StatusCode);
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        var result =
            await JsonSerializer.DeserializeAsync<T>(
                stream,
                SerializerOptions,
                cancellationToken);

        return result ??
            throw new InvalidDataException(
                "CurseForge API returned an empty or invalid response.");
    }

    private sealed class GameListResponse
    {
        public List<GameDto> Data { get; init; } = [];

        public PaginationDto? Pagination { get; init; }
    }

    private sealed class ApiResponse<T>
    {
        public List<T> Data { get; init; } = [];
    }

    private sealed class SingleApiResponse<T>
    {
        public required T Data { get; init; }
    }

    private sealed class ModSearchResponse
    {
        public List<ModDto> Data { get; init; } = [];

        public PaginationDto? Pagination { get; init; }
    }

    private sealed class ModDto
    {
        public int Id { get; init; }

        public int GameId { get; init; }

        public string? Name { get; init; }

        public string? Slug { get; init; }

        public string? Summary { get; init; }

        public int Status { get; init; }

        public int PrimaryCategoryId { get; init; }

        public List<CategoryDto> Categories { get; init; } = [];
    }

    private sealed class GameVersionTypeDto
    {
        public int Id { get; init; }

        public int GameId { get; init; }

        public string? Name { get; init; }

        public string? Slug { get; init; }
    }

    private sealed class GameVersionsDetailedResponse
    {
        public List<GameVersionsDetailedGroupDto>
            Data { get; init; } = [];
    }

    private sealed class GameVersionsDetailedGroupDto
    {
        public int Type { get; init; }

        public List<GameVersionDetailedDto>
            Versions { get; init; } = [];
    }

    private sealed class GameVersionDetailedDto
    {
        public int Id { get; init; }

        public string? Slug { get; init; }

        public string? Name { get; init; }
    }

    private sealed class GameDto
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public string? Slug { get; init; }
    }

    private sealed class CategoryDto
    {
        public int Id { get; init; }

        public int GameId { get; init; }

        public string? Name { get; init; }

        public string? Slug { get; init; }

        public bool IsClass { get; init; }

        public int? ClassId { get; init; }

        public int? ParentCategoryId { get; init; }

        public int DisplayIndex { get; init; }
    }

    private sealed class PaginationDto
    {
        public int TotalCount { get; init; }
    }
}
