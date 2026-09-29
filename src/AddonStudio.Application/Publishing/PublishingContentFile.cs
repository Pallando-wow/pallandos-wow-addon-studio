using AddonStudio.Core.Publishing;

namespace AddonStudio.Application.Publishing;

public sealed record PublishingContentFile(
    PublishingContentKind Kind,
    string Path,
    bool Exists);
