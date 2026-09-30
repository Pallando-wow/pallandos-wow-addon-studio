using System.Collections.ObjectModel;
using AddonStudio.Application.Documents;
using AddonStudio.Application.Projects;
using AddonStudio.Application.Publishing;
using AddonStudio.Application.Settings;
using AddonStudio.Application.WowData;
using AddonStudio.Core.Projects;
using AddonStudio.Core.Publishing;
using AddonStudio.Media;
using AddonStudio.Wow.Toc;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AddonStudio.App.ViewModels;

public enum StudioSidebar
{
    Start,
    ProjectOverview,
    Explorer,
    Components,
    Git,
    Publishing,
    CurseForge,
    Settings
}

public enum MarkdownEditorMode
{
    Editor,
    Preview,
    Split
}

public sealed record CollectorApiRow(
    string Key,
    string Availability,
    string ValueType,
    int Observations,
    string Builds);

public sealed record CollectorEventRow(
    string Key,
    string Support,
    string Observed,
    int Observations,
    string Builds);

public sealed record CollectorMapRow(
    int Id,
    string Name,
    string Type,
    string Parent,
    string Size,
    int Observations,
    string Builds);

public sealed record CollectorValidationIssueRow(
    string Severity,
    string Code,
    string Message);

public partial class MainWindowViewModel(
    AddonProjectService addonProjectService,
    IStudioSettingsStore settingsStore,
    ProjectCatalogService projectCatalogService,
    ProjectExplorerService projectExplorerService,
    MarkdownDocumentService markdownDocumentService,
    PublishingContentService publishingContentService,
    ProjectMediaService projectMediaService,
    TocDocumentReader tocDocumentReader,
    IPallandoCollectorReader pallandoCollectorReader) : ViewModelBase
{
    public string StudioVersion =>
        typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    [ObservableProperty]
    private StudioSidebar sidebar;

    [ObservableProperty]
    private string newAddonName = string.Empty;

    [ObservableProperty]
    private string importSourceDirectory = string.Empty;

    [ObservableProperty]
    private string collectorSourceFile = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private string? currentProjectName;

    [ObservableProperty]
    private string? currentProjectDirectory;

    [ObservableProperty]
    private string? currentProjectTypeName;

    [ObservableProperty]
    private string? currentPrimaryAddon;

    [ObservableProperty]
    private bool hasActionTab;

    [ObservableProperty]
    private string actionTabTitle = string.Empty;

    [ObservableProperty]
    private bool isCreateAddonAction;

    [ObservableProperty]
    private bool isImportAddonAction;

    [ObservableProperty]
    private bool isImportCollectorAction;

    [ObservableProperty]
    private bool hasCollectorImport;

    [ObservableProperty]
    private string collectorClientSummary =
        "No collector data loaded.";

    [ObservableProperty]
    private string collectorRunSummary = string.Empty;

    [ObservableProperty]
    private string collectorValidationSummary =
        "Not validated.";

    [ObservableProperty]
    private bool collectorValidationReady;

    [ObservableProperty]
    private bool hasCollectorValidationIssues;

    [ObservableProperty]
    private int workspaceTabIndex;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string projectRoot = string.Empty;

    [ObservableProperty]
    private string wowForeverAddOnsPath = string.Empty;

    [ObservableProperty]
    private bool setupRequired;

    [ObservableProperty]
    private ProjectCatalogEntry? selectedProject;

    [ObservableProperty]
    private bool hasSelectedProjectTocMetadata;

    [ObservableProperty]
    private string selectedProjectTocFile = "—";

    [ObservableProperty]
    private string selectedProjectTocTitle = "—";

    [ObservableProperty]
    private string selectedProjectTocVersion = "—";

    [ObservableProperty]
    private string selectedProjectTocAuthor = "—";

    [ObservableProperty]
    private string selectedProjectTocInterfaces = "—";

    [ObservableProperty]
    private string selectedProjectTocNotes = "—";

    [ObservableProperty]
    private string selectedProjectTocDependencies = "—";

    [ObservableProperty]
    private string selectedProjectTocSavedVariables = "—";

    [ObservableProperty]
    private bool hasSelectedProjectSummary;

    [ObservableProperty]
    private string selectedProjectSummary = string.Empty;

    [ObservableProperty]
    private ProjectTreeItem? selectedProjectTreeItem;

    [ObservableProperty]
    private string publishingLogoFileName = string.Empty;

    public ObservableCollection<string> PublishingScreenshots { get; } = [];

    [ObservableProperty]
    private string? markdownDocumentPath;

    [ObservableProperty]
    private string markdownText = string.Empty;

    [ObservableProperty]
    private bool hasMarkdownDocument;

    [ObservableProperty]
    private bool isMarkdownDirty;

    [ObservableProperty]
    private MarkdownEditorMode markdownEditorMode =
        MarkdownEditorMode.Split;

    private string savedMarkdownText = string.Empty;
    private PublishingContentKind? markdownPublishingKind;

    public MainWindowViewModel(
        AddonProjectService addonProjectService,
        IStudioSettingsStore settingsStore,
        ProjectCatalogService projectCatalogService,
        ProjectExplorerService projectExplorerService,
        MarkdownDocumentService markdownDocumentService,
        PublishingContentService publishingContentService,
        ProjectMediaService projectMediaService,
        TocDocumentReader tocDocumentReader,
        IPallandoCollectorReader pallandoCollectorReader,
        bool initialize = true)
        : this(
            addonProjectService,
            settingsStore,
            projectCatalogService,
            projectExplorerService,
            markdownDocumentService,
            publishingContentService,
            projectMediaService,
            tocDocumentReader,
            pallandoCollectorReader)
    {
        var settings = settingsStore.Load();

        projectRoot = settings.ProjectRoot;
        wowForeverAddOnsPath = settings.WowForeverAddOnsPath;
        setupRequired = !StudioSettingsValidator.IsComplete(settings);
        sidebar = setupRequired
            ? StudioSidebar.Settings
            : StudioSidebar.Start;

        if (setupRequired)
        {
            statusMessage = "Initial setup required";
        }
    }

    public ObservableCollection<ProjectCatalogEntry> Projects { get; } = [];

    public ObservableCollection<UnmanagedProjectFolder> UnmanagedFolders { get; } = [];

    public ObservableCollection<string> ProjectCatalogIssues { get; } = [];

    public ObservableCollection<ProjectTreeItem> CurrentProjectTree { get; } = [];

    public ObservableCollection<string> CurrentRuntimeAddons { get; } = [];

    public ObservableCollection<CollectorApiRow> CollectorApis { get; } = [];

    public ObservableCollection<CollectorEventRow> CollectorEvents { get; } = [];

    public ObservableCollection<CollectorMapRow> CollectorMaps { get; } = [];

    public ObservableCollection<CollectorValidationIssueRow> CollectorValidationIssues { get; } = [];

    public bool HasCurrentProject =>
        !string.IsNullOrWhiteSpace(CurrentProjectName);

    public bool HasNoCurrentProject => !HasCurrentProject;

    public bool IsSetupComplete => !SetupRequired;

    public bool HasProjects => Projects.Count > 0;

    public bool HasNoProjects => !HasProjects;

    public int ManagedProjectCount => Projects.Count;

    public int RuntimeAddonCount =>
        Projects.Sum(project => project.RuntimeAddonCount);

    public int UnmanagedFolderCount => UnmanagedFolders.Count;

    public bool HasUnmanagedFolders =>
        UnmanagedFolders.Count > 0;

    public bool HasProjectCatalogIssues =>
        ProjectCatalogIssues.Count > 0;

    public bool CanOpenSelectedProject =>
        SelectedProject is not null;

    public bool HasSelectedProject =>
        SelectedProject is not null;

    public bool HasNoSelectedProject =>
        SelectedProject is null;

    public string SelectedProjectName =>
        SelectedProject?.Name ?? string.Empty;

    public string SelectedProjectTypeName =>
        SelectedProject?.TypeName ?? string.Empty;

    public string SelectedProjectPrimaryAddon =>
        string.IsNullOrWhiteSpace(
            SelectedProject?.PrimaryAddon)
            ? "—"
            : SelectedProject!.PrimaryAddon;

    public int SelectedProjectRuntimeAddonCount =>
        SelectedProject?.RuntimeAddonCount ?? 0;

    public string SelectedProjectRuntimeAddons =>
        SelectedProject is null ||
        SelectedProject.Manifest.Runtime.Addons.Count == 0
            ? "—"
            : $"{SelectedProject.Manifest.Runtime.Addons.Count} · {string.Join(", ", SelectedProject.Manifest.Runtime.Addons)}";

    public string SelectedProjectComponents =>
        SelectedProject is null ||
        SelectedProject.Manifest.Components.Count == 0
            ? "None"
            : $"{SelectedProject.Manifest.Components.Count} · {string.Join(", ", SelectedProject.Manifest.Components.Select(component => component.Id))}";

    public string SelectedProjectPackageName =>
        string.IsNullOrWhiteSpace(
            SelectedProject?.Manifest.Release?.PackageName)
            ? "—"
            : SelectedProject!.Manifest.Release!.PackageName!;

    public string SelectedProjectCurseForge
    {
        get
        {
            var curseForge = SelectedProject?.Manifest.CurseForge;

            if (curseForge is null ||
                (string.IsNullOrWhiteSpace(curseForge.Slug) &&
                 string.IsNullOrWhiteSpace(curseForge.ProjectId)))
            {
                return "Not linked";
            }

            if (!string.IsNullOrWhiteSpace(curseForge.Slug) &&
                !string.IsNullOrWhiteSpace(curseForge.ProjectId))
            {
                return $"{curseForge.Slug} · #{curseForge.ProjectId}";
            }

            return curseForge.Slug ??
                $"Project #{curseForge.ProjectId}";
        }
    }

    public string SelectedProjectDirectory =>
        SelectedProject?.ProjectDirectory ?? string.Empty;

    public bool HasSelectedMarkdownFile =>
        SelectedProjectTreeItem is
        {
            IsDirectory: false
        } item &&
        string.Equals(
            Path.GetExtension(item.FullPath),
            ".md",
            StringComparison.OrdinalIgnoreCase);

    public string MarkdownDocumentName =>
        string.IsNullOrWhiteSpace(MarkdownDocumentPath)
            ? "Markdown"
            : Path.GetFileName(MarkdownDocumentPath);

    public string MarkdownDocumentTabTitle =>
        IsMarkdownDirty
            ? $"{MarkdownDocumentName} *"
            : MarkdownDocumentName;

    public bool IsMarkdownEditorMode =>
        MarkdownEditorMode == MarkdownEditorMode.Editor;

    public bool IsMarkdownPreviewMode =>
        MarkdownEditorMode == MarkdownEditorMode.Preview;

    public bool IsMarkdownSplitMode =>
        MarkdownEditorMode == MarkdownEditorMode.Split;

    public bool IsMarkdownEditingMode =>
        MarkdownEditorMode != MarkdownEditorMode.Preview;

    public bool CanCloseMarkdownDocument =>
        HasMarkdownDocument && !IsMarkdownDirty;

    public string PublishingSummaryFileName =>
        PublishingContentLayout.GetFileName(
            PublishingContentKind.Summary);

    public string PublishingDescriptionFileName =>
        PublishingContentLayout.GetFileName(
            PublishingContentKind.Description);

    public string PublishingChangelogFileName =>
        PublishingContentLayout.GetFileName(
            PublishingContentKind.Changelog);

    public string PublishingSummaryStatus =>
        GetPublishingStatus(
            PublishingContentKind.Summary);

    public string PublishingDescriptionStatus =>
        GetPublishingStatus(
            PublishingContentKind.Description);

    public string PublishingChangelogStatus =>
        GetPublishingStatus(
            PublishingContentKind.Changelog);

    public string PublishingSummaryActionText =>
        GetPublishingActionText(
            PublishingContentKind.Summary,
            "Summary");

    public string PublishingDescriptionActionText =>
        GetPublishingActionText(
            PublishingContentKind.Description,
            "Description");

    public string PublishingChangelogActionText =>
        GetPublishingActionText(
            PublishingContentKind.Changelog,
            "Changelog");

    public bool HasPublishingLogo =>
        !string.IsNullOrWhiteSpace(PublishingLogoFileName);

    public string PublishingLogoStatus =>
        HasPublishingLogo
            ? PublishingLogoFileName
            : "Not added";

    public string PublishingLogoActionText =>
        HasPublishingLogo
            ? "Replace Logo"
            : "Add Logo";

    public bool HasPublishingScreenshots =>
        PublishingScreenshots.Count > 0;

    public string PublishingScreenshotsStatus =>
        HasPublishingScreenshots
            ? $"{PublishingScreenshots.Count} screenshot(s)"
            : "No screenshots";

    public bool IsProjectSidebar =>
        Sidebar is
            StudioSidebar.ProjectOverview or
            StudioSidebar.Explorer or
            StudioSidebar.Git or
            StudioSidebar.Publishing or
            StudioSidebar.CurseForge;

    public bool IsProjectOverviewSidebar =>
        Sidebar == StudioSidebar.ProjectOverview;

    public bool HasSelectedProjectTreeItem =>
        SelectedProjectTreeItem is not null;

    public bool HasNoSelectedProjectTreeItem =>
        SelectedProjectTreeItem is null;

    public string SelectedProjectTreeItemName =>
        SelectedProjectTreeItem?.Name ?? string.Empty;

    public string SelectedProjectTreeItemPath =>
        SelectedProjectTreeItem?.FullPath ?? string.Empty;

    public bool IsSelectedReleaseFolder =>
        IsSelectedProjectDirectory(
            ProjectLayout.ReleaseDirectoryName);

    public bool IsSelectedLogoFolder =>
        IsSelectedProjectDirectory(
            ProjectLayout.MediaDirectoryName,
            ProjectLayout.LogoDirectoryName);

    public bool IsSelectedScreenshotsFolder =>
        IsSelectedProjectDirectory(
            ProjectLayout.MediaDirectoryName,
            ProjectLayout.ScreenshotsDirectoryName);

    public bool HasSelectedSpecialProjectFolder =>
        IsSelectedReleaseFolder ||
        IsSelectedLogoFolder ||
        IsSelectedScreenshotsFolder;

    public bool HasSelectedGenericProjectTreeItem =>
        HasSelectedProjectTreeItem &&
        !HasSelectedSpecialProjectFolder;

    public bool IsStartSidebar => Sidebar == StudioSidebar.Start;
    public bool IsExplorerSidebar => Sidebar == StudioSidebar.Explorer;
    public bool IsComponentsSidebar => Sidebar == StudioSidebar.Components;
    public bool IsGitSidebar => Sidebar == StudioSidebar.Git;
    public bool IsPublishingSidebar => Sidebar == StudioSidebar.Publishing;
    public bool IsCurseForgeSidebar => Sidebar == StudioSidebar.CurseForge;
    public bool IsSettingsSidebar => Sidebar == StudioSidebar.Settings;

    public string WorkspaceTitle => Sidebar switch
    {
        StudioSidebar.Start => "Start",
        StudioSidebar.ProjectOverview => CurrentProjectName ?? "Project",
        StudioSidebar.Explorer => "Files",
        StudioSidebar.Components => "Components",
        StudioSidebar.Git => "Git",
        StudioSidebar.Publishing => "Publishing",
        StudioSidebar.CurseForge => "CurseForge",
        StudioSidebar.Settings => "Settings",
        _ => "Workspace"
    };

    public async Task InitializeAsync()
    {
        if (IsSetupComplete)
        {
            await RefreshProjectsAsync();
        }
    }

    [RelayCommand]
    private void ShowStart()
    {
        if (SetupRequired)
        {
            ShowSettings();
            return;
        }

        SelectedProject = null;
        Sidebar = StudioSidebar.Start;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowProjectOverview()
    {
        if (SetupRequired ||
            !HasCurrentProject)
        {
            return;
        }

        Sidebar = StudioSidebar.ProjectOverview;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowExplorer()
    {
        if (SetupRequired ||
            !HasCurrentProject)
        {
            return;
        }

        Sidebar = StudioSidebar.Explorer;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowComponents()
    {
        if (SetupRequired)
        {
            return;
        }

        Sidebar = StudioSidebar.Components;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowGit()
    {
        if (SetupRequired ||
            !HasCurrentProject)
        {
            return;
        }

        Sidebar = StudioSidebar.Git;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowPublishing()
    {
        if (SetupRequired ||
            !HasCurrentProject)
        {
            return;
        }

        Sidebar = StudioSidebar.Publishing;
        WorkspaceTabIndex = 0;
        RefreshPublishingMedia();
        RaisePublishingProperties();
    }

    [RelayCommand]
    private void ShowCurseForge()
    {
        if (SetupRequired ||
            !HasCurrentProject)
        {
            return;
        }

        Sidebar = StudioSidebar.CurseForge;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        Sidebar = StudioSidebar.Settings;
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void OpenCreateAddon()
    {
        if (SetupRequired)
        {
            ShowSettings();
            return;
        }

        ActionTabTitle = "New Addon";
        IsCreateAddonAction = true;
        IsImportAddonAction = false;
        IsImportCollectorAction = false;
        HasActionTab = true;
        WorkspaceTabIndex = 1;
    }

    [RelayCommand]
    private void OpenImportAddon()
    {
        if (SetupRequired)
        {
            ShowSettings();
            return;
        }

        ImportSourceDirectory = string.Empty;
        ActionTabTitle = "Import Existing Addon";
        IsCreateAddonAction = false;
        IsImportAddonAction = true;
        IsImportCollectorAction = false;
        HasActionTab = true;
        WorkspaceTabIndex = 1;
    }

    [RelayCommand]
    private void OpenCollectorImport()
    {
        if (SetupRequired)
        {
            ShowSettings();
            return;
        }

        CollectorSourceFile = string.Empty;
        ClearCollectorImport();
        ActionTabTitle = "Collector Data";
        IsCreateAddonAction = false;
        IsImportAddonAction = false;
        IsImportCollectorAction = true;
        HasActionTab = true;
        WorkspaceTabIndex = 1;
    }

    [RelayCommand]
    private void CloseActionTab()
    {
        HasActionTab = false;
        IsCreateAddonAction = false;
        IsImportAddonAction = false;
        IsImportCollectorAction = false;
        WorkspaceTabIndex = 0;
    }

    public async Task SaveSettingsAsync()
    {
        var settings = new StudioSettings
        {
            ProjectRoot = ProjectRoot.Trim(),
            WowForeverAddOnsPath = WowForeverAddOnsPath.Trim()
        };

        var issues = StudioSettingsValidator.Validate(settings);

        if (issues.Count > 0)
        {
            StatusMessage = $"Settings error: {issues[0]}";
            SetupRequired = true;
            return;
        }

        settingsStore.Save(settings);

        ProjectRoot = Path.GetFullPath(settings.ProjectRoot);
        WowForeverAddOnsPath = Path.GetFullPath(settings.WowForeverAddOnsPath);
        SetupRequired = false;
        Sidebar = StudioSidebar.Start;
        WorkspaceTabIndex = 0;

        await RefreshProjectsAsync();

        StatusMessage = "Settings saved";
    }

    public void ResetSettings()
    {
        settingsStore.Delete();

        ProjectRoot = string.Empty;
        WowForeverAddOnsPath = string.Empty;
        SetupRequired = true;
        Sidebar = StudioSidebar.Settings;
        WorkspaceTabIndex = 0;
        HasActionTab = false;

        Projects.Clear();
        UnmanagedFolders.Clear();
        ProjectCatalogIssues.Clear();
        CurrentProjectTree.Clear();
        CurrentRuntimeAddons.Clear();
        ClearMarkdownDocument();

        SelectedProject = null;
        CurrentProjectName = null;
        CurrentProjectDirectory = null;
        CurrentProjectTypeName = null;
        CurrentPrimaryAddon = null;

        RaiseProjectCatalogProperties();

        StatusMessage =
            "Settings reset. Initial setup required.";
    }

    public async Task RefreshProjectsAsync()
    {
        if (SetupRequired)
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            var result = await projectCatalogService.DiscoverAsync(
                ProjectRoot);

            var selectedDirectory =
                SelectedProject?.ProjectDirectory;

            Projects.Clear();

            foreach (var project in result.Projects)
            {
                Projects.Add(project);
            }

            UnmanagedFolders.Clear();

            foreach (var folder in result.UnmanagedFolders)
            {
                UnmanagedFolders.Add(folder);
            }

            ProjectCatalogIssues.Clear();

            foreach (var issue in result.Issues)
            {
                ProjectCatalogIssues.Add(
                    $"{Path.GetFileName(issue.ProjectDirectory)}: {issue.Message}");
            }

            SelectedProject =
                string.IsNullOrWhiteSpace(selectedDirectory)
                    ? null
                    : Projects.FirstOrDefault(
                        project => string.Equals(
                            project.ProjectDirectory,
                            selectedDirectory,
                            StringComparison.OrdinalIgnoreCase));

            RaiseProjectCatalogProperties();

            StatusMessage =
                $"{result.Projects.Count} managed project(s), " +
                $"{result.UnmanagedFolders.Count} unmanaged folder(s), " +
                $"{result.Issues.Count} invalid project(s).";
        });
    }

    public async Task OpenSelectedProjectAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Select a project first.";
            return;
        }

        await OpenProjectAsync(SelectedProject);
    }

    public async Task OpenSelectedProjectTreeItemAsync(
        bool discardUnsavedChanges = false)
    {
        if (CurrentProjectDirectory is null ||
            SelectedProjectTreeItem is null)
        {
            return;
        }

        if (SelectedProjectTreeItem.IsDirectory)
        {
            return;
        }

        if (!HasSelectedMarkdownFile)
        {
            StatusMessage =
                "This editor foundation currently opens Markdown (.md) files.";
            return;
        }

        if (IsMarkdownDirty &&
            !discardUnsavedChanges)
        {
            StatusMessage =
                "The current Markdown document has unsaved changes.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            var document = await markdownDocumentService.OpenAsync(
                CurrentProjectDirectory,
                SelectedProjectTreeItem.FullPath);

            markdownPublishingKind = null;
            savedMarkdownText = document.Text;
            MarkdownDocumentPath = document.FilePath;
            MarkdownText = document.Text;
            HasMarkdownDocument = true;
            IsMarkdownDirty = false;
            MarkdownEditorMode = MarkdownEditorMode.Split;
            WorkspaceTabIndex = 2;

            StatusMessage =
                $"Opened '{document.DisplayName}'.";
        });
    }

    [RelayCommand]
    private Task OpenPublishingSummaryAsync() =>
        OpenPublishingContentAsync(
            PublishingContentKind.Summary);

    [RelayCommand]
    private Task OpenPublishingDescriptionAsync() =>
        OpenPublishingContentAsync(
            PublishingContentKind.Description);

    [RelayCommand]
    private Task OpenPublishingChangelogAsync() =>
        OpenPublishingContentAsync(
            PublishingContentKind.Changelog);

    public Task OpenPublishingContentFromUiAsync(
        PublishingContentKind kind,
        bool discardUnsavedChanges = false) =>
        OpenPublishingContentAsync(
            kind,
            discardUnsavedChanges);

    public async Task SetPublishingLogoAsync(
        string sourceFilePath)
    {
        if (CurrentProjectDirectory is null)
        {
            StatusMessage =
                "Open a project before adding a logo.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            await projectMediaService.SetLogoAsync(
                CurrentProjectDirectory,
                sourceFilePath);

            RefreshPublishingMedia();
            RefreshCurrentProjectTree();

            StatusMessage =
                "Project logo added.";
        });
    }

    public async Task AddPublishingScreenshotsAsync(
        IReadOnlyList<string> sourceFilePaths)
    {
        if (CurrentProjectDirectory is null)
        {
            StatusMessage =
                "Open a project before adding screenshots.";
            return;
        }

        if (sourceFilePaths.Count == 0)
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            var added =
                await projectMediaService.AddScreenshotsAsync(
                    CurrentProjectDirectory,
                    sourceFilePaths);

            RefreshPublishingMedia();
            RefreshCurrentProjectTree();

            StatusMessage =
                $"{added.Count} screenshot(s) added.";
        });
    }

    [RelayCommand]
    private void ShowMarkdownEditor() =>
        MarkdownEditorMode = MarkdownEditorMode.Editor;

    [RelayCommand]
    private void ShowMarkdownPreview() =>
        MarkdownEditorMode = MarkdownEditorMode.Preview;

    [RelayCommand]
    private void ShowMarkdownSplit() =>
        MarkdownEditorMode = MarkdownEditorMode.Split;

    [RelayCommand]
    private async Task SaveMarkdownDocumentAsync()
    {
        if (!HasMarkdownDocument ||
            CurrentProjectDirectory is null ||
            MarkdownDocumentPath is null)
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            if (markdownPublishingKind is PublishingContentKind kind)
            {
                await publishingContentService.WriteAsync(
                    CurrentProjectDirectory,
                    kind,
                    MarkdownText);

                RefreshCurrentProjectTree();
                RaisePublishingProperties();
            }
            else
            {
                await markdownDocumentService.SaveAsync(
                    CurrentProjectDirectory,
                    MarkdownDocumentPath,
                    MarkdownText);
            }

            savedMarkdownText = MarkdownText;
            IsMarkdownDirty = false;
            StatusMessage =
                $"Saved '{MarkdownDocumentName}'.";
        });
    }

    [RelayCommand]
    private void RevertMarkdownDocument()
    {
        if (!HasMarkdownDocument)
        {
            return;
        }

        MarkdownText = savedMarkdownText;
        IsMarkdownDirty = false;
        StatusMessage =
            $"Reverted '{MarkdownDocumentName}'.";
    }

    [RelayCommand]
    private void CloseMarkdownDocument()
    {
        if (!HasMarkdownDocument)
        {
            return;
        }

        if (IsMarkdownDirty)
        {
            StatusMessage =
                "Save or revert the Markdown document before closing it.";
            return;
        }

        ClearMarkdownDocument();
        WorkspaceTabIndex = 0;
        StatusMessage = "Markdown document closed.";
    }

    public Task OpenProjectAsync(
        ProjectCatalogEntry project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var changesProject =
            CurrentProjectDirectory is not null &&
            !string.Equals(
                CurrentProjectDirectory,
                project.ProjectDirectory,
                StringComparison.OrdinalIgnoreCase);

        if (changesProject && IsMarkdownDirty)
        {
            StatusMessage =
                "Save or revert the current Markdown document before switching projects.";
            return Task.CompletedTask;
        }

        if (changesProject)
        {
            ClearMarkdownDocument();
        }

        CurrentProjectName = project.Name;
        CurrentProjectDirectory = project.ProjectDirectory;
        CurrentProjectTypeName = project.TypeName;
        CurrentPrimaryAddon = project.PrimaryAddon;

        CurrentRuntimeAddons.Clear();

        foreach (var runtimeAddon in project.Manifest.Runtime.Addons)
        {
            CurrentRuntimeAddons.Add(runtimeAddon);
        }

        CurrentProjectTree.Clear();

        foreach (var item in projectExplorerService.BuildTree(project))
        {
            CurrentProjectTree.Add(item);
        }

        SelectedProject = project;
        SelectedProjectTreeItem = null;
        Sidebar = StudioSidebar.ProjectOverview;
        WorkspaceTabIndex = 0;
        StatusMessage = $"Project '{project.Name}' opened.";

        return Task.CompletedTask;
    }

    public async Task CreateAddonAsync()
    {
        await RunOperationAsync(async () =>
        {
            var result = await addonProjectService.CreateAsync(
                new CreateAddonProjectRequest(
                    NewAddonName,
                    ProjectRoot));

            NewAddonName = string.Empty;
            HasActionTab = false;

            await RefreshProjectsCoreAsync();

            var project = FindProject(
                result.ProjectDirectory);

            if (project is not null)
            {
                await OpenProjectAsync(project);
            }
            else
            {
                SetCurrentProject(result);
                Sidebar = StudioSidebar.Explorer;
            }

            StatusMessage =
                $"Addon '{result.ProjectName}' created.";
        });
    }

    public async Task ImportAddonAsync()
    {
        await RunOperationAsync(async () =>
        {
            var result = await addonProjectService.ImportAsync(
                new ImportAddonProjectRequest(
                    ImportSourceDirectory,
                    ProjectRoot));

            HasActionTab = false;

            await RefreshProjectsCoreAsync();

            var project = FindProject(
                result.ProjectDirectory);

            if (project is not null)
            {
                await OpenProjectAsync(project);
            }
            else
            {
                SetCurrentProject(result);
                Sidebar = StudioSidebar.Explorer;
            }

            StatusMessage =
                $"Addon '{result.ProjectName}' copied and normalized.";
        });
    }

    public async Task ImportCollectorAsync()
    {
        await RunOperationAsync(async () =>
        {
            var snapshot =
                await pallandoCollectorReader.ReadAsync(
                    CollectorSourceFile);

            CollectorApis.Clear();

            foreach (var api in snapshot.Apis)
            {
                CollectorApis.Add(
                    new CollectorApiRow(
                        api.Key,
                        api.Available ? "Available" : "Missing",
                        string.IsNullOrWhiteSpace(api.ValueType)
                            ? "—"
                            : api.ValueType,
                        api.ObservationCount,
                        FormatBuilds(api.Builds)));
            }

            CollectorEvents.Clear();

            foreach (var eventObservation in snapshot.Events)
            {
                CollectorEvents.Add(
                    new CollectorEventRow(
                        eventObservation.Key,
                        eventObservation.Supported
                            ? "Supported"
                            : "Unsupported",
                        eventObservation.Observed
                            ? "Yes"
                            : "No",
                        eventObservation.ObservationCount,
                        FormatBuilds(eventObservation.Builds)));
            }

            CollectorMaps.Clear();

            foreach (var map in snapshot.Maps)
            {
                CollectorMaps.Add(
                    new CollectorMapRow(
                        map.Id,
                        string.IsNullOrWhiteSpace(map.Name)
                            ? "—"
                            : map.Name,
                        map.MapType?.ToString() ?? "—",
                        map.ParentMapId?.ToString() ?? "—",
                        FormatMapSize(
                            map.WorldWidth,
                            map.WorldHeight),
                        map.ObservationCount,
                        FormatBuilds(map.Builds)));
            }

            CollectorClientSummary =
                $"{snapshot.Client.Id} · " +
                $"{snapshot.Client.Version} · " +
                $"build {snapshot.Client.Build} · " +
                $"Interface {snapshot.Client.Interface}";

            CollectorRunSummary =
                $"Collector {snapshot.CollectorVersion} · " +
                $"schema {snapshot.StorageSchemaVersion}/" +
                $"{snapshot.ExportSchemaVersion} · " +
                $"{snapshot.Locale} · " +
                $"{snapshot.Sessions} session(s) · " +
                $"{snapshot.TotalObservations} observation(s)";

            var validation =
                PallandoCollectorValidator.Validate(snapshot);

            CollectorValidationIssues.Clear();

            foreach (var issue in validation.Issues)
            {
                CollectorValidationIssues.Add(
                    new CollectorValidationIssueRow(
                        issue.Severity.ToString(),
                        issue.Code,
                        issue.Message));
            }

            CollectorValidationReady = validation.IsReady;
            HasCollectorValidationIssues =
                validation.Issues.Count > 0;

            CollectorValidationSummary =
                validation.IsReady
                    ? validation.WarningCount == 0
                        ? "Passed · ready for further processing"
                        : $"Passed with {validation.WarningCount} warning(s) · review before further processing"
                    : $"Blocked · {validation.ErrorCount} error(s), {validation.WarningCount} warning(s)";

            HasCollectorImport = true;

            StatusMessage =
                $"Collector data imported: " +
                $"{snapshot.Apis.Count} API(s), " +
                $"{snapshot.Events.Count} event(s), " +
                $"{snapshot.Maps.Count} map(s). " +
                (validation.IsReady
                    ? "Validation passed."
                    : $"Validation found {validation.ErrorCount} error(s).");
        });
    }

    partial void OnSidebarChanged(StudioSidebar value)
    {
        OnPropertyChanged(nameof(IsStartSidebar));
        OnPropertyChanged(nameof(IsProjectSidebar));
        OnPropertyChanged(nameof(IsProjectOverviewSidebar));
        OnPropertyChanged(nameof(IsExplorerSidebar));
        OnPropertyChanged(nameof(IsComponentsSidebar));
        OnPropertyChanged(nameof(IsGitSidebar));
        OnPropertyChanged(nameof(IsPublishingSidebar));
        OnPropertyChanged(nameof(IsCurseForgeSidebar));
        OnPropertyChanged(nameof(IsSettingsSidebar));
        OnPropertyChanged(nameof(WorkspaceTitle));
    }

    partial void OnSetupRequiredChanged(bool value) =>
        OnPropertyChanged(nameof(IsSetupComplete));

    partial void OnSelectedProjectChanged(
        ProjectCatalogEntry? value)
    {
        OnPropertyChanged(nameof(CanOpenSelectedProject));
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(HasNoSelectedProject));
        OnPropertyChanged(nameof(SelectedProjectName));
        OnPropertyChanged(nameof(SelectedProjectTypeName));
        OnPropertyChanged(nameof(SelectedProjectPrimaryAddon));
        OnPropertyChanged(nameof(SelectedProjectRuntimeAddonCount));
        OnPropertyChanged(nameof(SelectedProjectRuntimeAddons));
        OnPropertyChanged(nameof(SelectedProjectComponents));
        OnPropertyChanged(nameof(SelectedProjectPackageName));
        OnPropertyChanged(nameof(SelectedProjectCurseForge));
        OnPropertyChanged(nameof(SelectedProjectDirectory));

        ResetSelectedProjectPreviewMetadata();

        if (value is not null)
        {
            _ = LoadSelectedProjectPreviewMetadataAsync(value);
        }
    }

    private async Task LoadSelectedProjectPreviewMetadataAsync(
        ProjectCatalogEntry project)
    {
        try
        {
            var summary = await publishingContentService.ReadAsync(
                project.ProjectDirectory,
                PublishingContentKind.Summary);

            if (!IsStillSelectedProject(project))
            {
                return;
            }

            SelectedProjectSummary = summary.Trim();
            HasSelectedProjectSummary =
                SelectedProjectSummary.Length > 0;
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
        {
            // Preview metadata is optional and must not block project selection.
        }

        try
        {
            var runtimeAddon =
                !string.IsNullOrWhiteSpace(project.PrimaryAddon)
                    ? project.PrimaryAddon
                    : project.Manifest.Runtime.Addons.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(runtimeAddon))
            {
                return;
            }

            var addonDirectory = Path.Combine(
                project.ProjectDirectory,
                ProjectLayout.RuntimeDirectoryName,
                runtimeAddon);

            if (!Directory.Exists(addonDirectory))
            {
                return;
            }

            var tocFiles = Directory
                .EnumerateFiles(
                    addonDirectory,
                    "*.toc",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (tocFiles.Length == 0)
            {
                return;
            }

            var tocPath =
                tocFiles.FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        runtimeAddon,
                        StringComparison.OrdinalIgnoreCase))
                ?? tocFiles[0];

            var toc = await tocDocumentReader.ReadAsync(tocPath);

            if (!IsStillSelectedProject(project))
            {
                return;
            }

            SelectedProjectTocFile =
                Path.GetFileName(toc.FilePath);
            SelectedProjectTocTitle =
                DisplayMetadata(toc.Title);
            SelectedProjectTocVersion =
                DisplayMetadata(toc.Version);
            SelectedProjectTocAuthor =
                DisplayMetadata(toc.GetMetadata("Author"));
            SelectedProjectTocInterfaces =
                DisplayList(toc.Interfaces);
            SelectedProjectTocNotes =
                DisplayMetadata(toc.Notes);

            var dependencies = new List<string>();

            if (toc.Dependencies.Count > 0)
            {
                dependencies.Add(
                    $"Required: {string.Join(", ", toc.Dependencies)}");
            }

            if (toc.OptionalDependencies.Count > 0)
            {
                dependencies.Add(
                    $"Optional: {string.Join(", ", toc.OptionalDependencies)}");
            }

            SelectedProjectTocDependencies =
                dependencies.Count == 0
                    ? "—"
                    : string.Join(" · ", dependencies);

            var savedVariables = new List<string>();

            if (toc.SavedVariables.Count > 0)
            {
                savedVariables.Add(
                    $"Global: {string.Join(", ", toc.SavedVariables)}");
            }

            if (toc.SavedVariablesPerCharacter.Count > 0)
            {
                savedVariables.Add(
                    $"Per character: {string.Join(", ", toc.SavedVariablesPerCharacter)}");
            }

            SelectedProjectTocSavedVariables =
                savedVariables.Count == 0
                    ? "—"
                    : string.Join(" · ", savedVariables);

            HasSelectedProjectTocMetadata = true;
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
        {
            // A broken optional preview must not prevent opening the project.
        }
    }

    private bool IsStillSelectedProject(
        ProjectCatalogEntry project) =>
        SelectedProject is not null &&
        string.Equals(
            SelectedProject.ProjectDirectory,
            project.ProjectDirectory,
            StringComparison.OrdinalIgnoreCase);

    private static string DisplayMetadata(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "—"
            : value.Trim();

    private static string DisplayList(
        IReadOnlyList<string> values) =>
        values.Count == 0
            ? "—"
            : string.Join(", ", values);

    private void ResetSelectedProjectPreviewMetadata()
    {
        HasSelectedProjectTocMetadata = false;
        SelectedProjectTocFile = "—";
        SelectedProjectTocTitle = "—";
        SelectedProjectTocVersion = "—";
        SelectedProjectTocAuthor = "—";
        SelectedProjectTocInterfaces = "—";
        SelectedProjectTocNotes = "—";
        SelectedProjectTocDependencies = "—";
        SelectedProjectTocSavedVariables = "—";
        HasSelectedProjectSummary = false;
        SelectedProjectSummary = string.Empty;
    }

    partial void OnSelectedProjectTreeItemChanged(
        ProjectTreeItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedMarkdownFile));
        OnPropertyChanged(nameof(HasSelectedProjectTreeItem));
        OnPropertyChanged(nameof(HasNoSelectedProjectTreeItem));
        OnPropertyChanged(nameof(SelectedProjectTreeItemName));
        OnPropertyChanged(nameof(SelectedProjectTreeItemPath));
        OnPropertyChanged(nameof(IsSelectedReleaseFolder));
        OnPropertyChanged(nameof(IsSelectedLogoFolder));
        OnPropertyChanged(nameof(IsSelectedScreenshotsFolder));
        OnPropertyChanged(nameof(HasSelectedSpecialProjectFolder));
        OnPropertyChanged(nameof(HasSelectedGenericProjectTreeItem));
    }

    partial void OnMarkdownDocumentPathChanged(
        string? value)
    {
        OnPropertyChanged(nameof(MarkdownDocumentName));
        OnPropertyChanged(nameof(MarkdownDocumentTabTitle));
    }

    partial void OnMarkdownTextChanged(string value)
    {
        if (HasMarkdownDocument)
        {
            IsMarkdownDirty =
                !string.Equals(
                    value,
                    savedMarkdownText,
                    StringComparison.Ordinal);
        }
    }

    partial void OnIsMarkdownDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(MarkdownDocumentTabTitle));
        OnPropertyChanged(nameof(CanCloseMarkdownDocument));
    }

    partial void OnHasMarkdownDocumentChanged(bool value) =>
        OnPropertyChanged(nameof(CanCloseMarkdownDocument));

    partial void OnPublishingLogoFileNameChanged(
        string value)
    {
        OnPropertyChanged(nameof(HasPublishingLogo));
        OnPropertyChanged(nameof(PublishingLogoStatus));
        OnPropertyChanged(nameof(PublishingLogoActionText));
    }

    partial void OnCurrentProjectDirectoryChanged(
        string? value)
    {
        RefreshPublishingMedia();
        RaisePublishingProperties();
    }

    partial void OnMarkdownEditorModeChanged(
        MarkdownEditorMode value)
    {
        OnPropertyChanged(nameof(IsMarkdownEditorMode));
        OnPropertyChanged(nameof(IsMarkdownPreviewMode));
        OnPropertyChanged(nameof(IsMarkdownSplitMode));
        OnPropertyChanged(nameof(IsMarkdownEditingMode));
    }

    partial void OnCurrentProjectNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCurrentProject));
        OnPropertyChanged(nameof(HasNoCurrentProject));
        OnPropertyChanged(nameof(WorkspaceTitle));
    }

    private bool IsSelectedProjectDirectory(
        params string[] relativeSegments)
    {
        if (CurrentProjectDirectory is null ||
            SelectedProjectTreeItem is not
            {
                IsDirectory: true
            } item)
        {
            return false;
        }

        var expectedPath =
            relativeSegments.Aggregate(
                CurrentProjectDirectory,
                Path.Combine);

        return string.Equals(
            Path.GetFullPath(item.FullPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
            Path.GetFullPath(expectedPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private void ClearCollectorImport()
    {
        CollectorApis.Clear();
        CollectorEvents.Clear();
        CollectorMaps.Clear();
        CollectorValidationIssues.Clear();
        HasCollectorImport = false;
        CollectorValidationReady = false;
        HasCollectorValidationIssues = false;
        CollectorClientSummary =
            "No collector data loaded.";
        CollectorRunSummary = string.Empty;
        CollectorValidationSummary =
            "Not validated.";
    }

    private static string FormatBuilds(
        IReadOnlyList<int> builds) =>
        builds.Count == 0
            ? "—"
            : string.Join(", ", builds);

    private static string FormatMapSize(
        double? width,
        double? height) =>
        width is null || height is null
            ? "—"
            : $"{width.Value:0.##} × {height.Value:0.##}";

    private void ClearMarkdownDocument()
    {
        savedMarkdownText = string.Empty;
        markdownPublishingKind = null;
        MarkdownDocumentPath = null;
        MarkdownText = string.Empty;
        HasMarkdownDocument = false;
        IsMarkdownDirty = false;
        SelectedProjectTreeItem = null;
        MarkdownEditorMode = MarkdownEditorMode.Split;
    }

    private async Task OpenPublishingContentAsync(
        PublishingContentKind kind,
        bool discardUnsavedChanges = false)
    {
        if (CurrentProjectDirectory is null)
        {
            StatusMessage =
                "Open a project before editing publishing content.";
            return;
        }

        var file = publishingContentService.Resolve(
            CurrentProjectDirectory,
            kind);

        if (IsMarkdownDirty &&
            !discardUnsavedChanges)
        {
            StatusMessage =
                "The current Markdown document has unsaved changes.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            var text = await publishingContentService.ReadAsync(
                CurrentProjectDirectory,
                kind);

            markdownPublishingKind = kind;
            savedMarkdownText = text;
            MarkdownDocumentPath = file.Path;
            MarkdownText = text;
            HasMarkdownDocument = true;
            IsMarkdownDirty = false;
            MarkdownEditorMode = MarkdownEditorMode.Split;
            WorkspaceTabIndex = 2;

            StatusMessage =
                file.Exists
                    ? $"Opened '{Path.GetFileName(file.Path)}'."
                    : $"New publishing file '{Path.GetFileName(file.Path)}' will be created when saved.";
        });
    }

    private string GetPublishingStatus(
        PublishingContentKind kind)
    {
        if (CurrentProjectDirectory is null)
        {
            return "No project open";
        }

        try
        {
            return publishingContentService
                .Resolve(
                    CurrentProjectDirectory,
                    kind)
                .Exists
                ? "Created"
                : "Not created";
        }
        catch
        {
            return "Unavailable";
        }
    }

    private string GetPublishingActionText(
        PublishingContentKind kind,
        string label) =>
        GetPublishingStatus(kind) == "Created"
            ? $"Edit {label}"
            : $"Create {label}";

    private void RefreshPublishingMedia()
    {
        PublishingLogoFileName = string.Empty;
        PublishingScreenshots.Clear();

        if (CurrentProjectDirectory is null)
        {
            RaisePublishingMediaProperties();
            return;
        }

        try
        {
            var snapshot =
                projectMediaService.GetSnapshot(
                    CurrentProjectDirectory);

            PublishingLogoFileName =
                snapshot.LogoFilePath is null
                    ? string.Empty
                    : Path.GetFileName(
                        snapshot.LogoFilePath);

            foreach (var screenshotPath in
                     snapshot.ScreenshotFilePaths)
            {
                PublishingScreenshots.Add(
                    Path.GetFileName(
                        screenshotPath));
            }
        }
        catch
        {
            PublishingLogoFileName = string.Empty;
            PublishingScreenshots.Clear();
        }

        RaisePublishingMediaProperties();
    }

    private void RaisePublishingMediaProperties()
    {
        OnPropertyChanged(nameof(HasPublishingLogo));
        OnPropertyChanged(nameof(PublishingLogoStatus));
        OnPropertyChanged(nameof(PublishingLogoActionText));
        OnPropertyChanged(nameof(HasPublishingScreenshots));
        OnPropertyChanged(nameof(PublishingScreenshotsStatus));
    }

    private void RaisePublishingProperties()
    {
        OnPropertyChanged(nameof(PublishingSummaryFileName));
        OnPropertyChanged(nameof(PublishingDescriptionFileName));
        OnPropertyChanged(nameof(PublishingChangelogFileName));
        OnPropertyChanged(nameof(PublishingSummaryStatus));
        OnPropertyChanged(nameof(PublishingDescriptionStatus));
        OnPropertyChanged(nameof(PublishingChangelogStatus));
        OnPropertyChanged(nameof(PublishingSummaryActionText));
        OnPropertyChanged(nameof(PublishingDescriptionActionText));
        OnPropertyChanged(nameof(PublishingChangelogActionText));
        RaisePublishingMediaProperties();
    }

    private void RefreshCurrentProjectTree()
    {
        var project = SelectedProject;

        if (project is null ||
            CurrentProjectDirectory is null ||
            !string.Equals(
                project.ProjectDirectory,
                CurrentProjectDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var selectedPath =
            SelectedProjectTreeItem?.FullPath;

        CurrentProjectTree.Clear();

        foreach (var item in projectExplorerService.BuildTree(project))
        {
            CurrentProjectTree.Add(item);
        }

        SelectedProjectTreeItem =
            string.IsNullOrWhiteSpace(selectedPath)
                ? null
                : FindProjectTreeItem(
                    CurrentProjectTree,
                    selectedPath);
    }

    private static ProjectTreeItem? FindProjectTreeItem(
        IEnumerable<ProjectTreeItem> items,
        string fullPath)
    {
        foreach (var item in items)
        {
            if (string.Equals(
                    Path.GetFullPath(item.FullPath),
                    Path.GetFullPath(fullPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var child =
                FindProjectTreeItem(
                    item.Children,
                    fullPath);

            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private async Task RefreshProjectsCoreAsync()
    {
        var result = await projectCatalogService.DiscoverAsync(
            ProjectRoot);

        Projects.Clear();

        foreach (var project in result.Projects)
        {
            Projects.Add(project);
        }

        UnmanagedFolders.Clear();

        foreach (var folder in result.UnmanagedFolders)
        {
            UnmanagedFolders.Add(folder);
        }

        ProjectCatalogIssues.Clear();

        foreach (var issue in result.Issues)
        {
            ProjectCatalogIssues.Add(
                $"{Path.GetFileName(issue.ProjectDirectory)}: {issue.Message}");
        }

        RaiseProjectCatalogProperties();
    }

    private ProjectCatalogEntry? FindProject(
        string projectDirectory) =>
        Projects.FirstOrDefault(
            project => string.Equals(
                Path.GetFullPath(project.ProjectDirectory),
                Path.GetFullPath(projectDirectory),
                StringComparison.OrdinalIgnoreCase));

    private void RaiseProjectCatalogProperties()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(HasNoProjects));
        OnPropertyChanged(nameof(ManagedProjectCount));
        OnPropertyChanged(nameof(RuntimeAddonCount));
        OnPropertyChanged(nameof(UnmanagedFolderCount));
        OnPropertyChanged(nameof(HasUnmanagedFolders));
        OnPropertyChanged(nameof(HasProjectCatalogIssues));
    }

    private void SetCurrentProject(
        ProjectOperationResult result)
    {
        CurrentProjectName = result.ProjectName;
        CurrentProjectDirectory = result.ProjectDirectory;
        CurrentProjectTypeName = "Addon";
        CurrentPrimaryAddon =
            result.RuntimeAddons.FirstOrDefault();

        CurrentRuntimeAddons.Clear();

        foreach (var runtimeAddon in result.RuntimeAddons)
        {
            CurrentRuntimeAddons.Add(runtimeAddon);
        }
    }

    private async Task RunOperationAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Working ...";
            await operation();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Error: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
