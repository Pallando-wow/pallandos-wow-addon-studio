using AddonStudio.Core.Wow.Collector;

namespace AddonStudio.Application.WowData;

public interface IPallandoCollectorReader
{
    Task<PallandoCollectorSnapshot> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
