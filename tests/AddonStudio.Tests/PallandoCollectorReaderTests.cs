using AddonStudio.Wow.Collector;

namespace AddonStudio.Tests;

public sealed class PallandoCollectorReaderTests
{
    [Fact]
    public async Task ReadAsync_ParsesCollectorSummaryAndObservations()
    {
        var path = CreateCollectorFile(
            """
            PallandoDataCollectorDB = {
                ["locale"] = "enUS",
                ["observations"] = {
                    ["map"] = {
                        ["id:1433"] = {
                            ["builds"] = {
                                ["70058"] = true,
                            },
                            ["facts"] = {
                                ["mapType"] = 3,
                                ["worldWidth"] = 2170.833984375,
                                ["worldHeight"] = 1447.916015625,
                                ["name"] = "Redridge Mountains",
                                ["parentMapId"] = 1415,
                                ["mapArtId"] = 2121,
                            },
                            ["observationCount"] = 14,
                            ["entity"] = {
                                ["id"] = 1433,
                            },
                        },
                    },
                    ["event"] = {
                        ["key:ZONE_CHANGED"] = {
                            ["builds"] = {
                                ["70058"] = true,
                            },
                            ["facts"] = {
                                ["supported"] = true,
                                ["observed"] = true,
                            },
                            ["observationCount"] = 19,
                            ["entity"] = {
                                ["key"] = "ZONE_CHANGED",
                            },
                        },
                    },
                    ["api"] = {
                        ["key:GetSpellInfo"] = {
                            ["builds"] = {
                                ["70058"] = true,
                            },
                            ["facts"] = {
                                ["valueType"] = "nil",
                                ["available"] = false,
                            },
                            ["observationCount"] = 2,
                            ["entity"] = {
                                ["key"] = "GetSpellInfo",
                            },
                        },
                        ["key:C_Spell.GetSpellInfo"] = {
                            ["builds"] = {
                                ["70058"] = true,
                            },
                            ["facts"] = {
                                ["valueType"] = "function",
                                ["available"] = true,
                            },
                            ["observationCount"] = 2,
                            ["entity"] = {
                                ["key"] = "C_Spell.GetSpellInfo",
                            },
                        },
                    },
                },
                ["collector"] = {
                    ["name"] = "PallandoDataCollector",
                    ["version"] = "0.1.1",
                    ["exportSchemaVersion"] = 2,
                },
                ["client"] = {
                    ["id"] = "wow_forever",
                    ["interface"] = 16001,
                    ["version"] = "1.60.1",
                    ["build"] = 70058,
                },
                ["schemaVersion"] = 1,
                ["stats"] = {
                    ["sessions"] = 2,
                    ["totalObservations"] = 325,
                },
            }
            """);

        try
        {
            var reader = new PallandoCollectorReader();

            var result = await reader.ReadAsync(path);

            Assert.Equal("enUS", result.Locale);
            Assert.Equal("0.1.1", result.CollectorVersion);
            Assert.Equal(2, result.ExportSchemaVersion);
            Assert.Equal(1, result.StorageSchemaVersion);
            Assert.Equal("wow_forever", result.Client.Id);
            Assert.Equal("1.60.1", result.Client.Version);
            Assert.Equal(70058, result.Client.Build);
            Assert.Equal(16001, result.Client.Interface);
            Assert.Equal(2, result.Sessions);
            Assert.Equal(325, result.TotalObservations);

            Assert.Equal(2, result.Apis.Count);
            Assert.False(
                result.Apis.Single(
                    item => item.Key == "GetSpellInfo")
                .Available);
            Assert.True(
                result.Apis.Single(
                    item => item.Key == "C_Spell.GetSpellInfo")
                .Available);

            var eventObservation =
                Assert.Single(result.Events);
            Assert.Equal(
                "ZONE_CHANGED",
                eventObservation.Key);
            Assert.True(eventObservation.Supported);
            Assert.True(eventObservation.Observed);

            var map = Assert.Single(result.Maps);
            Assert.Equal(1433, map.Id);
            Assert.Equal(
                "Redridge Mountains",
                map.Name);
            Assert.Equal(1415, map.ParentMapId);
            Assert.Equal(2121, map.MapArtId);
            Assert.Equal(14, map.ObservationCount);
            Assert.Equal([70058], map.Builds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsUnexpectedRootVariable()
    {
        var path = CreateCollectorFile(
            """
            OtherAddonDB = {
                ["schemaVersion"] = 1,
            }
            """);

        try
        {
            var reader = new PallandoCollectorReader();

            var exception =
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => reader.ReadAsync(path));

            Assert.Contains(
                "PallandoDataCollectorDB",
                exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_DoesNotAcceptExecutableLua()
    {
        var path = CreateCollectorFile(
            """
            PallandoDataCollectorDB = loadstring("return {}")()
            """);

        try
        {
            var reader = new PallandoCollectorReader();

            await Assert.ThrowsAsync<InvalidDataException>(
                () => reader.ReadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateCollectorFile(
        string content)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"PallandoDataCollector-{Guid.NewGuid():N}.lua");

        File.WriteAllText(
            path,
            content);

        return path;
    }
}
