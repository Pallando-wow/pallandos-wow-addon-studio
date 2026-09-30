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
