using AddonStudio.Application.Settings;
using AddonStudio.Core.Projects;
using AddonStudio.Wow.Deployment;
using AddonStudio.Wow.Toc;

namespace AddonStudio.Tests;

public sealed class WowTestInstallCoordinatorTests
{
    [Fact]
    public async Task Coordinator_UsesManifestRuntimeAddonsForNormalInstall()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();

        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## SavedVariables: ForeverBagDB
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- bag"));

        environment.CreateRuntimeAddon(
            "ForeverMail",
            """
            ## Interface: 16001
            ## SavedVariables: ForeverMailDB
            ForeverMail.lua
            """,
            ("ForeverMail.lua", "-- mail"));

        var savedVariable =
            environment.CreateAccountSavedVariable(
                "ForeverBag.lua",
                "keep on normal install");

        var coordinator =
            CreateCoordinator(
                environment);

        var result =
            await coordinator.InstallAsync(
                environment.ProjectDirectory,
                CreateManifest(
                    "ForeverBag",
                    "ForeverMail"),
                new StudioSettings
                {
                    WowForeverAddOnsPath =
                        environment.WowAddOnsDirectory,
                    SavedVariablesPath =
                        environment.AccountSavedVariablesDirectory
                });

        Assert.Equal(
            ["ForeverBag", "ForeverMail"],
            result.InstalledAddons);
        Assert.True(
            File.Exists(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag",
                    "ForeverBag.lua")));
        Assert.True(
            File.Exists(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverMail",
                    "ForeverMail.lua")));
        Assert.True(
            File.Exists(
                savedVariable));
        Assert.Empty(
            result.ResetSavedVariablesFiles);
        Assert.Null(
            result.SavedVariablesBackupDirectory);
    }

    [Fact]
    public async Task Coordinator_CleanTestUsesConfiguredSavedVariablesPath()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();

        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## SavedVariables: ForeverBagDB
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- bag"));

        var savedVariable =
            environment.CreateAccountSavedVariable(
                "ForeverBag.lua",
                "clean me");

        var coordinator =
            CreateCoordinator(
                environment);

        var result =
            await coordinator.InstallAsync(
                environment.ProjectDirectory,
                CreateManifest(
                    "ForeverBag"),
                new StudioSettings
                {
                    WowForeverAddOnsPath =
                        environment.WowAddOnsDirectory,
                    SavedVariablesPath =
                        environment.AccountSavedVariablesDirectory
                },
                cleanTest: true,
                backupRootDirectory:
                    environment.BackupRoot);

        Assert.False(
            File.Exists(
                savedVariable));
        Assert.Single(
            result.ResetSavedVariablesFiles);
        Assert.NotNull(
            result.SavedVariablesBackupDirectory);
        Assert.Equal(
            "clean me",
            File.ReadAllText(
                System.IO.Path.Combine(
                    result.SavedVariablesBackupDirectory!,
                    "SavedVariables",
                    "ForeverBag.lua")));
    }

    [Fact]
    public async Task Coordinator_CleanTestRequiresSavedVariablesConfigurationBeforeInstall()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();

        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- new"));

        var targetDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                targetDirectory,
                "ForeverBag.toc"),
            """
            ## Interface: 16001
            ## Version: 1.0.0
            ForeverBag.lua
            """);

        var targetFile =
            System.IO.Path.Combine(
                targetDirectory,
                "ForeverBag.lua");

        File.WriteAllText(
            targetFile,
            "-- old");

        var coordinator =
            CreateCoordinator(
                environment);

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => coordinator.InstallAsync(
                    environment.ProjectDirectory,
                    CreateManifest(
                        "ForeverBag"),
                    new StudioSettings
                    {
                        WowForeverAddOnsPath =
                            environment.WowAddOnsDirectory,
                        SavedVariablesPath =
                            string.Empty
                    },
                    cleanTest: true));

        Assert.Equal(
            "-- old",
            File.ReadAllText(
                targetFile));
    }

    [Fact]
    public async Task Coordinator_RejectsManifestWithoutRuntimeAddons()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();

        var coordinator =
            CreateCoordinator(
                environment);

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => coordinator.InstallAsync(
                    environment.ProjectDirectory,
                    new ProjectManifest
                    {
                        Project =
                            new ProjectIdentity
                            {
                                Id = "empty",
                                Name = "Empty",
                                Type = ProjectType.Addon
                            },
                        Runtime =
                            new RuntimeLayout
                            {
                                PrimaryAddon = null,
                                Addons = []
                            }
                    },
                    new StudioSettings
                    {
                        WowForeverAddOnsPath =
                            environment.WowAddOnsDirectory
                    }));
    }

    private static WowTestInstallCoordinatorService
        CreateCoordinator(
            TemporaryEnvironment environment) =>
        new(
            new WowTestInstallWorkflowService(
                new WowTestInstallPlanner(
                    new TocDocumentReader(),
                    environment.BackupRoot),
                new WowTestInstallService(),
                new FakeProcessDetector()));

    private static ProjectManifest CreateManifest(
        params string[] addons)
    {
        if (addons.Length == 0)
        {
            throw new ArgumentException(
                "At least one runtime addon is required.",
                nameof(addons));
        }

        return new ProjectManifest
        {
            Project =
                new ProjectIdentity
                {
                    Id = "test-addon",
                    Name = "Test Addon",
                    Type = ProjectType.Addon
                },
            Runtime =
                new RuntimeLayout
                {
                    PrimaryAddon =
                        addons[0],
                    Addons =
                        addons
                }
        };
    }

    private sealed class FakeProcessDetector :
        IWowClientProcessDetector
    {
        public IReadOnlyList<WowRunningClientProcess>
            FindRunningClients(
                string wowAddOnsDirectory) =>
            [];
    }

    private sealed class TemporaryEnvironment :
        IDisposable
    {
        private readonly string root;

        public TemporaryEnvironment()
        {
            root =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "AddonStudio.Tests",
                    Guid.NewGuid().ToString("N"));

            ProjectDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        root,
                        "Project"))
                    .FullName;

            WowAddOnsDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        root,
                        "WoW",
                        "_classic_beta_",
                        "Interface",
                        "AddOns"))
                    .FullName;

            AccountSavedVariablesDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        root,
                        "WoW",
                        "_classic_beta_",
                        "WTF",
                        "Account",
                        "12345",
                        "SavedVariables"))
                    .FullName;

            BackupRoot =
                System.IO.Path.Combine(
                    root,
                    "StudioBackups");
        }

        public string ProjectDirectory { get; }

        public string WowAddOnsDirectory { get; }

        public string AccountSavedVariablesDirectory { get; }

        public string BackupRoot { get; }

        public void CreateManagedProject()
        {
            File.WriteAllText(
                System.IO.Path.Combine(
                    ProjectDirectory,
                    ProjectLayout.ManifestFileName),
                "{}");
        }

        public void CreateRuntimeAddon(
            string addonName,
            string toc,
            params (string Name, string Content)[] files)
        {
            var directory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        ProjectDirectory,
                        ProjectLayout.RuntimeDirectoryName,
                        addonName))
                    .FullName;

            File.WriteAllText(
                System.IO.Path.Combine(
                    directory,
                    addonName + ".toc"),
                toc.ReplaceLineEndings(
                    Environment.NewLine));

            foreach (var file in files)
            {
                File.WriteAllText(
                    System.IO.Path.Combine(
                        directory,
                        file.Name),
                    file.Content);
            }
        }

        public string CreateAccountSavedVariable(
            string fileName,
            string content)
        {
            var path =
                System.IO.Path.Combine(
                    AccountSavedVariablesDirectory,
                    fileName);

            File.WriteAllText(
                path,
                content);

            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
