using AddonStudio.Core.Wow.Data;

namespace AddonStudio.Application.WowData;

public interface IWowDevelopmentDataSource
{
    Task<WowDevelopmentDataCatalog> GetCatalogAsync(
        CancellationToken cancellationToken = default);
}
