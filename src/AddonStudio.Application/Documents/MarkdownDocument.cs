namespace AddonStudio.Application.Documents;

public sealed record MarkdownDocument(
    string FilePath,
    string DisplayName,
    string Text);
