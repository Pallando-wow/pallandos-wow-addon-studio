namespace AddonStudio.Media;

public sealed record ProjectMediaSnapshot(
    string? LogoFilePath,
    IReadOnlyList<string> ScreenshotFilePaths);
