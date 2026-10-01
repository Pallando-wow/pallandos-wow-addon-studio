using System.Net;
using System.Text;
using System.Text.Json;
using AddonStudio.Platforms.CurseForge;

namespace AddonStudio.Tests;

public sealed class CurseForgeUploadApiClientTests
{
    [Fact]
    public async Task GetGameVersionsAsync_UsesUploadTokenAndWowEndpoint()
    {
        var handler =
            new AsyncStubHandler(
                (request, _) =>
                {
                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);
                    Assert.Equal(
                        "/api/game/versions",
                        request.RequestUri?.AbsolutePath);

                    Assert.True(
                        request.Headers.TryGetValues(
                            "X-Api-Token",
                            out var tokens));
                    Assert.Equal(
                        "upload-token",
                        Assert.Single(tokens));

                    return Task.FromResult(
                        Json(
                            """
                            [
                              {
                                "id": 12919,
                                "gameVersionTypeID": 67408,
                                "name": "1.15.7",
                                "slug": "1-15-7"
                              },
                              {
                                "id": 13001,
                                "gameVersionTypeID": 67408,
                                "name": "1.15.8",
                                "slug": "1-15-8"
                              }
                            ]
                            """));
                });

        var client =
            new CurseForgeUploadApiClient(
                new HttpClient(handler)
                {
                    BaseAddress =
                        new Uri(
                            "https://wow.curseforge.com/")
                });

        var versions =
            await client.GetGameVersionsAsync(
                "upload-token");

        Assert.Equal(
            2,
            versions.Count);
        Assert.Contains(
            versions,
            version =>
                version.Id == 12919 &&
                version.GameVersionTypeId == 67408 &&
                version.Name == "1.15.7");
    }

    [Fact]
    public async Task UploadFileAsync_SendsMultipartMetadataAndZip()
    {
        var packagePath =
            Path.Combine(
                Path.GetTempPath(),
                "AddonStudio.Tests",
                Guid.NewGuid().ToString("N"),
                "ForeverBag-1.1.0.zip");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                packagePath)!);

        await File.WriteAllBytesAsync(
            packagePath,
            [0x50, 0x4B, 0x03, 0x04]);

        try
        {
            var handler =
                new AsyncStubHandler(
                    async (request, cancellationToken) =>
                    {
                        Assert.Equal(
                            HttpMethod.Post,
                            request.Method);
                        Assert.Equal(
                            "/api/projects/1712846/upload-file",
                            request.RequestUri?.AbsolutePath);

                        Assert.True(
                            request.Headers.TryGetValues(
                                "X-Api-Token",
                                out var tokens));
                        Assert.Equal(
                            "upload-token",
                            Assert.Single(tokens));

                        var multipart =
                            Assert.IsType<
                                MultipartFormDataContent>(
                                request.Content);

                        var parts =
                            multipart.ToArray();

                        var metadataPart =
                            Assert.Single(
                                parts,
                                part =>
                                    string.Equals(
                                        part.Headers.ContentDisposition?
                                            .Name?
                                            .Trim('"'),
                                        "metadata",
                                        StringComparison.Ordinal));

                        var metadataJson =
                            await metadataPart.ReadAsStringAsync(
                                cancellationToken);

                        using var document =
                            JsonDocument.Parse(
                                metadataJson);

                        var root =
                            document.RootElement;

                        Assert.Equal(
                            "## Changes\n- Fixed something",
                            root.GetProperty(
                                    "Changelog")
                                .GetString());
                        Assert.Equal(
                            "markdown",
                            root.GetProperty(
                                    "ChangelogType")
                                .GetString());
                        Assert.Equal(
                            "ForeverBag 1.1.0",
                            root.GetProperty(
                                    "DisplayName")
                                .GetString());
                        Assert.Equal(
                            "release",
                            root.GetProperty(
                                    "ReleaseType")
                                .GetString());
                        Assert.False(
                            root.GetProperty(
                                    "IsMarkedForManualRelease")
                                .GetBoolean());

                        Assert.Equal(
                            [12919, 13001],
                            root.GetProperty(
                                    "GameVersions")
                                .EnumerateArray()
                                .Select(element =>
                                    element.GetInt32())
                                .ToArray());

                        var filePart =
                            Assert.Single(
                                parts,
                                part =>
                                    string.Equals(
                                        part.Headers.ContentDisposition?
                                            .Name?
                                            .Trim('"'),
                                        "file",
                                        StringComparison.Ordinal));

                        Assert.Equal(
                            "ForeverBag-1.1.0.zip",
                            filePart.Headers.ContentDisposition?
                                .FileName?
                                .Trim('"'));
                        Assert.Equal(
                            "application/zip",
                            filePart.Headers.ContentType?
                                .MediaType);

                        var bytes =
                            await filePart.ReadAsByteArrayAsync(
                                cancellationToken);

                        Assert.Equal(
                            [0x50, 0x4B, 0x03, 0x04],
                            bytes);

                        return Json(
                            """
                            {
                              "id": 20402
                            }
                            """);
                    });

            var client =
                new CurseForgeUploadApiClient(
                    new HttpClient(handler)
                    {
                        BaseAddress =
                            new Uri(
                                "https://wow.curseforge.com/")
                    });

            var result =
                await client.UploadFileAsync(
                    "upload-token",
                    new CurseForgeUploadPlan(
                        1712846,
                        packagePath,
                        "ForeverBag-1.1.0.zip",
                        "ForeverBag 1.1.0",
                        "## Changes\n- Fixed something",
                        CurseForgeChangelogMarkupType.Markdown,
                        [12919, 13001],
                        CurseForgeFileReleaseType.Release,
                        4,
                        false,
                        new string('a', 64)));

            Assert.Equal(
                20402,
                result.FileId);
        }
        finally
        {
            var directory =
                Path.GetDirectoryName(
                    packagePath)!;

            if (Directory.Exists(
                    directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadFileAsync_ReportsApiErrorBody()
    {
        var packagePath =
            Path.Combine(
                Path.GetTempPath(),
                "AddonStudio.Tests",
                Guid.NewGuid().ToString("N"),
                "ForeverBag.zip");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                packagePath)!);

        await File.WriteAllBytesAsync(
            packagePath,
            [1, 2, 3]);

        try
        {
            var handler =
                new AsyncStubHandler(
                    (_, _) =>
                        Task.FromResult(
                            new HttpResponseMessage(
                                HttpStatusCode.Forbidden)
                            {
                                Content =
                                    new StringContent(
                                        "invalid token",
                                        Encoding.UTF8,
                                        "text/plain")
                            }));

            var client =
                new CurseForgeUploadApiClient(
                    new HttpClient(handler)
                    {
                        BaseAddress =
                            new Uri(
                                "https://wow.curseforge.com/")
                    });

            var exception =
                await Assert.ThrowsAsync<
                    HttpRequestException>(
                    () => client.UploadFileAsync(
                        "bad-token",
                        new CurseForgeUploadPlan(
                            1712846,
                            packagePath,
                            "ForeverBag.zip",
                            "ForeverBag",
                            "Changes",
                            CurseForgeChangelogMarkupType.Markdown,
                            [12919],
                            CurseForgeFileReleaseType.Release,
                            3,
                            false,
                            new string('a', 64))));

            Assert.Equal(
                HttpStatusCode.Forbidden,
                exception.StatusCode);
            Assert.Contains(
                "invalid token",
                exception.Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            var directory =
                Path.GetDirectoryName(
                    packagePath)!;

            if (Directory.Exists(
                    directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void UploadWireValues_MatchUploadApiStrings()
    {
        Assert.Equal(
            "release",
            CurseForgeFileReleaseType.Release
                .ToWireValue());
        Assert.Equal(
            "beta",
            CurseForgeFileReleaseType.Beta
                .ToWireValue());
        Assert.Equal(
            "alpha",
            CurseForgeFileReleaseType.Alpha
                .ToWireValue());

        Assert.Equal(
            "text",
            CurseForgeChangelogMarkupType.Text
                .ToWireValue());
        Assert.Equal(
            "html",
            CurseForgeChangelogMarkupType.Html
                .ToWireValue());
        Assert.Equal(
            "markdown",
            CurseForgeChangelogMarkupType.Markdown
                .ToWireValue());
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

    private sealed class AsyncStubHandler(
        Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>>
            responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
            responseFactory(
                request,
                cancellationToken);
    }
}
