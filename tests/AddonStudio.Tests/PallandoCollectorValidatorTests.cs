using AddonStudio.Application.WowData;
using AddonStudio.Core.Wow.Collector;

namespace AddonStudio.Tests;

public sealed class PallandoCollectorValidatorTests
{
    [Fact]
    public void Validate_ValidForeverSnapshot_IsReady()
    {
        var result =
            PallandoCollectorValidator.Validate(
                CreateValidSnapshot());

        Assert.True(result.IsReady);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_InconsistentSnapshot_IsBlocked()
    {
        var snapshot =
            CreateValidSnapshot() with
            {
                ExportSchemaVersion = 3,
                Locale = "english",
                Client = new PallandoCollectorClient(
                    "retail",
                    "1.60.1",
                    70058,
                    11509),
                Sessions = 0,
                TotalObservations = 1,
                Events =
                [
                    new PallandoCollectorEventObservation(
                        "QUEST_ACCEPTED",
                        false,
                        true,
                        3,
                        [70058])
                ],
                Maps =
                [
                    new PallandoCollectorMapObservation(
                        1433,
                        "Redridge Mountains",
                        3,
                        1433,
                        2,
                        null,
                        null,
                        null,
                        null,
                        2121,
                        2170.83,
                        1447.92,
                        4,
                        [70058])
                ]
            };

        var result =
            PallandoCollectorValidator.Validate(snapshot);

        Assert.False(result.IsReady);
        Assert.True(result.ErrorCount >= 5);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV002");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV003");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV005");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV010");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV201");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV302");
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code == "PCV006" &&
                issue.Severity ==
                PallandoCollectorValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_MissingParentAndBuildEvidence_ReturnsWarnings()
    {
        var snapshot =
            CreateValidSnapshot() with
            {
                Apis = [],
                Events = [],
                Maps =
                [
                    new PallandoCollectorMapObservation(
                        1433,
                        "Redridge Mountains",
                        3,
                        1415,
                        2,
                        null,
                        null,
                        null,
                        null,
                        2121,
                        2170.83,
                        1447.92,
                        4,
                        [])
                ],
                TotalObservations = 4
            };

        var result =
            PallandoCollectorValidator.Validate(snapshot);

        Assert.True(result.IsReady);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV303");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV402");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "PCV011");
    }

    private static PallandoCollectorSnapshot
        CreateValidSnapshot() =>
        new(
            "PallandoDataCollector.lua",
            1,
            "0.1.1",
            2,
            "enUS",
            new PallandoCollectorClient(
                "wow_forever",
                "1.60.1",
                70058,
                16001),
            2,
            9,
            [
                new PallandoCollectorApiObservation(
                    "C_Map.GetMapInfo",
                    true,
                    "function",
                    2,
                    [70058])
            ],
            [
                new PallandoCollectorEventObservation(
                    "ZONE_CHANGED",
                    true,
                    true,
                    3,
                    [70058])
            ],
            [
                new PallandoCollectorMapObservation(
                    947,
                    "Azeroth",
                    1,
                    0,
                    2304,
                    0,
                    0,
                    0,
                    0,
                    2141,
                    19731.83,
                    68042.07,
                    4,
                    [70058])
            ]);
}
