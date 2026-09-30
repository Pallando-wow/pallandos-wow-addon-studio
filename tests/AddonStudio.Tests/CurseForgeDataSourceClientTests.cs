using System.Net;
using System.Text;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeDataSourceClientTests
{
    [Fact]
    public async Task LoadWorldOfWarcraftAsync_LoadsGameAndCategories()
    {
        var handler = new StubHandler(request =>
        {
            Assert.True(
                request.Headers.TryGetValues(
                    "x-api-key",
                    out var apiKeys));
            Assert.Equal(
                "secret-key",
                Assert.Single(apiKeys));

            var path =
                request.RequestUri?.PathAndQuery ??
                string.Empty;

            if (path.StartsWith(
                    "/v1/games",
                    StringComparison.Ordinal))
            {
                return Json(
                    """
                    {
                      "data": [
                        {
                          "id": 1,
                          "name": "World of Warcraft",
                          "slug": "world-of-warcraft"
                        }
                      ],
                      "pagination": {
                        "totalCount": 1
                      }
                    }
                    """);
            }

            if (path ==
                "/v1/categories?gameId=1")
            {
                return Json(
                    """
                    {
                      "data": [
                        {
                          "id": 100,
                          "gameId": 1,
                          "name": "Addons",
                          "slug": "addons",
                          "isClass": true,
                          "classId": null,
                          "parentCategoryId": null,
                          "displayIndex": 0
                        },
                        {
                          "id": 101,
                          "gameId": 1,
                          "name": "Bags & Inventory",
                          "slug": "bags-inventory",
                          "isClass": false,
                          "classId": 100,
                          "parentCategoryId": null,
                          "displayIndex": 1
                        }
                      ]
                    }
                    """);
            }

            return new HttpResponseMessage(
                HttpStatusCode.NotFound);
        });

        var client =
            new CurseForgeDataSourceClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://api.curseforge.com/")
                });

        var snapshot =
            await client.LoadWorldOfWarcraftAsync(
                "secret-key");

        Assert.Equal(1, snapshot.Game.Id);
        Assert.Equal(
            "world-of-warcraft",
            snapshot.Game.Slug);
        Assert.Equal(2, snapshot.Categories.Count);
        Assert.Contains(
            snapshot.Categories,
            category =>
                category.Name ==
                "Bags & Inventory");
    }

    [Fact]
    public async Task LoadWorldOfWarcraftAsync_ReportsApiError()
    {
        var handler =
            new StubHandler(_ =>
                new HttpResponseMessage(
                    HttpStatusCode.Unauthorized));

        var client =
            new CurseForgeDataSourceClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://api.curseforge.com/")
                });

        var exception =
            await Assert.ThrowsAsync<HttpRequestException>(
                () =>
                    client.LoadWorldOfWarcraftAsync(
                        "invalid-key"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            exception.StatusCode);
    }

    private static HttpResponseMessage Json(
        string json) =>
        new(HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                responseFactory(request));
    }
}
