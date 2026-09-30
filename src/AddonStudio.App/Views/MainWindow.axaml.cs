using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AddonStudio.App.ViewModels;
using AddonStudio.Core.Publishing;

namespace AddonStudio.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MarkdownSplitEditorTextBox.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            MarkdownSplitEditor_ScrollChanged);

        MarkdownSplitPreview.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            MarkdownSplitPreview_ScrollChanged);

        MarkdownEditorTextBox.PropertyChanged +=
            MarkdownEditor_PropertyChanged;

        MarkdownSplitEditorTextBox.PropertyChanged +=
            MarkdownEditor_PropertyChanged;

        Opened += MainWindow_Opened;
    }

    private MainWindowViewModel? ViewModel =>
        DataContext as MainWindowViewModel;

    private async void MainWindow_Opened(
        object? sender,
        EventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.InitializeAsync();
        }
    }

    private async void ChooseProjectRoot_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var path = await PickFolderAsync(
            "Select global project root");

        if (path is not null && ViewModel is not null)
        {
            ViewModel.ProjectRoot = path;
        }
    }

    private async void ChooseWowAddOnsPath_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var path = await PickFolderAsync(
            "Select WoW Forever AddOns folder");

        if (path is not null && ViewModel is not null)
        {
            ViewModel.WowForeverAddOnsPath = path;
        }
    }

    private async void ChooseSavedVariablesPath_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var path = await PickFolderAsync(
            "Select WoW SavedVariables folder");

        if (path is not null && ViewModel is not null)
        {
            ViewModel.SavedVariablesPath = path;
        }
    }

    private async void ChooseImportSource_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var path = await PickFolderAsync(
            "Select existing WoW addon");

        if (path is not null && ViewModel is not null)
        {
            ViewModel.ImportSourceDirectory = path;
        }
    }

    private async void ChooseCollectorSource_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var path = await PickFileAsync(
            "Select PallandoDataCollector SavedVariables");

        if (path is not null && ViewModel is not null)
        {
            ViewModel.CollectorSourceFile = path;
        }
    }

    private async void ChoosePublishingLogo_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var path = await PickLogoFileAsync(
            "Select project logo");

        if (path is not null && ViewModel is not null)
        {
            await ViewModel.SetPublishingLogoAsync(path);
        }
    }

    private async void AddPublishingScreenshots_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var paths = await PickScreenshotFilesAsync(
            "Select project screenshots");

        if (paths.Count > 0 && ViewModel is not null)
        {
            await ViewModel.AddPublishingScreenshotsAsync(
                paths);
        }
    }

    private async void SaveSettings_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.SaveSettingsAsync();
        }
    }

    private void ResetSettings_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ViewModel?.ResetSettings();
    }

    private async void RefreshProjects_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.RefreshProjectsAsync();
        }
    }

    private async void OpenSelectedProject_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.OpenSelectedProjectAsync();
        }
    }

    private async void CreateAddon_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.CreateAddonAsync();
        }
    }

    private async void ImportAddon_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ImportAddonAsync();
        }
    }

    private async void ImportCollector_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ImportCollectorAsync();
        }
    }

    private async void ProjectTree_DoubleTapped(
        object? sender,
        TappedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!ViewModel.HasSelectedMarkdownFile)
        {
            await ViewModel.OpenSelectedProjectTreeItemAsync();
            return;
        }

        var discardUnsavedChanges =
            !ViewModel.IsMarkdownDirty ||
            await ConfirmDiscardMarkdownChangesAsync();

        if (!discardUnsavedChanges)
        {
            return;
        }

        await ViewModel.OpenSelectedProjectTreeItemAsync(
            discardUnsavedChanges: true);
    }

    private async void OpenPublishingSummary_Click(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPublishingContentWithConfirmationAsync(
            PublishingContentKind.Summary);

    private async void OpenPublishingDescription_Click(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPublishingContentWithConfirmationAsync(
            PublishingContentKind.Description);

    private async void OpenPublishingChangelog_Click(
        object? sender,
        RoutedEventArgs e) =>
        await OpenPublishingContentWithConfirmationAsync(
            PublishingContentKind.Changelog);

    private async Task OpenPublishingContentWithConfirmationAsync(
        PublishingContentKind kind)
    {
        if (ViewModel is null)
        {
            return;
        }

        var discardUnsavedChanges =
            !ViewModel.IsMarkdownDirty ||
            await ConfirmDiscardMarkdownChangesAsync();

        if (!discardUnsavedChanges)
        {
            return;
        }

        await ViewModel.OpenPublishingContentFromUiAsync(
            kind,
            discardUnsavedChanges: true);
    }

    private async Task<bool> ConfirmDiscardMarkdownChangesAsync()
    {
        if (ViewModel is null)
        {
            return false;
        }

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 90
        };

        var discardButton = new Button
        {
            Content = "Discard Changes",
            MinWidth = 130
        };

        var dialog = new Window
        {
            Title = "Unsaved changes",
            Width = 460,
            Height = 205,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        cancelButton.Click +=
            (_, _) => dialog.Close(false);

        discardButton.Click +=
            (_, _) => dialog.Close(true);

        dialog.Content = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Unsaved changes",
                        FontSize = 20,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text =
                            $"'{ViewModel.MarkdownDocumentName}' contains unsaved changes. " +
                            "Discard them and open the selected file?",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment =
                            HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            discardButton
                        }
                    }
                }
            }
        };

        return await dialog.ShowDialog<bool>(this);
    }

    private bool syncingMarkdownScroll;

    private void MarkdownHeading1_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyHeadingLevel(1);

    private void MarkdownHeading2_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyHeadingLevel(2);

    private void MarkdownHeading3_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyHeadingLevel(3);

    private void MarkdownHeading4_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyHeadingLevel(4);

    private void ApplyHeadingLevel(int level) =>
        ApplyLinePrefix(
            string.Empty,
            LinePrefixMode.Heading,
            level);

    private void MarkdownBold_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyInlineMarkdown(
            "**",
            "**",
            "bold text");

    private void MarkdownItalic_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyInlineMarkdown(
            "*",
            "*",
            "italic text");

    private void MarkdownStrike_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyInlineMarkdown(
            "~~",
            "~~",
            "strikethrough");

    private void MarkdownLink_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyInlineMarkdown(
            "[",
            "](https://)",
            "link text");

    private void MarkdownCode_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyInlineMarkdown(
            "`",
            "`",
            "code");

    private void MarkdownImage_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyInlineMarkdown(
            "![",
            "](image.png)",
            "alt text");

    private void MarkdownNumberedList_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyLinePrefix(
            string.Empty,
            LinePrefixMode.NumberedList);

    private void MarkdownBulletList_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyLinePrefix(
            "- ",
            LinePrefixMode.BulletList);

    private void MarkdownQuote_Click(
        object? sender,
        RoutedEventArgs e) =>
        ApplyLinePrefix(
            "> ",
            LinePrefixMode.Quote);

    private TextBox? ActiveMarkdownEditor =>
        ViewModel?.IsMarkdownSplitMode == true
            ? MarkdownSplitEditorTextBox
            : ViewModel?.IsMarkdownEditorMode == true
                ? MarkdownEditorTextBox
                : null;

    private int markdownSelectionStart;
    private int markdownSelectionEnd;

    private void MarkdownEditor_PropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TextBox editor ||
            (e.Property != TextBox.SelectionStartProperty &&
             e.Property != TextBox.SelectionEndProperty))
        {
            return;
        }

        markdownSelectionStart = editor.SelectionStart;
        markdownSelectionEnd = editor.SelectionEnd;
    }

    private (int Start, int End) GetMarkdownSelection(
        TextBox editor,
        int textLength)
    {
        var start = editor.IsFocused
            ? editor.SelectionStart
            : markdownSelectionStart;
        var end = editor.IsFocused
            ? editor.SelectionEnd
            : markdownSelectionEnd;

        return (
            Math.Clamp(
                Math.Min(start, end),
                0,
                textLength),
            Math.Clamp(
                Math.Max(start, end),
                0,
                textLength));
    }

    private void ApplyInlineMarkdown(
        string prefix,
        string suffix,
        string placeholder)
    {
        var editor = ActiveMarkdownEditor;

        if (editor is null ||
            ViewModel is null)
        {
            return;
        }

        var text = ViewModel.MarkdownText;
        var selection =
            GetMarkdownSelection(
                editor,
                text.Length);
        var selectionStart = selection.Start;
        var selectionEnd = selection.End;

        var selected = text[
            selectionStart..selectionEnd];
        var body = selected.Length > 0
            ? selected
            : placeholder;

        var inserted =
            prefix + body + suffix;

        var updated =
            text[..selectionStart] +
            inserted +
            text[selectionEnd..];

        ViewModel.MarkdownText = updated;
        editor.Text = updated;
        editor.SelectionStart =
            selectionStart + prefix.Length;
        editor.SelectionEnd =
            editor.SelectionStart + body.Length;
        markdownSelectionStart =
            editor.SelectionStart;
        markdownSelectionEnd =
            editor.SelectionEnd;
        editor.Focus();
    }

    private void ApplyLinePrefix(
        string prefix,
        LinePrefixMode mode,
        int headingLevel = 1)
    {
        var editor = ActiveMarkdownEditor;

        if (editor is null ||
            ViewModel is null)
        {
            return;
        }

        var text = ViewModel.MarkdownText;
        var selection =
            GetMarkdownSelection(
                editor,
                text.Length);
        var selectionStart = selection.Start;
        var selectionEnd = selection.End;

        var lineStart = selectionStart == 0
            ? 0
            : text.LastIndexOf(
                '\n',
                selectionStart - 1) + 1;

        var nextLineBreak =
            text.IndexOf(
                '\n',
                selectionEnd);

        var lineEnd = nextLineBreak < 0
            ? text.Length
            : nextLineBreak;

        var block = text[lineStart..lineEnd];
        var lines = block.Split('\n');

        var removePrefixes =
            mode switch
            {
                LinePrefixMode.BulletList =>
                    lines
                        .Where(line =>
                            !string.IsNullOrWhiteSpace(line))
                        .All(line =>
                            line.StartsWith(
                                "- ",
                                StringComparison.Ordinal)),
                LinePrefixMode.Quote =>
                    lines
                        .Where(line =>
                            !string.IsNullOrWhiteSpace(line))
                        .All(line =>
                            line.StartsWith(
                                "> ",
                                StringComparison.Ordinal)),
                LinePrefixMode.NumberedList =>
                    lines
                        .Where(line =>
                            !string.IsNullOrWhiteSpace(line))
                        .All(HasNumberedListPrefix),
                _ => false
            };

        for (var index = 0;
             index < lines.Length;
             index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            lines[index] = mode switch
            {
                LinePrefixMode.Heading =>
                    SetHeadingLevel(
                        lines[index],
                        headingLevel),
                LinePrefixMode.NumberedList =>
                    removePrefixes
                        ? StripNumberedListPrefix(
                            lines[index])
                        : $"{index + 1}. " +
                          StripNumberedListPrefix(
                              lines[index]),
                LinePrefixMode.BulletList =>
                    removePrefixes
                        ? lines[index][2..]
                        : prefix +
                          StripBulletPrefix(
                              lines[index]),
                LinePrefixMode.Quote =>
                    removePrefixes
                        ? lines[index][2..]
                        : prefix +
                          StripQuotePrefix(
                              lines[index]),
                _ => lines[index]
            };
        }

        var replacement =
            string.Join(
                '\n',
                lines);

        var updated =
            text[..lineStart] +
            replacement +
            text[lineEnd..];

        ViewModel.MarkdownText = updated;
        editor.Text = updated;
        editor.SelectionStart = lineStart;
        editor.SelectionEnd =
            lineStart + replacement.Length;
        markdownSelectionStart =
            editor.SelectionStart;
        markdownSelectionEnd =
            editor.SelectionEnd;
        editor.Focus();
    }

    private static string SetHeadingLevel(
        string line,
        int level)
    {
        level = Math.Clamp(
            level,
            1,
            4);

        var stripped = line.TrimStart();
        var leadingLength =
            line.Length - stripped.Length;

        var currentLevel = 0;

        while (currentLevel < stripped.Length &&
               currentLevel < 6 &&
               stripped[currentLevel] == '#')
        {
            currentLevel++;
        }

        if (currentLevel > 0 &&
            currentLevel < stripped.Length &&
            stripped[currentLevel] == ' ')
        {
            stripped =
                stripped[(currentLevel + 1)..];
        }

        if (currentLevel == level)
        {
            return line[..leadingLength] +
                   stripped;
        }

        return line[..leadingLength] +
               new string(
                   '#',
                   level) +
               " " +
               stripped;
    }

    private static string StripBulletPrefix(
        string line) =>
        line.StartsWith(
            "- ",
            StringComparison.Ordinal) ||
        line.StartsWith(
            "* ",
            StringComparison.Ordinal) ||
        line.StartsWith(
            "+ ",
            StringComparison.Ordinal)
            ? line[2..]
            : line;

    private static string StripQuotePrefix(
        string line) =>
        line.StartsWith(
            "> ",
            StringComparison.Ordinal)
            ? line[2..]
            : line;

    private static bool HasNumberedListPrefix(
        string line)
    {
        var dotIndex = line.IndexOf(
            ". ",
            StringComparison.Ordinal);

        return dotIndex > 0 &&
               line[..dotIndex]
                   .All(char.IsDigit);
    }

    private static string StripNumberedListPrefix(
        string line)
    {
        var dotIndex = line.IndexOf(
            ". ",
            StringComparison.Ordinal);

        return dotIndex > 0 &&
               line[..dotIndex]
                   .All(char.IsDigit)
            ? line[(dotIndex + 2)..]
            : line;
    }

    private void MarkdownSplitEditor_ScrollChanged(
        object? sender,
        ScrollChangedEventArgs e)
    {
        if (syncingMarkdownScroll ||
            e.Source is not ScrollViewer source)
        {
            return;
        }

        var target =
            MarkdownSplitPreview
                .GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault();

        SyncMarkdownScroll(
            source,
            target);
    }

    private void MarkdownSplitPreview_ScrollChanged(
        object? sender,
        ScrollChangedEventArgs e)
    {
        if (syncingMarkdownScroll ||
            e.Source is not ScrollViewer source)
        {
            return;
        }

        var target =
            MarkdownSplitEditorTextBox
                .GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault();

        SyncMarkdownScroll(
            source,
            target);
    }

    private void SyncMarkdownScroll(
        ScrollViewer source,
        ScrollViewer? target)
    {
        if (target is null)
        {
            return;
        }

        var sourceMaximum =
            Math.Max(
                0,
                source.Extent.Height -
                source.Viewport.Height);
        var targetMaximum =
            Math.Max(
                0,
                target.Extent.Height -
                target.Viewport.Height);

        if (sourceMaximum <= 0 ||
            targetMaximum <= 0)
        {
            return;
        }

        var ratio =
            Math.Clamp(
                source.Offset.Y /
                sourceMaximum,
                0,
                1);

        try
        {
            syncingMarkdownScroll = true;

            target.Offset =
                new Vector(
                    target.Offset.X,
                    ratio * targetMaximum);
        }
        finally
        {
            syncingMarkdownScroll = false;
        }
    }

    private enum LinePrefixMode
    {
        Heading,
        NumberedList,
        BulletList,
        Quote
    }

    private async Task<string?> PickFileAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(
                        "Lua SavedVariables")
                    {
                        Patterns = ["*.lua"]
                    }
                ]
            });

        return files.Count == 0
            ? null
            : files[0].TryGetLocalPath();
    }

    private async Task<string?> PickLogoFileAsync(
        string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(
                        "PNG image")
                    {
                        Patterns = ["*.png"]
                    }
                ]
            });

        return files.Count == 0
            ? null
            : files[0].TryGetLocalPath();
    }

    private async Task<IReadOnlyList<string>> PickScreenshotFilesAsync(
        string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(
                        "Images")
                    {
                        Patterns =
                        [
                            "*.png",
                            "*.jpg",
                            "*.jpeg"
                        ]
                    }
                ]
            });

        return files
            .Select(file =>
                file.TryGetLocalPath())
            .Where(path =>
                !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

        return folders.Count == 0
            ? null
            : folders[0].TryGetLocalPath();
    }
}
