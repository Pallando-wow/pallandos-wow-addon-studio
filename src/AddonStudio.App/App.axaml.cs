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
using AddonStudio.Wow.Collector;
using AddonStudio.Wow.Projects;

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
            var addonSourceInspector = new AddonSourceInspector();

            var addonProjectService = new AddonProjectService(
                addonSourceInspector,
                new ProjectManifestWriter());

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

            var pallandoCollectorReader =
                new PallandoCollectorReader();

            var settingsStore =
                new JsonStudioSettingsStore();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    addonProjectService,
                    settingsStore,
                    projectCatalogService,
                    projectExplorerService,
                    markdownDocumentService,
                    publishingContentService,
                    pallandoCollectorReader,
                    initialize: true),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
