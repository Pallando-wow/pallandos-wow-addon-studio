using System.Collections.ObjectModel;
using AddonStudio.Application.Documents;
using AddonStudio.Application.Projects;
using AddonStudio.Application.Publishing;
using AddonStudio.Application.Settings;
using AddonStudio.Application.WowData;
using AddonStudio.Core.Projects;
using AddonStudio.Core.Publishing;
using AddonStudio.Media;
using AddonStudio.Platforms.CurseForge;
using AddonStudio.Wow.Deployment;
using AddonStudio.Wow.Toc;
using Avalonia.Media.Imaging;
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

public partial class CurseForgeCategoryChoice(
    int id,
    string name,
    string slug) : ObservableObject
{
    public int Id { get; } = id;

    public string Name { get; } = name;

    public string Slug { get; } = slug;

    public event EventHandler? SelectionChanged;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool canSelectAdditional = true;

    partial void OnIsSelectedChanged(bool value) =>
        SelectionChanged?.Invoke(
            this,
            EventArgs.Empty);
}

public sealed class ProjectScreenshotItem(
    string fullPath,
    Bitmap image) : IDisposable
{
    public string FullPath { get; } = fullPath;

    public string FileName { get; } =
        Path.GetFileName(fullPath);

    public Bitmap Image { get; } = image;

    public void Dispose() =>
        Image.Dispose();
}

public partial class MainWindowViewModel(
    AddonProjectService addonProjectService,
    ProjectCurseForgeSettingsService projectCurseForgeSettingsService,
    CurseForgeDataSourceClient curseForgeDataSourceClient,
    ILocalSecretStore localSecretStore,
    IStudioSettingsStore settingsStore,
    ProjectCatalogService projectCatalogService,
    ProjectExplorerService projectExplorerService,
    MarkdownDocumentService markdownDocumentService,
    PublishingContentService publishingContentService,
    ProjectMediaService projectMediaService,
    TocDocumentReader tocDocumentReader,
    IPallandoCollectorReader pallandoCollectorReader,
    WowTestInstallCoordinatorService wowTestInstallCoordinatorService) : ViewModelBase
{
    private const string CurseForgeApiKeySecretName =
        "curseforge-api-key";

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
    private string installFeedbackMessage = string.Empty;

    [ObservableProperty]
    private bool installFeedbackIsSuccess;

    [ObservableProperty]
    private bool installFeedbackIsError;

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
    private string savedVariablesPath = string.Empty;

    [ObservableProperty]
    private string curseForgeApiKey = string.Empty;

    [ObservableProperty]
    private bool curseForgeDataSourceReady;

    [ObservableProperty]
    private string curseForgeDataSourceStatus =
        "Not configured";

    private int curseForgeGameId;
    private string savedCurseForgeApiKey = string.Empty;
    private bool suppressCurseForgeMainCategorySelectionChanged;
    private CurseForgeProject? curseForgeRemoteProject;

    [ObservableProperty]
    private CurseForgeCategoryChoice?
        selectedCurseForgeMainCategory;

    [ObservableProperty]
    private string curseForgeMainCategorySearchText =
        string.Empty;

    [ObservableProperty]
    private string curseForgeAdditionalCategorySearchText =
        string.Empty;

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

    [ObservableProperty]
    private string publishingSummaryText = string.Empty;

    [ObservableProperty]
    private string publishingDescriptionText = string.Empty;

    [ObservableProperty]
    private string publishingChangelogText = string.Empty;

    [ObservableProperty]
    private bool publishingWorkspaceDirty;

    [ObservableProperty]
    private Bitmap? currentProjectLogoImage;

    [ObservableProperty]
    private ProjectScreenshotItem?
        selectedProjectScreenshot;

    public ObservableCollection<string> PublishingScreenshots { get; } = [];

    public ObservableCollection<ProjectScreenshotItem>
        ProjectScreenshots { get; } = [];

    public ObservableCollection<CurseForgeCategoryChoice>
        CurseForgeCategories { get; } = [];

    public ObservableCollection<CurseForgeCategoryChoice>
        FilteredCurseForgeMainCategories { get; } = [];

    public ObservableCollection<CurseForgeCategoryChoice>
        FilteredCurseForgeAdditionalCategories { get; } = [];

    [ObservableProperty]
    private string curseForgeProjectId = string.Empty;

    [ObservableProperty]
    private string curseForgeSlug = string.Empty;

    [ObservableProperty]
    private string curseForgeMainCategoryId = string.Empty;

    [ObservableProperty]
    private string curseForgeAdditionalCategoryIds = string.Empty;

    [ObservableProperty]
    private string curseForgeLicense = string.Empty;

    [ObservableProperty]
    private string curseForgeDistributionSelection =
        "Not configured";

    [ObservableProperty]
    private string currentProjectTocVersion = "—";

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
    private string savedPublishingSummaryText = string.Empty;
    private string savedPublishingDescriptionText = string.Empty;
    private string savedPublishingChangelogText = string.Empty;
    private string? publishingWorkspaceProjectDirectory;
    private bool suppressPublishingWorkspaceDirty;
    private bool publishingChangelogLoadedFromLegacy;
    private ProjectCatalogEntry? currentProject;

    public MainWindowViewModel(
        AddonProjectService addonProjectService,
        ProjectCurseForgeSettingsService projectCurseForgeSettingsService,
        CurseForgeDataSourceClient curseForgeDataSourceClient,
        ILocalSecretStore localSecretStore,
        IStudioSettingsStore settingsStore,
        ProjectCatalogService projectCatalogService,
        ProjectExplorerService projectExplorerService,
        MarkdownDocumentService markdownDocumentService,
        PublishingContentService publishingContentService,
        ProjectMediaService projectMediaService,
        TocDocumentReader tocDocumentReader,
        IPallandoCollectorReader pallandoCollectorReader,
        WowTestInstallCoordinatorService wowTestInstallCoordinatorService,
        bool initialize = true)
        : this(
            addonProjectService,
            projectCurseForgeSettingsService,
            curseForgeDataSourceClient,
            localSecretStore,
            settingsStore,
            projectCatalogService,
            projectExplorerService,
            markdownDocumentService,
            publishingContentService,
            projectMediaService,
            tocDocumentReader,
            pallandoCollectorReader,
            wowTestInstallCoordinatorService)
    {
        var settings = settingsStore.Load();

        projectRoot = settings.ProjectRoot;
        wowForeverAddOnsPath = settings.WowForeverAddOnsPath;
        savedVariablesPath = settings.SavedVariablesPath;

        var protectedCurseForgeApiKey =
            localSecretStore.Load(
                CurseForgeApiKeySecretName);

        if (!string.IsNullOrWhiteSpace(
                protectedCurseForgeApiKey))
        {
            curseForgeApiKey =
                protectedCurseForgeApiKey;
        }
        else if (!string.IsNullOrWhiteSpace(
                     settings.CurseForgeApiKey))
        {
            curseForgeApiKey =
                settings.CurseForgeApiKey.Trim();

            localSecretStore.Save(
                CurseForgeApiKeySecretName,
                curseForgeApiKey);

            // Rewrite settings without the legacy plaintext key.
            settingsStore.Save(settings);
        }

        savedCurseForgeApiKey =
            curseForgeApiKey;

        curseForgeDataSourceStatus =
            string.IsNullOrWhiteSpace(curseForgeApiKey)
                ? "Not configured"
                : "Saved · not connected";
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

    public bool HasCurrentReleaseVersion =>
        !string.IsNullOrWhiteSpace(
            CurrentProjectTocVersion) &&
        CurrentProjectTocVersion != "—";

    public bool HasNoCurrentReleaseVersion =>
        !HasCurrentReleaseVersion;

    public string CurrentReleaseTitle =>
        HasCurrentReleaseVersion
            ? $"Current Release · {CurrentProjectTocVersion}"
            : "Current Release · version unavailable";

    public string PublishingChangelogFileName =>
        HasCurrentReleaseVersion
            ? PublishingContentLayout
                .GetReleaseChangelogRelativePath(
                    CurrentProjectTocVersion)
            : "Version unavailable";

    public string PublishingSummaryStatus =>
        GetPublishingStatus(
            PublishingContentKind.Summary);

    public string PublishingDescriptionStatus =>
        GetPublishingStatus(
            PublishingContentKind.Description);

    public string PublishingChangelogStatus =>
        GetPublishingStatus(
            PublishingContentKind.Changelog);

    public bool PublishingReleaseDirty =>
        !string.Equals(
            PublishingChangelogText,
            savedPublishingChangelogText,
            StringComparison.Ordinal);

    public string PublishingWorkspaceStatus =>
        PublishingWorkspaceDirty
            ? !HasCurrentReleaseVersion &&
              PublishingReleaseDirty
                ? "Unsaved changes · release version unavailable"
                : "Unsaved changes"
            : publishingChangelogLoadedFromLegacy
                ? "Legacy changelog ready to migrate"
                : "All changes saved";

    public bool CanSavePublishingWorkspace =>
        HasCurrentProject &&
        (PublishingWorkspaceDirty ||
         publishingChangelogLoadedFromLegacy) &&
        (!PublishingReleaseDirty ||
         HasCurrentReleaseVersion) &&
        (!publishingChangelogLoadedFromLegacy ||
         HasCurrentReleaseVersion);

    public bool CanRevertPublishingWorkspace =>
        PublishingWorkspaceDirty;

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

    public bool HasCurrentProjectLogoImage =>
        CurrentProjectLogoImage is not null;

    public bool HasNoCurrentProjectLogoImage =>
        !HasCurrentProjectLogoImage;

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

    public bool HasProjectScreenshots =>
        ProjectScreenshots.Count > 0;

    public bool HasNoProjectScreenshots =>
        !HasProjectScreenshots;

    public bool CanShowPreviousProjectScreenshot =>
        SelectedProjectScreenshot is not null &&
        ProjectScreenshots.IndexOf(
            SelectedProjectScreenshot) > 0;

    public bool CanShowNextProjectScreenshot =>
        SelectedProjectScreenshot is not null &&
        ProjectScreenshots.IndexOf(
            SelectedProjectScreenshot) >= 0 &&
        ProjectScreenshots.IndexOf(
            SelectedProjectScreenshot) <
            ProjectScreenshots.Count - 1;

    public string SelectedProjectScreenshotName =>
        SelectedProjectScreenshot?.FileName ??
        "No screenshot selected";

    public Bitmap? SelectedProjectScreenshotImage =>
        SelectedProjectScreenshot?.Image;

    public bool IsCurseForgeDataSourceConfigured =>
        !string.IsNullOrWhiteSpace(
            CurseForgeApiKey);

    public bool CurseForgeApiKeyDirty =>
        !string.Equals(
            CurseForgeApiKey.Trim(),
            savedCurseForgeApiKey,
            StringComparison.Ordinal);

    public bool CanSaveCurseForgeApiKey =>
        CurseForgeApiKeyDirty;

    public string CurseForgeApiKeyStorageStatus =>
        string.IsNullOrWhiteSpace(
            savedCurseForgeApiKey)
            ? "API key not saved"
            : CurseForgeApiKeyDirty
                ? "Unsaved API key changes"
                : "API key saved securely for this Windows user";

    public bool CanUseCurseForgeProjectSettings =>
        CurseForgeDataSourceReady;

    public bool HasCurseForgeCategories =>
        CurseForgeCategories.Count > 0;

    public string CurseForgeAdditionalCategorySelectionStatus =>
        $"{CurseForgeCategories.Count(category => category.IsSelected)} / 4 selected";

    public IReadOnlyList<string> CurseForgeDistributionOptions { get; } =
    [
        "Not configured",
        "Allowed",
        "Blocked"
    ];

    public string CurseForgeProjectName =>
        CurrentProjectName ?? "—";

    public bool CurseForgeProjectConnected =>
        curseForgeRemoteProject is not null;

    public string CurseForgeRemoteProjectName =>
        curseForgeRemoteProject?.Name ?? "—";

    public string CurseForgeRemoteProjectSlug =>
        curseForgeRemoteProject?.Slug ?? "—";

    public string CurseForgeRemoteProjectStatus =>
        curseForgeRemoteProject?.StatusName ??
        "Not loaded";

    public string CurseForgeRemoteMainCategory =>
        curseForgeRemoteProject?.PrimaryCategory is
            { } primaryCategory
            ? $"{primaryCategory.Name} · #{primaryCategory.Id}"
            : curseForgeRemoteProject is null
                ? "Not loaded"
                : $"Category #{curseForgeRemoteProject.PrimaryCategoryId}";

    public bool CanLoadCurseForgeProject =>
        CurseForgeDataSourceReady &&
        (!string.IsNullOrWhiteSpace(
             CurseForgeProjectId) ||
         !string.IsNullOrWhiteSpace(
             CurseForgeSlug));

    public string CurseForgeBindingStatus =>
        curseForgeRemoteProject is not null
            ? $"Connected · {curseForgeRemoteProject.Name} · #{curseForgeRemoteProject.Id}"
            : string.IsNullOrWhiteSpace(CurseForgeProjectId) &&
              string.IsNullOrWhiteSpace(CurseForgeSlug)
                ? "Not linked"
                : "Configured locally · not verified";

    public string CurseForgeCategoryComparisonStatus
    {
        get
        {
            if (curseForgeRemoteProject is null)
            {
                return "Not checked";
            }

            if (SelectedCurseForgeMainCategory is null)
            {
                return "Local classification incomplete";
            }

            var remoteMainCategory =
                ResolveLocalCurseForgeMainCategory(
                    curseForgeRemoteProject);

            if (remoteMainCategory is null)
            {
                return "Remote main category is not available locally";
            }

            var remoteAdditionalIds =
                curseForgeRemoteProject.Categories
                    .Where(category =>
                        category.Id !=
                            curseForgeRemoteProject
                                .PrimaryCategoryId)
                    .Select(
                        ResolveLocalCurseForgeCategory)
                    .OfType<CurseForgeCategoryChoice>()
                    .Where(category =>
                        category.Id !=
                            remoteMainCategory.Id)
                    .Select(category =>
                        category.Id)
                    .ToHashSet();

            var localAdditionalIds =
                CurseForgeCategories
                    .Where(category =>
                        category.IsSelected)
                    .Select(category =>
                        category.Id)
                    .ToHashSet();

            return remoteMainCategory.Id ==
                       SelectedCurseForgeMainCategory.Id &&
                   remoteAdditionalIds.SetEquals(
                       localAdditionalIds)
                ? "Matches CurseForge"
                : "Differs from CurseForge";
        }
    }

    public string CurseForgeScreenshotsStatus =>
        HasPublishingScreenshots
            ? $"Ready · {PublishingScreenshots.Count} screenshot(s)"
            : "Optional · none";

    public string CurseForgeReadinessBinding =>
        CurseForgeProjectConnected
            ? "Ready"
            : "Missing";

    public string CurseForgeReadinessSummary =>
        PublishingSummaryStatus == "Created"
            ? "Ready"
            : "Missing";

    public string CurseForgeReadinessDescription =>
        PublishingDescriptionStatus == "Created"
            ? "Ready"
            : "Missing";

    public string CurseForgeReadinessLogo =>
        HasPublishingLogo
            ? "Ready"
            : "Missing";

    public string CurseForgeReadinessVersion =>
        HasCurrentReleaseVersion
            ? $"Ready · {CurrentProjectTocVersion}"
            : "Missing";

    public string CurseForgeReadinessChangelog =>
        PublishingChangelogStatus == "Created"
            ? "Ready"
            : PublishingChangelogStatus.StartsWith(
                "Legacy",
                StringComparison.Ordinal)
                ? "Needs migration"
                : "Missing";

    public string CurseForgeReadinessMainCategory =>
        SelectedCurseForgeMainCategory is not null
            ? $"Ready · {SelectedCurseForgeMainCategory.Name}"
            : "Missing";

    public string CurseForgeReadinessLicense =>
        string.IsNullOrWhiteSpace(
            CurseForgeLicense)
            ? "Missing"
            : $"Ready · {CurseForgeLicense.Trim()}";

    public string CurseForgeReadinessDistribution =>
        CurseForgeDistributionSelection == "Not configured"
            ? "Missing"
            : $"Ready · {CurseForgeDistributionSelection}";

    public int CurseForgeReadinessIssuesCount =>
        new[]
        {
            CurseForgeReadinessBinding,
            CurseForgeReadinessSummary,
            CurseForgeReadinessDescription,
            CurseForgeReadinessLogo,
            CurseForgeReadinessVersion,
            CurseForgeReadinessChangelog,
            CurseForgeReadinessMainCategory,
            CurseForgeReadinessLicense,
            CurseForgeReadinessDistribution
        }.Count(status =>
            status == "Missing" ||
            status == "Needs migration");

    public string CurseForgePublishingReadiness =>
        CurseForgeReadinessIssuesCount == 0
            ? "Ready for release preparation"
            : $"{CurseForgeReadinessIssuesCount} item(s) need attention";

    public string ProjectDashboardVersion =>
        HasSelectedProjectTocMetadata
            ? SelectedProjectTocVersion
            : "—";

    public string ProjectDashboardAuthor =>
        HasSelectedProjectTocMetadata
            ? SelectedProjectTocAuthor
            : "—";

    public string ProjectDashboardInterfaces =>
        HasSelectedProjectTocMetadata
            ? SelectedProjectTocInterfaces
            : "—";

    public string ProjectDashboardComponents =>
        currentProject is null ||
        currentProject.Manifest.Components.Count == 0
            ? "None"
            : string.Join(
                ", ",
                currentProject.Manifest.Components
                    .Select(component =>
                        component.Id));

    public string ProjectDashboardCurseForgeStatus =>
        !CurseForgeDataSourceReady
            ? "Data source not connected"
            : CurseForgeBindingStatus;

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
    private async Task ShowPublishing()
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

        await LoadPublishingWorkspaceAsync();
    }

    [RelayCommand]
    private async Task ShowCurseForge()
    {
        if (SetupRequired ||
            !HasCurrentProject)
        {
            return;
        }

        Sidebar = StudioSidebar.CurseForge;
        WorkspaceTabIndex = 0;
        RefreshPublishingMedia();
        RaisePublishingProperties();

        if (!CurseForgeDataSourceReady &&
            IsCurseForgeDataSourceConfigured &&
            !CurseForgeApiKeyDirty)
        {
            await TestCurseForgeDataSourceAsync();
            return;
        }

        if (CurseForgeDataSourceReady &&
            CanLoadCurseForgeProject &&
            !CurseForgeProjectConnected)
        {
            await LoadCurseForgeProjectAsync();
        }
    }

    [RelayCommand]
    private async Task SaveCurseForgeSettingsAsync()
    {
        if (currentProject is null ||
            CurrentProjectDirectory is null)
        {
            StatusMessage =
                "Open a project before editing CurseForge settings.";
            return;
        }

        if (!CurseForgeDataSourceReady)
        {
            StatusMessage =
                "Connect the CurseForge data source before editing CurseForge settings.";
            return;
        }

        var additionalCategories =
            CurseForgeCategories
                .Where(category =>
                    category.IsSelected)
                .Select(category =>
                    category.Id.ToString())
                .ToArray();

        if (additionalCategories.Length > 4)
        {
            StatusMessage =
                "CurseForge supports at most four additional categories.";
            return;
        }

        if (SelectedCurseForgeMainCategory is not null &&
            additionalCategories.Contains(
                SelectedCurseForgeMainCategory.Id.ToString(),
                StringComparer.OrdinalIgnoreCase))
        {
            StatusMessage =
                "The main category must not also be listed as an additional category.";
            return;
        }

        var allowDistribution =
            CurseForgeDistributionSelection switch
            {
                "Allowed" => true,
                "Blocked" => false,
                _ => (bool?)null
            };

        var configuration =
            new CurseForgeConfiguration
            {
                ProjectId = CurseForgeProjectId,
                Slug = CurseForgeSlug,
                MainCategoryId =
                    SelectedCurseForgeMainCategory?
                        .Id.ToString(),
                AdditionalCategoryIds =
                    additionalCategories,
                License = CurseForgeLicense,
                AllowDistribution =
                    allowDistribution
            };

        await RunOperationAsync(async () =>
        {
            var updatedManifest =
                await projectCurseForgeSettingsService.SaveAsync(
                    CurrentProjectDirectory,
                    currentProject.Manifest,
                    configuration);

            var updatedProject =
                new ProjectCatalogEntry(
                    CurrentProjectDirectory,
                    updatedManifest);

            ReplaceProjectInCatalog(
                updatedProject);

            currentProject = updatedProject;
            LoadCurseForgeSettings(
                updatedManifest.CurseForge);

            RefreshCurrentProjectTree();

            StatusMessage =
                "CurseForge settings saved to project.json.";
        });
    }

    [RelayCommand]
    private async Task LoadCurseForgeProjectAsync()
    {
        if (!CurseForgeDataSourceReady)
        {
            StatusMessage =
                "Connect the CurseForge data source first.";
            return;
        }

        if (!CanLoadCurseForgeProject)
        {
            StatusMessage =
                "Enter a CurseForge Project ID or slug first.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            ClearCurseForgeRemoteProject();

            var project =
                await ResolveConfiguredCurseForgeProjectAsync();

            var classificationInitialized =
                SetCurseForgeRemoteProject(
                    project);

            StatusMessage =
                classificationInitialized
                    ? $"CurseForge project '{project.Name}' connected; local classification initialized from CurseForge."
                    : $"CurseForge project '{project.Name}' connected.";
        });
    }

    [RelayCommand]
    private void SaveCurseForgeApiKey()
    {
        var apiKey =
            CurseForgeApiKey.Trim();

        if (string.IsNullOrWhiteSpace(
                apiKey))
        {
            localSecretStore.Delete(
                CurseForgeApiKeySecretName);

            savedCurseForgeApiKey =
                string.Empty;
            CurseForgeApiKey =
                string.Empty;
            CurseForgeDataSourceReady =
                false;
            CurseForgeDataSourceStatus =
                "Not configured";

            StatusMessage =
                "CurseForge API key removed.";
        }
        else
        {
            localSecretStore.Save(
                CurseForgeApiKeySecretName,
                apiKey);

            savedCurseForgeApiKey =
                apiKey;
            CurseForgeApiKey =
                apiKey;

            if (!CurseForgeDataSourceReady)
            {
                CurseForgeDataSourceStatus =
                    "Saved · not connected";
            }

            StatusMessage =
                "CurseForge API key saved securely.";
        }

        RaiseCurseForgeApiKeyStorageProperties();
    }

    [RelayCommand]
    private async Task TestCurseForgeDataSourceAsync()
    {
        if (!IsCurseForgeDataSourceConfigured)
        {
            CurseForgeDataSourceReady = false;
            CurseForgeDataSourceStatus =
                "Not configured";
            curseForgeGameId = 0;
            ClearCurseForgeCategoryChoices();
            ClearCurseForgeRemoteProject();
            StatusMessage =
                "Enter a CurseForge API key first.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            try
            {
                CurseForgeDataSourceReady = false;
                CurseForgeDataSourceStatus =
                    "Connecting ...";
                curseForgeGameId = 0;
                ClearCurseForgeCategoryChoices();
                ClearCurseForgeRemoteProject();

                var snapshot =
                    await curseForgeDataSourceClient
                        .LoadWorldOfWarcraftAsync(
                            CurseForgeApiKey);

                curseForgeGameId =
                    snapshot.Game.Id;

                var addonClass =
                    snapshot.Categories
                        .FirstOrDefault(category =>
                            category.IsClass &&
                            string.Equals(
                                category.Name,
                                "Addons",
                                StringComparison.OrdinalIgnoreCase));

                var projectCategories =
                    snapshot.Categories
                        .Where(category =>
                            !category.IsClass &&
                            (addonClass is null ||
                             category.ClassId ==
                             addonClass.Id))
                        .OrderBy(category =>
                            category.DisplayIndex)
                        .ThenBy(category =>
                            category.Name,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                foreach (var category in
                         projectCategories)
                {
                    var choice =
                        new CurseForgeCategoryChoice(
                            category.Id,
                            category.Name,
                            category.Slug);

                    choice.SelectionChanged +=
                        CurseForgeCategory_SelectionChanged;

                    CurseForgeCategories.Add(
                        choice);
                }

                RefreshCurseForgeCategoryFilters();

                CurseForgeDataSourceReady = true;
                CurseForgeDataSourceStatus =
                    $"Connected · {snapshot.Game.Name} · " +
                    $"{CurseForgeCategories.Count} categories";

                ApplyCurseForgeCategorySelections();

                if (CanLoadCurseForgeProject)
                {
                    try
                    {
                        var project =
                            await ResolveConfiguredCurseForgeProjectAsync();

                        var classificationInitialized =
                            SetCurseForgeRemoteProject(
                                project);

                        StatusMessage =
                            classificationInitialized
                                ? $"CurseForge connected · {project.Name}; local classification initialized from CurseForge."
                                : $"CurseForge connected · {project.Name}.";
                    }
                    catch (Exception exception)
                        when (exception is
                            HttpRequestException or
                            InvalidDataException or
                            ArgumentException)
                    {
                        ClearCurseForgeRemoteProject();
                        StatusMessage =
                            $"CurseForge data source connected; project lookup failed: {exception.Message}";
                    }
                }
                else
                {
                    StatusMessage =
                        "CurseForge data source connected.";
                }
            }
            catch
            {
                CurseForgeDataSourceReady = false;
                CurseForgeDataSourceStatus =
                    "Connection failed";
                curseForgeGameId = 0;
                ClearCurseForgeCategoryChoices();
                ClearCurseForgeRemoteProject();
                throw;
            }
        });
    }

    private async Task<CurseForgeProject>
        ResolveConfiguredCurseForgeProjectAsync()
    {
        if (curseForgeGameId <= 0)
        {
            throw new InvalidOperationException(
                "The CurseForge World of Warcraft data source is not connected.");
        }

        CurseForgeProject? project;

        if (!string.IsNullOrWhiteSpace(
                CurseForgeProjectId))
        {
            if (!int.TryParse(
                    CurseForgeProjectId.Trim(),
                    out var projectId) ||
                projectId <= 0)
            {
                throw new InvalidDataException(
                    "CurseForge Project ID must be a positive number.");
            }

            project =
                await curseForgeDataSourceClient
                    .GetProjectAsync(
                        CurseForgeApiKey,
                        projectId);
        }
        else
        {
            project =
                await curseForgeDataSourceClient
                    .FindProjectBySlugAsync(
                        CurseForgeApiKey,
                        curseForgeGameId,
                        CurseForgeSlug);

            if (project is null)
            {
                throw new InvalidDataException(
                    $"No CurseForge project with slug '{CurseForgeSlug.Trim()}' was found.");
            }
        }

        if (project.GameId !=
            curseForgeGameId)
        {
            throw new InvalidDataException(
                $"CurseForge project '{project.Name}' does not belong to World of Warcraft.");
        }

        return project;
    }

    private bool SetCurseForgeRemoteProject(
        CurseForgeProject project)
    {
        curseForgeRemoteProject =
            project;

        CurseForgeProjectId =
            project.Id.ToString();
        CurseForgeSlug =
            project.Slug;

        var classificationInitialized =
            ApplyRemoteCurseForgeCategoriesWhenLocalEmpty(
                project);

        RaiseCurseForgeProjectProperties();
        RaiseCurseForgeReadinessProperties();
        RaiseProjectDashboardProperties();

        return classificationInitialized;
    }

    private bool ApplyRemoteCurseForgeCategoriesWhenLocalEmpty(
        CurseForgeProject project)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(
                CurseForgeMainCategoryId))
        {
            var remoteMainCategory =
                ResolveLocalCurseForgeMainCategory(
                    project);

            if (remoteMainCategory is not null)
            {
                CurseForgeMainCategoryId =
                    remoteMainCategory.Id.ToString();
                SelectedCurseForgeMainCategory =
                    remoteMainCategory;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(
                CurseForgeAdditionalCategoryIds))
        {
            var remoteAdditionalCategories =
                project.Categories
                    .Where(category =>
                        category.Id !=
                            project.PrimaryCategoryId)
                    .Select(
                        ResolveLocalCurseForgeCategory)
                    .OfType<CurseForgeCategoryChoice>()
                    .Where(category =>
                        category !=
                            SelectedCurseForgeMainCategory)
                    .DistinctBy(category =>
                        category.Id)
                    .Take(4)
                    .ToArray();

            if (remoteAdditionalCategories.Length > 0)
            {
                foreach (var category in
                         remoteAdditionalCategories)
                {
                    category.IsSelected = true;
                }

                CurseForgeAdditionalCategoryIds =
                    string.Join(
                        ", ",
                        remoteAdditionalCategories
                            .Select(category =>
                                category.Id));

                changed = true;
            }
        }

        if (changed)
        {
            RefreshCurseForgeCategoryFilters();
            OnPropertyChanged(
                nameof(CurseForgeCategoryComparisonStatus));
            RaiseCurseForgeReadinessProperties();
        }

        return changed;
    }

    private CurseForgeCategoryChoice?
        ResolveLocalCurseForgeMainCategory(
            CurseForgeProject project)
    {
        if (project.PrimaryCategory is
            { } primaryCategory)
        {
            var resolved =
                ResolveLocalCurseForgeCategory(
                    primaryCategory);

            if (resolved is not null)
            {
                return resolved;
            }
        }

        var directById =
            CurseForgeCategories.FirstOrDefault(
                category =>
                    category.Id ==
                    project.PrimaryCategoryId);

        if (directById is not null)
        {
            return directById;
        }

        var matchingCategories =
            project.Categories
                .Select(
                    ResolveLocalCurseForgeCategory)
                .OfType<CurseForgeCategoryChoice>()
                .DistinctBy(category =>
                    category.Id)
                .ToArray();

        return matchingCategories.Length == 1
            ? matchingCategories[0]
            : null;
    }

    private CurseForgeCategoryChoice?
        ResolveLocalCurseForgeCategory(
            CurseForgeProjectCategory remoteCategory)
    {
        var byId =
            CurseForgeCategories.FirstOrDefault(
                category =>
                    category.Id ==
                    remoteCategory.Id);

        if (byId is not null)
        {
            return byId;
        }

        if (!string.IsNullOrWhiteSpace(
                remoteCategory.Slug))
        {
            var bySlug =
                CurseForgeCategories.FirstOrDefault(
                    category =>
                        string.Equals(
                            category.Slug,
                            remoteCategory.Slug,
                            StringComparison.OrdinalIgnoreCase));

            if (bySlug is not null)
            {
                return bySlug;
            }
        }

        if (!string.IsNullOrWhiteSpace(
                remoteCategory.Name))
        {
            return CurseForgeCategories.FirstOrDefault(
                category =>
                    string.Equals(
                        category.Name,
                        remoteCategory.Name,
                        StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private void ClearCurseForgeRemoteProject()
    {
        curseForgeRemoteProject = null;
        RaiseCurseForgeProjectProperties();
        RaiseCurseForgeReadinessProperties();
        RaiseProjectDashboardProperties();
    }

    private void RaiseCurseForgeProjectProperties()
    {
        OnPropertyChanged(
            nameof(CurseForgeProjectConnected));
        OnPropertyChanged(
            nameof(CurseForgeRemoteProjectName));
        OnPropertyChanged(
            nameof(CurseForgeRemoteProjectSlug));
        OnPropertyChanged(
            nameof(CurseForgeRemoteProjectStatus));
        OnPropertyChanged(
            nameof(CurseForgeRemoteMainCategory));
        OnPropertyChanged(
            nameof(CanLoadCurseForgeProject));
        OnPropertyChanged(
            nameof(CurseForgeBindingStatus));
        OnPropertyChanged(
            nameof(CurseForgeCategoryComparisonStatus));
    }

    private void RaiseCurseForgeReadinessProperties()
    {
        OnPropertyChanged(
            nameof(CurseForgeScreenshotsStatus));
        OnPropertyChanged(
            nameof(CurseForgeReadinessBinding));
        OnPropertyChanged(
            nameof(CurseForgeReadinessSummary));
        OnPropertyChanged(
            nameof(CurseForgeReadinessDescription));
        OnPropertyChanged(
            nameof(CurseForgeReadinessLogo));
        OnPropertyChanged(
            nameof(CurseForgeReadinessVersion));
        OnPropertyChanged(
            nameof(CurseForgeReadinessChangelog));
        OnPropertyChanged(
            nameof(CurseForgeReadinessMainCategory));
        OnPropertyChanged(
            nameof(CurseForgeReadinessLicense));
        OnPropertyChanged(
            nameof(CurseForgeReadinessDistribution));
        OnPropertyChanged(
            nameof(CurseForgeReadinessIssuesCount));
        OnPropertyChanged(
            nameof(CurseForgePublishingReadiness));
    }

    private void RaiseCurseForgeApiKeyStorageProperties()
    {
        OnPropertyChanged(
            nameof(CurseForgeApiKeyDirty));
        OnPropertyChanged(
            nameof(CanSaveCurseForgeApiKey));
        OnPropertyChanged(
            nameof(CurseForgeApiKeyStorageStatus));
    }

    private void ClearCurseForgeCategoryChoices()
    {
        foreach (var category in
                 CurseForgeCategories)
        {
            category.SelectionChanged -=
                CurseForgeCategory_SelectionChanged;
        }

        CurseForgeCategories.Clear();
        FilteredCurseForgeMainCategories.Clear();
        FilteredCurseForgeAdditionalCategories.Clear();
        SelectedCurseForgeMainCategory = null;

        OnPropertyChanged(
            nameof(HasCurseForgeCategories));
        OnPropertyChanged(
            nameof(CurseForgeAdditionalCategorySelectionStatus));
    }

    private void CurseForgeCategory_SelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is not CurseForgeCategoryChoice category)
        {
            return;
        }

        var selectedCount =
            CurseForgeCategories.Count(
                choice =>
                    choice.IsSelected);

        if (category.IsSelected &&
            selectedCount > 4)
        {
            category.IsSelected = false;
            StatusMessage =
                "CurseForge supports at most four additional categories.";
            return;
        }

        UpdateCurseForgeAdditionalCategoryAvailability();
        OnPropertyChanged(
            nameof(CurseForgeAdditionalCategorySelectionStatus));
        OnPropertyChanged(
            nameof(CurseForgeCategoryComparisonStatus));
        RaiseCurseForgeReadinessProperties();
    }

    private void RefreshCurseForgeCategoryFilters()
    {
        var selectedMainCategory =
            SelectedCurseForgeMainCategory ??
            CurseForgeCategories.FirstOrDefault(
                category =>
                    string.Equals(
                        category.Id.ToString(),
                        CurseForgeMainCategoryId,
                        StringComparison.OrdinalIgnoreCase));

        suppressCurseForgeMainCategorySelectionChanged =
            true;

        try
        {
            FilteredCurseForgeMainCategories.Clear();

            foreach (var category in
                     CurseForgeCategories.Where(category =>
                         MatchesCategorySearch(
                             category,
                             CurseForgeMainCategorySearchText)))
            {
                FilteredCurseForgeMainCategories.Add(
                    category);
            }

            SelectedCurseForgeMainCategory =
                selectedMainCategory is not null &&
                FilteredCurseForgeMainCategories.Contains(
                    selectedMainCategory)
                    ? selectedMainCategory
                    : null;
        }
        finally
        {
            suppressCurseForgeMainCategorySelectionChanged =
                false;
        }

        FilteredCurseForgeAdditionalCategories.Clear();

        foreach (var category in
                 CurseForgeCategories.Where(category =>
                     category !=
                         SelectedCurseForgeMainCategory &&
                     MatchesCategorySearch(
                         category,
                         CurseForgeAdditionalCategorySearchText)))
        {
            FilteredCurseForgeAdditionalCategories.Add(
                category);
        }

        UpdateCurseForgeAdditionalCategoryAvailability();
    }

    private void UpdateCurseForgeAdditionalCategoryAvailability()
    {
        var selectedCount =
            CurseForgeCategories.Count(
                category =>
                    category.IsSelected);

        foreach (var category in
                 CurseForgeCategories)
        {
            category.CanSelectAdditional =
                category.IsSelected ||
                selectedCount < 4;
        }

        OnPropertyChanged(
            nameof(CurseForgeAdditionalCategorySelectionStatus));
    }

    private static bool MatchesCategorySearch(
        CurseForgeCategoryChoice category,
        string searchText) =>
        string.IsNullOrWhiteSpace(searchText) ||
        category.Name.Contains(
            searchText.Trim(),
            StringComparison.OrdinalIgnoreCase);

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

        CollectorSourceFile =
            string.IsNullOrWhiteSpace(
                SavedVariablesPath)
                ? string.Empty
                : PallandoCollectorSource
                    .ResolveFromSavedVariablesDirectory(
                        SavedVariablesPath);
        ClearCollectorImport();
        ActionTabTitle = "Collector Data";
        IsCreateAddonAction = false;
        IsImportAddonAction = false;
        IsImportCollectorAction = true;
        HasActionTab = true;
        WorkspaceTabIndex = 1;
    }

    [RelayCommand]
    private async Task InstallForTestingAsync()
    {
        InstallFeedbackMessage =
            string.Empty;
        InstallFeedbackIsSuccess =
            false;
        InstallFeedbackIsError =
            false;

        if (SetupRequired)
        {
            ShowSettings();
            return;
        }

        if (currentProject is null ||
            string.IsNullOrWhiteSpace(
                CurrentProjectDirectory))
        {
            const string message =
                "Open a project before installing it for testing.";

            StatusMessage =
                message;
            InstallFeedbackMessage =
                message;
            InstallFeedbackIsError =
                true;
            return;
        }

        await RunOperationAsync(async () =>
        {
            try
            {
                var settings =
                    settingsStore.Load();

                var result =
                    await wowTestInstallCoordinatorService.InstallAsync(
                        CurrentProjectDirectory,
                        currentProject.Manifest,
                        settings,
                        cleanTest: false);

                var installedAddons =
                    string.Join(
                        ", ",
                        result.InstalledAddons);

                StatusMessage =
                    $"Installed for testing: {installedAddons}. SavedVariables kept.";

                InstallFeedbackMessage =
                    $"Installed: {installedAddons}. SavedVariables kept.";
                InstallFeedbackIsSuccess =
                    true;
            }
            catch (Exception exception)
            {
                InstallFeedbackMessage =
                    exception.Message;
                InstallFeedbackIsError =
                    true;
                throw;
            }
        });
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
            WowForeverAddOnsPath = WowForeverAddOnsPath.Trim(),
            SavedVariablesPath = SavedVariablesPath.Trim(),
            CurseForgeApiKey = CurseForgeApiKey.Trim()
        };

        var issues = StudioSettingsValidator.Validate(settings);

        if (issues.Count > 0)
        {
            StatusMessage = $"Settings error: {issues[0]}";
            SetupRequired = true;
            return;
        }

        settingsStore.Save(settings);

        if (string.IsNullOrWhiteSpace(
                settings.CurseForgeApiKey))
        {
            localSecretStore.Delete(
                CurseForgeApiKeySecretName);
        }
        else
        {
            localSecretStore.Save(
                CurseForgeApiKeySecretName,
                settings.CurseForgeApiKey);
        }

        ProjectRoot = Path.GetFullPath(settings.ProjectRoot);
        WowForeverAddOnsPath = Path.GetFullPath(settings.WowForeverAddOnsPath);
        SavedVariablesPath =
            string.IsNullOrWhiteSpace(
                settings.SavedVariablesPath)
                ? string.Empty
                : Path.GetFullPath(
                    settings.SavedVariablesPath);
        savedCurseForgeApiKey =
            settings.CurseForgeApiKey;
        CurseForgeApiKey =
            settings.CurseForgeApiKey;
        RaiseCurseForgeApiKeyStorageProperties();
        SetupRequired = false;
        Sidebar = StudioSidebar.Start;
        WorkspaceTabIndex = 0;

        await RefreshProjectsAsync();

        StatusMessage = "Settings saved";
    }

    public void ResetSettings()
    {
        settingsStore.Delete();
        localSecretStore.DeleteAll();

        ProjectRoot = string.Empty;
        WowForeverAddOnsPath = string.Empty;
        SavedVariablesPath = string.Empty;
        CollectorSourceFile = string.Empty;
        savedCurseForgeApiKey = string.Empty;
        CurseForgeApiKey = string.Empty;
        RaiseCurseForgeApiKeyStorageProperties();
        CurseForgeDataSourceReady = false;
        CurseForgeDataSourceStatus = "Not configured";
        ClearCurseForgeCategoryChoices();
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
        ClearPublishingWorkspace();
        CurrentProjectLogoImage?.Dispose();
        CurrentProjectLogoImage = null;
        DisposeProjectScreenshots();

        SelectedProject = null;
        currentProject = null;
        LoadCurseForgeSettings(null);
        CurrentProjectTocVersion = "—";
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
    private async Task SavePublishingWorkspaceAsync()
    {
        if ((!PublishingWorkspaceDirty &&
             !publishingChangelogLoadedFromLegacy) ||
            CurrentProjectDirectory is null)
        {
            return;
        }

        if ((PublishingReleaseDirty ||
             publishingChangelogLoadedFromLegacy) &&
            !HasCurrentReleaseVersion)
        {
            StatusMessage =
                "The .toc version is required before saving the release changelog.";
            return;
        }

        await RunOperationAsync(async () =>
        {
            if (!string.Equals(
                    PublishingSummaryText,
                    savedPublishingSummaryText,
                    StringComparison.Ordinal))
            {
                await publishingContentService.WriteAsync(
                    CurrentProjectDirectory,
                    PublishingContentKind.Summary,
                    PublishingSummaryText);
            }

            if (!string.Equals(
                    PublishingDescriptionText,
                    savedPublishingDescriptionText,
                    StringComparison.Ordinal))
            {
                await publishingContentService.WriteAsync(
                    CurrentProjectDirectory,
                    PublishingContentKind.Description,
                    PublishingDescriptionText);
            }

            if (PublishingReleaseDirty ||
                publishingChangelogLoadedFromLegacy)
            {
                await publishingContentService
                    .WriteReleaseChangelogAsync(
                        CurrentProjectDirectory,
                        CurrentProjectTocVersion,
                        PublishingChangelogText,
                        removeLegacyFile: true);

                publishingChangelogLoadedFromLegacy =
                    false;
            }

            savedPublishingSummaryText =
                PublishingSummaryText;
            savedPublishingDescriptionText =
                PublishingDescriptionText;
            savedPublishingChangelogText =
                PublishingChangelogText;
            PublishingWorkspaceDirty = false;

            RefreshCurrentProjectTree();
            RaisePublishingProperties();

            if (SelectedProject is not null &&
                string.Equals(
                    SelectedProject.ProjectDirectory,
                    CurrentProjectDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadSelectedProjectPreviewMetadataAsync(
                    SelectedProject);
            }

            StatusMessage =
                "Publishing content saved.";
        });
    }

    [RelayCommand]
    private void RevertPublishingWorkspace()
    {
        suppressPublishingWorkspaceDirty = true;

        try
        {
            PublishingSummaryText =
                savedPublishingSummaryText;
            PublishingDescriptionText =
                savedPublishingDescriptionText;
            PublishingChangelogText =
                savedPublishingChangelogText;
        }
        finally
        {
            suppressPublishingWorkspaceDirty = false;
        }

        PublishingWorkspaceDirty = false;
        StatusMessage =
            "Publishing changes reverted.";
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

    public async Task RemovePublishingScreenshotAsync(
        ProjectScreenshotItem screenshot)
    {
        ArgumentNullException.ThrowIfNull(
            screenshot);

        if (CurrentProjectDirectory is null)
        {
            StatusMessage =
                "Open a project before removing screenshots.";
            return;
        }

        var fileName =
            screenshot.FileName;

        await RunOperationAsync(() =>
        {
            projectMediaService.RemoveScreenshot(
                CurrentProjectDirectory,
                screenshot.FullPath);

            RefreshPublishingMedia();
            RefreshCurrentProjectTree();

            StatusMessage =
                $"Screenshot '{fileName}' removed.";

            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private void ShowPreviousProjectScreenshot()
    {
        if (SelectedProjectScreenshot is null)
        {
            return;
        }

        var index =
            ProjectScreenshots.IndexOf(
                SelectedProjectScreenshot);

        if (index > 0)
        {
            SelectedProjectScreenshot =
                ProjectScreenshots[index - 1];
        }
    }

    [RelayCommand]
    private void ShowNextProjectScreenshot()
    {
        if (SelectedProjectScreenshot is null)
        {
            return;
        }

        var index =
            ProjectScreenshots.IndexOf(
                SelectedProjectScreenshot);

        if (index >= 0 &&
            index < ProjectScreenshots.Count - 1)
        {
            SelectedProjectScreenshot =
                ProjectScreenshots[index + 1];
        }
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
                if (kind == PublishingContentKind.Changelog)
                {
                    if (!HasCurrentReleaseVersion)
                    {
                        throw new InvalidOperationException(
                            "The .toc version is required before saving the release changelog.");
                    }

                    await publishingContentService
                        .WriteReleaseChangelogAsync(
                            CurrentProjectDirectory,
                            CurrentProjectTocVersion,
                            MarkdownText,
                            removeLegacyFile: true);

                    publishingChangelogLoadedFromLegacy =
                        false;
                }
                else
                {
                    await publishingContentService.WriteAsync(
                        CurrentProjectDirectory,
                        kind,
                        MarkdownText);
                }

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

    public async Task OpenProjectAsync(
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
            return;
        }

        if (changesProject && PublishingWorkspaceDirty)
        {
            StatusMessage =
                "Save or revert the Publishing changes before switching projects.";
            return;
        }

        if (changesProject)
        {
            ClearMarkdownDocument();
            ClearPublishingWorkspace();
        }

        InstallFeedbackMessage =
            string.Empty;
        InstallFeedbackIsSuccess =
            false;
        InstallFeedbackIsError =
            false;

        currentProject = project;
        CurrentProjectName = project.Name;
        CurrentProjectDirectory = project.ProjectDirectory;
        CurrentProjectTypeName = project.TypeName;
        CurrentPrimaryAddon = project.PrimaryAddon;
        LoadCurseForgeSettings(
            project.Manifest.CurseForge);
        CurrentProjectTocVersion = "—";
        await LoadCurrentProjectTocVersionAsync(
            project);

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
        RefreshPublishingMedia();
        RaisePublishingProperties();
        RaiseProjectDashboardProperties();
        Sidebar = StudioSidebar.ProjectOverview;
        WorkspaceTabIndex = 0;
        StatusMessage = $"Project '{project.Name}' opened.";
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
        RaiseProjectDashboardProperties();

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
            RaiseProjectDashboardProperties();
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

    partial void OnPublishingSummaryTextChanged(
        string value) =>
        UpdatePublishingWorkspaceDirty();

    partial void OnPublishingDescriptionTextChanged(
        string value) =>
        UpdatePublishingWorkspaceDirty();

    partial void OnPublishingChangelogTextChanged(
        string value) =>
        UpdatePublishingWorkspaceDirty();

    partial void OnPublishingWorkspaceDirtyChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(PublishingWorkspaceStatus));
        OnPropertyChanged(
            nameof(CanSavePublishingWorkspace));
        OnPropertyChanged(
            nameof(CanRevertPublishingWorkspace));
        OnPropertyChanged(
            nameof(PublishingReleaseDirty));
    }

    private void UpdatePublishingWorkspaceDirty()
    {
        if (suppressPublishingWorkspaceDirty)
        {
            return;
        }

        PublishingWorkspaceDirty =
            !string.Equals(
                PublishingSummaryText,
                savedPublishingSummaryText,
                StringComparison.Ordinal) ||
            !string.Equals(
                PublishingDescriptionText,
                savedPublishingDescriptionText,
                StringComparison.Ordinal) ||
            !string.Equals(
                PublishingChangelogText,
                savedPublishingChangelogText,
                StringComparison.Ordinal);

        OnPropertyChanged(
            nameof(PublishingReleaseDirty));
        OnPropertyChanged(
            nameof(PublishingWorkspaceStatus));
        OnPropertyChanged(
            nameof(CanSavePublishingWorkspace));
    }

    partial void OnPublishingLogoFileNameChanged(
        string value)
    {
        OnPropertyChanged(nameof(HasPublishingLogo));
        OnPropertyChanged(nameof(PublishingLogoStatus));
        OnPropertyChanged(nameof(PublishingLogoActionText));
    }

    partial void OnCurrentProjectLogoImageChanged(
        Bitmap? value)
    {
        OnPropertyChanged(
            nameof(HasCurrentProjectLogoImage));
        OnPropertyChanged(
            nameof(HasNoCurrentProjectLogoImage));
    }

    partial void OnSelectedProjectScreenshotChanged(
        ProjectScreenshotItem? value)
    {
        OnPropertyChanged(
            nameof(CanShowPreviousProjectScreenshot));
        OnPropertyChanged(
            nameof(CanShowNextProjectScreenshot));
        OnPropertyChanged(
            nameof(SelectedProjectScreenshotName));
        OnPropertyChanged(
            nameof(SelectedProjectScreenshotImage));
    }

    partial void OnCurseForgeApiKeyChanged(
        string value)
    {
        CurseForgeDataSourceReady = false;
        curseForgeGameId = 0;
        ClearCurseForgeCategoryChoices();
        ClearCurseForgeRemoteProject();
        CurseForgeDataSourceStatus =
            string.IsNullOrWhiteSpace(value)
                ? "Not configured"
                : CurseForgeApiKeyDirty
                    ? "Unsaved · not connected"
                    : "Saved · not connected";

        OnPropertyChanged(
            nameof(IsCurseForgeDataSourceConfigured));
        OnPropertyChanged(
            nameof(CanUseCurseForgeProjectSettings));
        OnPropertyChanged(
            nameof(HasCurseForgeCategories));
        OnPropertyChanged(
            nameof(CanLoadCurseForgeProject));
        RaiseCurseForgeApiKeyStorageProperties();
    }

    partial void OnCurseForgeDataSourceReadyChanged(
        bool value)
    {
        OnPropertyChanged(
            nameof(CanUseCurseForgeProjectSettings));
        OnPropertyChanged(
            nameof(CanLoadCurseForgeProject));
        OnPropertyChanged(
            nameof(ProjectDashboardCurseForgeStatus));
        RaiseCurseForgeReadinessProperties();
    }

    partial void OnSelectedCurseForgeMainCategoryChanged(
        CurseForgeCategoryChoice? value)
    {
        if (suppressCurseForgeMainCategorySelectionChanged)
        {
            return;
        }

        if (value?.IsSelected == true)
        {
            value.IsSelected = false;
        }

        if (value is not null &&
            !string.IsNullOrWhiteSpace(
                CurseForgeMainCategorySearchText))
        {
            CurseForgeMainCategorySearchText =
                string.Empty;
            return;
        }

        RefreshCurseForgeCategoryFilters();
        OnPropertyChanged(
            nameof(CurseForgeCategoryComparisonStatus));
        RaiseCurseForgeReadinessProperties();
    }

    partial void OnCurseForgeMainCategorySearchTextChanged(
        string value) =>
        RefreshCurseForgeCategoryFilters();

    partial void OnCurseForgeAdditionalCategorySearchTextChanged(
        string value) =>
        RefreshCurseForgeCategoryFilters();

    partial void OnCurseForgeProjectIdChanged(
        string value)
    {
        if (curseForgeRemoteProject is not null &&
            !string.Equals(
                curseForgeRemoteProject.Id.ToString(),
                value.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            ClearCurseForgeRemoteProject();
        }

        OnPropertyChanged(
            nameof(CanLoadCurseForgeProject));
        OnPropertyChanged(
            nameof(CurseForgeBindingStatus));
        RaiseCurseForgeReadinessProperties();
        RaiseProjectDashboardProperties();
    }

    partial void OnCurseForgeSlugChanged(
        string value)
    {
        if (curseForgeRemoteProject is not null &&
            !string.Equals(
                curseForgeRemoteProject.Slug,
                value.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            ClearCurseForgeRemoteProject();
        }

        OnPropertyChanged(
            nameof(CanLoadCurseForgeProject));
        OnPropertyChanged(
            nameof(CurseForgeBindingStatus));
        RaiseCurseForgeReadinessProperties();
        RaiseProjectDashboardProperties();
    }

    partial void OnCurseForgeLicenseChanged(
        string value) =>
        RaiseCurseForgeReadinessProperties();

    partial void OnCurseForgeDistributionSelectionChanged(
        string value) =>
        RaiseCurseForgeReadinessProperties();

    partial void OnCurrentProjectTocVersionChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(HasCurrentReleaseVersion));
        OnPropertyChanged(
            nameof(HasNoCurrentReleaseVersion));
        OnPropertyChanged(
            nameof(CurrentReleaseTitle));
        OnPropertyChanged(
            nameof(PublishingChangelogFileName));
        OnPropertyChanged(
            nameof(PublishingChangelogStatus));
        OnPropertyChanged(
            nameof(PublishingWorkspaceStatus));
        OnPropertyChanged(
            nameof(CanSavePublishingWorkspace));
        RaiseCurseForgeReadinessProperties();
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
        OnPropertyChanged(nameof(CurseForgeProjectName));
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

    private void LoadCurseForgeSettings(
        CurseForgeConfiguration? configuration)
    {
        if (curseForgeRemoteProject is not null)
        {
            var configuredProjectId =
                configuration?.ProjectId?.Trim();
            var configuredSlug =
                configuration?.Slug?.Trim();

            if ((!string.IsNullOrWhiteSpace(
                     configuredProjectId) &&
                 !string.Equals(
                     configuredProjectId,
                     curseForgeRemoteProject.Id.ToString(),
                     StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(
                     configuredSlug) &&
                 !string.Equals(
                     configuredSlug,
                     curseForgeRemoteProject.Slug,
                     StringComparison.OrdinalIgnoreCase)) ||
                (string.IsNullOrWhiteSpace(
                     configuredProjectId) &&
                 string.IsNullOrWhiteSpace(
                     configuredSlug)))
            {
                ClearCurseForgeRemoteProject();
            }
        }

        CurseForgeProjectId =
            configuration?.ProjectId ?? string.Empty;
        CurseForgeSlug =
            configuration?.Slug ?? string.Empty;
        CurseForgeMainCategoryId =
            configuration?.MainCategoryId ?? string.Empty;
        CurseForgeAdditionalCategoryIds =
            configuration is null ||
            configuration.AdditionalCategoryIds.Count == 0
                ? string.Empty
                : string.Join(
                    ", ",
                    configuration.AdditionalCategoryIds);
        CurseForgeLicense =
            configuration?.License ?? string.Empty;
        CurseForgeDistributionSelection =
            configuration?.AllowDistribution switch
            {
                true => "Allowed",
                false => "Blocked",
                null => "Not configured"
            };

        ApplyCurseForgeCategorySelections();
        RaiseCurseForgeProjectProperties();
        RaiseCurseForgeReadinessProperties();
        RaiseProjectDashboardProperties();
    }

    private void ApplyCurseForgeCategorySelections()
    {
        var additionalCategoryIds =
            ParseAdditionalCategoryIds(
                CurseForgeAdditionalCategoryIds)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        SelectedCurseForgeMainCategory =
            CurseForgeCategories
                .FirstOrDefault(category =>
                    string.Equals(
                        category.Id.ToString(),
                        CurseForgeMainCategoryId,
                        StringComparison.OrdinalIgnoreCase));

        foreach (var category in
                 CurseForgeCategories)
        {
            category.IsSelected =
                additionalCategoryIds.Contains(
                    category.Id.ToString());
        }

        RefreshCurseForgeCategoryFilters();
    }

    private static IReadOnlyList<string> ParseAdditionalCategoryIds(
        string value) =>
        value
            .Split(
                [',', ';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(category =>
                !string.IsNullOrWhiteSpace(category))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void ReplaceProjectInCatalog(
        ProjectCatalogEntry updatedProject)
    {
        for (var index = 0;
             index < Projects.Count;
             index++)
        {
            if (!string.Equals(
                    Projects[index].ProjectDirectory,
                    updatedProject.ProjectDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Projects[index] = updatedProject;
            break;
        }

        if (SelectedProject is not null &&
            string.Equals(
                SelectedProject.ProjectDirectory,
                updatedProject.ProjectDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            SelectedProject = updatedProject;
        }
    }

    private async Task LoadCurrentProjectTocVersionAsync(
        ProjectCatalogEntry project)
    {
        try
        {
            var runtimeAddon =
                !string.IsNullOrWhiteSpace(
                    project.PrimaryAddon)
                    ? project.PrimaryAddon
                    : project.Manifest.Runtime.Addons
                        .FirstOrDefault();

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

            var tocPath =
                Directory
                    .EnumerateFiles(
                        addonDirectory,
                        "*.toc",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(path =>
                        string.Equals(
                            Path.GetFileNameWithoutExtension(path),
                            runtimeAddon,
                            StringComparison.OrdinalIgnoreCase))
                ?? Directory
                    .EnumerateFiles(
                        addonDirectory,
                        "*.toc",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

            if (tocPath is null)
            {
                return;
            }

            var toc =
                await tocDocumentReader.ReadAsync(
                    tocPath);

            if (currentProject is null ||
                !string.Equals(
                    currentProject.ProjectDirectory,
                    project.ProjectDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CurrentProjectTocVersion =
                DisplayMetadata(
                    toc.Version);
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException)
        {
            CurrentProjectTocVersion = "—";
        }
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

        if (kind == PublishingContentKind.Changelog &&
            !HasCurrentReleaseVersion)
        {
            StatusMessage =
                "The .toc version is required before editing the release changelog.";
            return;
        }

        var file =
            kind == PublishingContentKind.Changelog
                ? publishingContentService
                    .ResolveReleaseChangelog(
                        CurrentProjectDirectory,
                        CurrentProjectTocVersion)
                : publishingContentService.Resolve(
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
            var text =
                kind == PublishingContentKind.Changelog
                    ? await publishingContentService
                        .ReadReleaseChangelogAsync(
                            CurrentProjectDirectory,
                            CurrentProjectTocVersion)
                    : await publishingContentService.ReadAsync(
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
            if (kind != PublishingContentKind.Changelog)
            {
                return publishingContentService
                    .Resolve(
                        CurrentProjectDirectory,
                        kind)
                    .Exists
                    ? "Created"
                    : "Not created";
            }

            if (!HasCurrentReleaseVersion)
            {
                return "Version unavailable";
            }

            var releaseFile =
                publishingContentService
                    .ResolveReleaseChangelog(
                        CurrentProjectDirectory,
                        CurrentProjectTocVersion);

            if (releaseFile.Exists)
            {
                return "Created";
            }

            return publishingContentService
                .IsReleaseChangelogUsingLegacyFallback(
                    CurrentProjectDirectory,
                    CurrentProjectTocVersion)
                ? "Legacy file · save to migrate"
                : "Not created";
        }
        catch
        {
            return "Unavailable";
        }
    }

    private string GetPublishingActionText(
        PublishingContentKind kind,
        string label)
    {
        var status =
            GetPublishingStatus(kind);

        return status is "Created" ||
               status.StartsWith(
                   "Legacy",
                   StringComparison.Ordinal)
            ? $"Edit {label}"
            : $"Create {label}";
    }

    private async Task LoadPublishingWorkspaceAsync(
        bool force = false)
    {
        if (CurrentProjectDirectory is null)
        {
            ClearPublishingWorkspace();
            return;
        }

        if (!force &&
            PublishingWorkspaceDirty &&
            string.Equals(
                publishingWorkspaceProjectDirectory,
                CurrentProjectDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await RunOperationAsync(async () =>
        {
            var summaryTask =
                publishingContentService.ReadAsync(
                    CurrentProjectDirectory,
                    PublishingContentKind.Summary);
            var descriptionTask =
                publishingContentService.ReadAsync(
                    CurrentProjectDirectory,
                    PublishingContentKind.Description);

            var changelogTask =
                HasCurrentReleaseVersion
                    ? publishingContentService
                        .ReadReleaseChangelogAsync(
                            CurrentProjectDirectory,
                            CurrentProjectTocVersion)
                    : Task.FromResult(
                        string.Empty);

            await Task.WhenAll(
                summaryTask,
                descriptionTask,
                changelogTask);

            suppressPublishingWorkspaceDirty = true;

            try
            {
                PublishingSummaryText =
                    await summaryTask;
                PublishingDescriptionText =
                    await descriptionTask;
                PublishingChangelogText =
                    await changelogTask;
            }
            finally
            {
                suppressPublishingWorkspaceDirty = false;
            }

            savedPublishingSummaryText =
                PublishingSummaryText;
            savedPublishingDescriptionText =
                PublishingDescriptionText;
            savedPublishingChangelogText =
                PublishingChangelogText;

            publishingChangelogLoadedFromLegacy =
                HasCurrentReleaseVersion &&
                publishingContentService
                    .IsReleaseChangelogUsingLegacyFallback(
                        CurrentProjectDirectory,
                        CurrentProjectTocVersion);

            publishingWorkspaceProjectDirectory =
                CurrentProjectDirectory;
            PublishingWorkspaceDirty = false;

            RaisePublishingProperties();

            StatusMessage =
                publishingChangelogLoadedFromLegacy
                    ? "Publishing content loaded. Legacy changelog will migrate on save."
                    : "Publishing content loaded.";
        });
    }

    private void ClearPublishingWorkspace()
    {
        suppressPublishingWorkspaceDirty = true;

        try
        {
            PublishingSummaryText = string.Empty;
            PublishingDescriptionText = string.Empty;
            PublishingChangelogText = string.Empty;
        }
        finally
        {
            suppressPublishingWorkspaceDirty = false;
        }

        savedPublishingSummaryText = string.Empty;
        savedPublishingDescriptionText = string.Empty;
        savedPublishingChangelogText = string.Empty;
        publishingWorkspaceProjectDirectory = null;
        publishingChangelogLoadedFromLegacy = false;
        PublishingWorkspaceDirty = false;
    }

    private void RefreshPublishingMedia()
    {
        PublishingLogoFileName = string.Empty;
        PublishingScreenshots.Clear();

        CurrentProjectLogoImage?.Dispose();
        CurrentProjectLogoImage = null;

        DisposeProjectScreenshots();

        if (CurrentProjectDirectory is null)
        {
            RaisePublishingMediaProperties();
            RaiseProjectDashboardProperties();
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

            if (snapshot.LogoFilePath is not null)
            {
                CurrentProjectLogoImage =
                    new Bitmap(
                        snapshot.LogoFilePath);
            }

            foreach (var screenshotPath in
                     snapshot.ScreenshotFilePaths)
            {
                PublishingScreenshots.Add(
                    Path.GetFileName(
                        screenshotPath));

                ProjectScreenshots.Add(
                    new ProjectScreenshotItem(
                        screenshotPath,
                        new Bitmap(
                            screenshotPath)));
            }

            SelectedProjectScreenshot =
                ProjectScreenshots.FirstOrDefault();
        }
        catch
        {
            PublishingLogoFileName = string.Empty;
            PublishingScreenshots.Clear();
            CurrentProjectLogoImage?.Dispose();
            CurrentProjectLogoImage = null;
            DisposeProjectScreenshots();
        }

        RaisePublishingMediaProperties();
        RaiseProjectDashboardProperties();
    }

    private void DisposeProjectScreenshots()
    {
        SelectedProjectScreenshot = null;

        foreach (var screenshot in
                 ProjectScreenshots)
        {
            screenshot.Dispose();
        }

        ProjectScreenshots.Clear();

        OnPropertyChanged(
            nameof(HasProjectScreenshots));
        OnPropertyChanged(
            nameof(HasNoProjectScreenshots));
        OnPropertyChanged(
            nameof(CanShowPreviousProjectScreenshot));
        OnPropertyChanged(
            nameof(CanShowNextProjectScreenshot));
        OnPropertyChanged(
            nameof(SelectedProjectScreenshotName));
    }

    private void RaisePublishingMediaProperties()
    {
        OnPropertyChanged(nameof(HasPublishingLogo));
        OnPropertyChanged(nameof(PublishingLogoStatus));
        OnPropertyChanged(nameof(PublishingLogoActionText));
        OnPropertyChanged(nameof(HasPublishingScreenshots));
        OnPropertyChanged(nameof(PublishingScreenshotsStatus));
        OnPropertyChanged(nameof(HasProjectScreenshots));
        OnPropertyChanged(nameof(HasNoProjectScreenshots));
        OnPropertyChanged(nameof(CanShowPreviousProjectScreenshot));
        OnPropertyChanged(nameof(CanShowNextProjectScreenshot));
        OnPropertyChanged(nameof(SelectedProjectScreenshotName));
        OnPropertyChanged(nameof(SelectedProjectScreenshotImage));
        RaiseCurseForgeReadinessProperties();
    }

    private void RaisePublishingProperties()
    {
        OnPropertyChanged(nameof(PublishingSummaryFileName));
        OnPropertyChanged(nameof(PublishingDescriptionFileName));
        OnPropertyChanged(nameof(PublishingChangelogFileName));
        OnPropertyChanged(nameof(HasCurrentReleaseVersion));
        OnPropertyChanged(nameof(HasNoCurrentReleaseVersion));
        OnPropertyChanged(nameof(CurrentReleaseTitle));
        OnPropertyChanged(nameof(PublishingSummaryStatus));
        OnPropertyChanged(nameof(PublishingDescriptionStatus));
        OnPropertyChanged(nameof(PublishingChangelogStatus));
        OnPropertyChanged(nameof(PublishingSummaryActionText));
        OnPropertyChanged(nameof(PublishingDescriptionActionText));
        OnPropertyChanged(nameof(PublishingChangelogActionText));
        OnPropertyChanged(nameof(PublishingWorkspaceStatus));
        OnPropertyChanged(nameof(CanSavePublishingWorkspace));
        OnPropertyChanged(nameof(CanRevertPublishingWorkspace));
        OnPropertyChanged(nameof(PublishingReleaseDirty));
        RaisePublishingMediaProperties();
        RaiseCurseForgeReadinessProperties();
        RaiseProjectDashboardProperties();
    }

    private void RaiseProjectDashboardProperties()
    {
        OnPropertyChanged(nameof(ProjectDashboardVersion));
        OnPropertyChanged(nameof(ProjectDashboardAuthor));
        OnPropertyChanged(nameof(ProjectDashboardInterfaces));
        OnPropertyChanged(nameof(ProjectDashboardComponents));
        OnPropertyChanged(nameof(ProjectDashboardCurseForgeStatus));
    }

    private void RefreshCurrentProjectTree()
    {
        var project = currentProject;

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
