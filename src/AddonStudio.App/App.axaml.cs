using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AddonStudio.App.ViewModels;
using AddonStudio.App.Views;
using AddonStudio.Application.Documents;
using AddonStudio.Application.Projects;
using AddonStudio.Application.Publishing;
using AddonStudio.Data.Projects;
using AddonStudio.Data.Settings;
using AddonStudio.Media;
using AddonStudio.Platforms.CurseForge;
using AddonStudio.Platforms.Security;
using AddonStudio.Wow.Collector;
using AddonStudio.Wow.Deployment;
using AddonStudio.Wow.Projects;
using AddonStudio.Wow.Toc;

namespace AddonStudio.App;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var manifestReader = new ProjectManifestReader();
            var manifestWriter = new ProjectManifestWriter();
            var addonSourceInspector = new AddonSourceInspector();

            var addonProjectService = new AddonProjectService(
                addonSourceInspector,
                manifestWriter);

            var projectCurseForgeSettingsService =
                new ProjectCurseForgeSettingsService(
                    manifestWriter);

            var curseForgeDataSourceClient =
                new CurseForgeDataSourceClient(
                    new HttpClient());

            var localSecretStore =
                new WindowsProtectedSecretStore();

            var projectCatalogService =
                new ProjectCatalogService(
                    manifestReader,
                    addonSourceInspector);

            var projectExplorerService =
                new ProjectExplorerService();

            var markdownDocumentService =
                new MarkdownDocumentService();

            var publishingContentService =
                new PublishingContentService();

            var projectMediaService =
                new ProjectMediaService();

            var tocDocumentReader =
                new TocDocumentReader();

            var pallandoCollectorReader =
                new PallandoCollectorReader();

            var wowTestInstallCoordinatorService =
                new WowTestInstallCoordinatorService(
                    new WowTestInstallWorkflowService(
                        new WowTestInstallPlanner(
                            tocDocumentReader),
                        new WowTestInstallService(),
                        new SystemWowClientProcessDetector()));

            var settingsStore =
                new JsonStudioSettingsStore();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
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
                    wowTestInstallCoordinatorService,
                    initialize: true),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
