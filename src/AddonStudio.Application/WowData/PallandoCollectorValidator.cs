using System.Text.RegularExpressions;
using AddonStudio.Core.Wow.Collector;

namespace AddonStudio.Application.WowData;

public enum PallandoCollectorValidationSeverity
{
    Warning,
    Error
}

public sealed record PallandoCollectorValidationIssue(
    PallandoCollectorValidationSeverity Severity,
    string Code,
    string Message);

public sealed record PallandoCollectorValidationResult(
    IReadOnlyList<PallandoCollectorValidationIssue> Issues)
{
    public int ErrorCount =>
        Issues.Count(
            issue =>
                issue.Severity ==
                PallandoCollectorValidationSeverity.Error);

    public int WarningCount =>
        Issues.Count(
            issue =>
                issue.Severity ==
                PallandoCollectorValidationSeverity.Warning);

    public bool IsReady => ErrorCount == 0;
}

public static class PallandoCollectorValidator
{
    private const int SupportedStorageSchemaVersion = 1;
    private const int SupportedExportSchemaVersion = 2;
    private const string SupportedClientId = "wow_forever";

    private static readonly Regex LocalePattern =
        new(
            "^[a-z]{2}[A-Z]{2}$",
            RegexOptions.CultureInvariant);

    private static readonly Regex ClientVersionPattern =
        new(
            "^(?<major>[0-9]+)\\.(?<minor>[0-9]+)\\.(?<patch>[0-9]+)$",
            RegexOptions.CultureInvariant);

    public static PallandoCollectorValidationResult Validate(
        PallandoCollectorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var issues =
            new List<PallandoCollectorValidationIssue>();

        if (snapshot.StorageSchemaVersion !=
            SupportedStorageSchemaVersion)
        {
            AddError(
                issues,
                "PCV001",
                $"Storage schema {snapshot.StorageSchemaVersion} is not supported; expected {SupportedStorageSchemaVersion}.");
        }

        if (snapshot.ExportSchemaVersion !=
            SupportedExportSchemaVersion)
        {
            AddError(
                issues,
                "PCV002",
                $"Export schema {snapshot.ExportSchemaVersion} is not supported; expected {SupportedExportSchemaVersion}.");
        }

        if (!string.Equals(
                snapshot.Client.Id,
                SupportedClientId,
                StringComparison.Ordinal))
        {
            AddError(
                issues,
                "PCV003",
                $"Client '{snapshot.Client.Id}' is not supported by the current Studio data workflow.");
        }

        ValidateClientVersionAndInterface(
            snapshot,
            issues);

        if (!LocalePattern.IsMatch(snapshot.Locale))
        {
            AddWarning(
                issues,
                "PCV006",
                string.IsNullOrWhiteSpace(snapshot.Locale)
                    ? "Collector locale is missing."
                    : $"Collector locale '{snapshot.Locale}' does not use the expected xxXX format.");
        }

        if (snapshot.Sessions <= 0)
        {
            AddWarning(
                issues,
                "PCV007",
                "The collector reports no completed sessions.");
        }

        var recordCount =
            snapshot.Apis.Count +
            snapshot.Events.Count +
            snapshot.Maps.Count;

        if (recordCount == 0)
        {
            AddWarning(
                issues,
                "PCV008",
                "The collector contains no supported observation records.");
        }

        var representedObservations =
            snapshot.Apis.Sum(item => item.ObservationCount) +
            snapshot.Events.Sum(item => item.ObservationCount) +
            snapshot.Maps.Sum(item => item.ObservationCount);

        if (snapshot.TotalObservations < 0)
        {
            AddError(
                issues,
                "PCV009",
                "Total observation count must not be negative.");
        }
        else if (representedObservations >
                 snapshot.TotalObservations)
        {
            AddError(
                issues,
                "PCV010",
                $"Displayed records account for {representedObservations} observations, but the collector total is only {snapshot.TotalObservations}.");
        }

        ValidateApis(
            snapshot,
            issues);
        ValidateEvents(
            snapshot,
            issues);
        ValidateMaps(
            snapshot,
            issues);

        if (recordCount > 0 &&
            !ContainsBuild(
                snapshot,
                snapshot.Client.Build))
        {
            AddWarning(
                issues,
                "PCV011",
                $"No imported observation references the current client build {snapshot.Client.Build}.");
        }

        return new PallandoCollectorValidationResult(
            issues);
    }

    private static void ValidateClientVersionAndInterface(
        PallandoCollectorSnapshot snapshot,
        List<PallandoCollectorValidationIssue> issues)
    {
        var match =
            ClientVersionPattern.Match(
                snapshot.Client.Version);

        if (!match.Success ||
            !int.TryParse(
                match.Groups["major"].Value,
                out var major) ||
            !int.TryParse(
                match.Groups["minor"].Value,
                out var minor) ||
            !int.TryParse(
                match.Groups["patch"].Value,
                out var patch))
        {
            AddError(
                issues,
                "PCV004",
                $"Client version '{snapshot.Client.Version}' is invalid.");
            return;
        }

        try
        {
            var expectedInterface =
                checked(
                    major * 10000 +
                    minor * 100 +
                    patch);

            if (snapshot.Client.Interface !=
                expectedInterface)
            {
                AddError(
                    issues,
                    "PCV005",
                    $"Interface {snapshot.Client.Interface} does not match client version {snapshot.Client.Version}; expected {expectedInterface}.");
            }
        }
        catch (OverflowException)
        {
            AddError(
                issues,
                "PCV004",
                $"Client version '{snapshot.Client.Version}' is outside the supported numeric range.");
        }
    }

    private static void ValidateApis(
        PallandoCollectorSnapshot snapshot,
        List<PallandoCollectorValidationIssue> issues)
    {
        foreach (var api in snapshot.Apis)
        {
            ValidateRecordEvidence(
                "API",
                api.Key,
                api.ObservationCount,
                api.Builds,
                issues);

            if (api.Available &&
                string.Equals(
                    api.ValueType,
                    "nil",
                    StringComparison.Ordinal))
            {
                AddError(
                    issues,
                    "PCV101",
                    $"API '{api.Key}' is marked available but has value type 'nil'.");
            }

            if (!api.Available &&
                !string.IsNullOrWhiteSpace(
                    api.ValueType) &&
                !string.Equals(
                    api.ValueType,
                    "nil",
                    StringComparison.Ordinal))
            {
                AddWarning(
                    issues,
                    "PCV102",
                    $"API '{api.Key}' is marked missing but reports value type '{api.ValueType}'.");
            }
        }
    }

    private static void ValidateEvents(
        PallandoCollectorSnapshot snapshot,
        List<PallandoCollectorValidationIssue> issues)
    {
        foreach (var eventObservation in snapshot.Events)
        {
            ValidateRecordEvidence(
                "Event",
                eventObservation.Key,
                eventObservation.ObservationCount,
                eventObservation.Builds,
                issues);

            if (eventObservation.Observed &&
                !eventObservation.Supported)
            {
                AddError(
                    issues,
                    "PCV201",
                    $"Event '{eventObservation.Key}' was observed although it is marked unsupported.");
            }
        }
    }

    private static void ValidateMaps(
        PallandoCollectorSnapshot snapshot,
        List<PallandoCollectorValidationIssue> issues)
    {
        var mapIds =
            snapshot.Maps
                .Select(map => map.Id)
                .ToHashSet();

        foreach (var map in snapshot.Maps)
        {
            ValidateRecordEvidence(
                "Map",
                map.Id.ToString(),
                map.ObservationCount,
                map.Builds,
                issues);

            if (string.IsNullOrWhiteSpace(map.Name))
            {
                AddWarning(
                    issues,
                    "PCV301",
                    $"Map {map.Id} has no name.");
            }

            if (map.ParentMapId == map.Id)
            {
                AddError(
                    issues,
                    "PCV302",
                    $"Map {map.Id} references itself as its parent.");
            }
            else if (map.ParentMapId is > 0 &&
                     !mapIds.Contains(
                         map.ParentMapId.Value))
            {
                AddWarning(
                    issues,
                    "PCV303",
                    $"Parent map {map.ParentMapId} referenced by map {map.Id} is not present in the imported snapshot.");
            }

            var hasWidth =
                map.WorldWidth is not null;
            var hasHeight =
                map.WorldHeight is not null;

            if (hasWidth != hasHeight)
            {
                AddWarning(
                    issues,
                    "PCV304",
                    $"Map {map.Id} has incomplete world-size data.");
            }

            if (map.WorldWidth is <= 0 ||
                map.WorldHeight is <= 0)
            {
                AddError(
                    issues,
                    "PCV305",
                    $"Map {map.Id} has a non-positive world size.");
            }
        }
    }

    private static void ValidateRecordEvidence(
        string kind,
        string identity,
        int observationCount,
        IReadOnlyList<int> builds,
        List<PallandoCollectorValidationIssue> issues)
    {
        if (observationCount <= 0)
        {
            AddError(
                issues,
                "PCV401",
                $"{kind} '{identity}' has no positive observation count.");
        }

        if (builds.Count == 0)
        {
            AddWarning(
                issues,
                "PCV402",
                $"{kind} '{identity}' has no build evidence.");
        }
    }

    private static bool ContainsBuild(
        PallandoCollectorSnapshot snapshot,
        int build) =>
        snapshot.Apis.Any(
            item => item.Builds.Contains(build)) ||
        snapshot.Events.Any(
            item => item.Builds.Contains(build)) ||
        snapshot.Maps.Any(
            item => item.Builds.Contains(build));

    private static void AddError(
        List<PallandoCollectorValidationIssue> issues,
        string code,
        string message) =>
        issues.Add(
            new PallandoCollectorValidationIssue(
                PallandoCollectorValidationSeverity.Error,
                code,
                message));

    private static void AddWarning(
        List<PallandoCollectorValidationIssue> issues,
        string code,
        string message) =>
        issues.Add(
            new PallandoCollectorValidationIssue(
                PallandoCollectorValidationSeverity.Warning,
                code,
                message));
}
