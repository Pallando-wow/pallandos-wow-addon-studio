namespace AddonStudio.Core.Wow.Collector;

public sealed record PallandoCollectorSnapshot(
    string SourcePath,
    int StorageSchemaVersion,
    string CollectorVersion,
    int ExportSchemaVersion,
    string Locale,
    PallandoCollectorClient Client,
    int Sessions,
    int TotalObservations,
    IReadOnlyList<PallandoCollectorApiObservation> Apis,
    IReadOnlyList<PallandoCollectorEventObservation> Events,
    IReadOnlyList<PallandoCollectorMapObservation> Maps);

public sealed record PallandoCollectorClient(
    string Id,
    string Version,
    int Build,
    int Interface);

public sealed record PallandoCollectorApiObservation(
    string Key,
    bool Available,
    string ValueType,
    int ObservationCount,
    IReadOnlyList<int> Builds);

public sealed record PallandoCollectorEventObservation(
    string Key,
    bool Supported,
    bool Observed,
    int ObservationCount,
    IReadOnlyList<int> Builds);

public sealed record PallandoCollectorMapObservation(
    int Id,
    string Name,
    int? MapType,
    int? ParentMapId,
    int? Flags,
    int? PlayerMinLevel,
    int? PlayerMaxLevel,
    int? PetMinLevel,
    int? PetMaxLevel,
    int? MapArtId,
    double? WorldWidth,
    double? WorldHeight,
    int ObservationCount,
    IReadOnlyList<int> Builds);
