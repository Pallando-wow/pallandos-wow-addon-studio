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
    public async Task LoadGameVersionCatalogAsync_JoinsTypesAndDetailedVersions()
    {
        var handler = new StubHandler(request =>
        {
            var path =
                request.RequestUri?.PathAndQuery ??
                string.Empty;

            if (path ==
                "/v1/games/1/version-types")
            {
                return Json(
                    """
                    {
                      "data": [
                        {
                          "id": 517,
                          "gameId": 1,
                          "name": "WoW Retail",
                          "slug": "wow_retail"
                        },
                        {
                          "id": 67408,
                          "gameId": 1,
                          "name": "WoW Classic",
                          "slug": "wow_classic"
                        }
                      ]
                    }
                    """);
            }

            if (path ==
                "/v2/games/1/versions")
            {
                return Json(
                    """
                    {
                      "data": [
                        {
                          "type": 517,
                          "versions": [
                            {
                              "id": 10001,
                              "slug": "11-2-5",
                              "name": "11.2.5"
                            }
                          ]
                        },
                        {
                          "type": 67408,
                          "versions": [
                            {
                              "id": 20001,
                              "slug": "1-15-9",
                              "name": "1.15.9"
                            }
                          ]
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

        var catalog =
            await client.LoadGameVersionCatalogAsync(
                "secret-key",
                1);

        Assert.Equal(
            2,
            catalog.Types.Count);
        Assert.Equal(
            2,
            catalog.Versions.Count);

        var classic =
            Assert.Single(
                catalog.Versions,
                version =>
                    version.Name ==
                    "1.15.9");

        Assert.Equal(
            20001,
            classic.Id);
        Assert.Equal(
            67408,
            classic.TypeId);
        Assert.Equal(
            "WoW Classic",
            classic.TypeName);
        Assert.Equal(
            "wow_classic",
            classic.TypeSlug);
    }

    [Fact]
    public async Task GetProjectAsync_LoadsProjectById()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(
                "/v1/mods/12345",
                request.RequestUri?.PathAndQuery);

            return Json(
                """
                {
                  "data": {
                    "id": 12345,
                    "gameId": 1,
                    "name": "ForeverBag",
                    "slug": "foreverbag",
                    "summary": "Bag addon",
                    "status": 4,
                    "primaryCategoryId": 101,
                    "categories": [
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
                }
                """);
        });

        var client =
            new CurseForgeDataSourceClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://api.curseforge.com/")
                });

        var project =
            await client.GetProjectAsync(
                "secret-key",
                12345);

        Assert.Equal(
            12345,
            project.Id);
        Assert.Equal(
            "ForeverBag",
            project.Name);
        Assert.Equal(
            "foreverbag",
            project.Slug);
        Assert.Equal(
            "Approved",
            project.StatusName);
        Assert.Equal(
            101,
            project.PrimaryCategoryId);
        Assert.Equal(
            "Bags & Inventory",
            project.PrimaryCategory?.Name);
        Assert.Equal(
            "bags-inventory",
            project.PrimaryCategory?.Slug);
        Assert.Contains(
            101,
            project.CategoryIds);
    }

    [Fact]
    public async Task FindProjectBySlugAsync_UsesExactSlugMatch()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(
                "/v1/mods/search?gameId=1&slug=foreverbag&pageSize=50",
                request.RequestUri?.PathAndQuery);

            return Json(
                """
                {
                  "data": [
                    {
                      "id": 12345,
                      "gameId": 1,
                      "name": "ForeverBag",
                      "slug": "foreverbag",
                      "summary": "Bag addon",
                      "status": 4,
                      "primaryCategoryId": 101,
                      "categories": []
                    }
                  ],
                  "pagination": {
                    "index": 0,
                    "pageSize": 50,
                    "resultCount": 1,
                    "totalCount": 1
                  }
                }
                """);
        });

        var client =
            new CurseForgeDataSourceClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://api.curseforge.com/")
                });

        var project =
            await client.FindProjectBySlugAsync(
                "secret-key",
                1,
                "foreverbag");

        Assert.NotNull(project);
        Assert.Equal(
            12345,
            project.Id);
        Assert.Equal(
            "foreverbag",
            project.Slug);
    }

    [Fact]
    public async Task FindProjectBySlugAsync_ReturnsNullWhenNoExactSlugExists()
    {
        var handler = new StubHandler(_ =>
            Json(
                """
                {
                  "data": [
                    {
                      "id": 999,
                      "gameId": 1,
                      "name": "Forever Bag Tools",
                      "slug": "forever-bag-tools",
                      "summary": "",
                      "status": 4,
                      "primaryCategoryId": 101,
                      "categories": []
                    }
                  ],
                  "pagination": {
                    "index": 0,
                    "pageSize": 50,
                    "resultCount": 1,
                    "totalCount": 1
                  }
                }
                """));

        var client =
            new CurseForgeDataSourceClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://api.curseforge.com/")
                });

        var project =
            await client.FindProjectBySlugAsync(
                "secret-key",
                1,
                "foreverbag");

        Assert.Null(project);
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
