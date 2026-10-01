using AddonStudio.Core.Projects;
using AddonStudio.Wow.Deployment;
using AddonStudio.Wow.Toc;

namespace AddonStudio.Tests;

public sealed class WowTestInstallWorkflowTests
{
    [Fact]
    public async Task Workflow_BlocksBeforeChangingFilesWhenWowIsRunning()
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
            ("ForeverBag.lua", "-- new"));

        var installedDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        var installedFile =
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.lua");

        File.WriteAllText(
            installedFile,
            "-- old");

        var savedVariablesFile =
            environment.CreateAccountSavedVariable(
                "ForeverBag.lua",
                "keep while running");

        var workflow =
            CreateWorkflow(
                environment,
                new FakeProcessDetector(
                    [
                        new WowRunningClientProcess(
                            4242,
                            "WowClassic",
                            System.IO.Path.Combine(
                                environment.WowClientRoot,
                                "WowClassic.exe"))
                    ]));

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => workflow.ExecuteAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["ForeverBag"],
                        ResetSavedVariables: true,
                        SavedVariablesDirectory:
                            environment.AccountSavedVariablesDirectory)));

        Assert.Contains(
            "PID 4242",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            "-- old",
            File.ReadAllText(
                installedFile));
        Assert.Equal(
            "keep while running",
            File.ReadAllText(
                savedVariablesFile));
        Assert.Empty(
            Directory.Exists(
                    environment.BackupRoot)
                ? Directory.EnumerateDirectories(
                    environment.BackupRoot)
                : []);
    }

    [Fact]
    public async Task Workflow_InstallsMultipleRuntimeAddonsAndPerformsCleanTest()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();

        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## SavedVariables: ForeverBagDB
            ## SavedVariablesPerCharacter: ForeverBagCharacterDB
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- bag new"),
            ("Modules/BagModule.lua", "-- module"));

        environment.CreateRuntimeAddon(
            "ForeverMail",
            """
            ## Interface: 16001
            ## SavedVariables: ForeverMailDB
            ForeverMail.lua
            """,
            ("ForeverMail.lua", "-- mail new"));

        var oldBagDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                oldBagDirectory,
                "ForeverBag.lua"),
            "-- bag old");

        File.WriteAllText(
            System.IO.Path.Combine(
                oldBagDirectory,
                "Removed.lua"),
            "-- stale");

        var oldMailDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverMail"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                oldMailDirectory,
                "ForeverMail.lua"),
            "-- mail old");

        var bagAccount =
            environment.CreateAccountSavedVariable(
                "ForeverBag.lua",
                "bag account");

        var bagCharacter =
            environment.CreateCharacterSavedVariable(
                "Realm",
                "Character",
                "ForeverBag.lua",
                "bag character");

        var mailAccount =
            environment.CreateAccountSavedVariable(
                "ForeverMail.lua",
                "mail account");

        var unrelated =
            environment.CreateAccountSavedVariable(
                "OtherAddon.lua",
                "keep me");

        var workflow =
            CreateWorkflow(
                environment,
                new FakeProcessDetector(
                    []));

        var result =
            await workflow.ExecuteAsync(
                new WowTestInstallRequest(
                    environment.ProjectDirectory,
                    environment.WowAddOnsDirectory,
                    ["ForeverBag", "ForeverMail"],
                    ResetSavedVariables: true,
                    SavedVariablesDirectory:
                        environment.AccountSavedVariablesDirectory));

        Assert.Equal(
            ["ForeverBag", "ForeverMail"],
            result.InstalledAddons);

        Assert.Equal(
            "-- bag new",
            File.ReadAllText(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag",
                    "ForeverBag.lua")));

        Assert.True(
            File.Exists(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag",
                    "Modules",
                    "BagModule.lua")));

        Assert.False(
            File.Exists(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag",
                    "Removed.lua")));

        Assert.Equal(
            "-- mail new",
            File.ReadAllText(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverMail",
                    "ForeverMail.lua")));

        Assert.False(
            File.Exists(
                bagAccount));
        Assert.False(
            File.Exists(
                bagCharacter));
        Assert.False(
            File.Exists(
                mailAccount));
        Assert.True(
            File.Exists(
                unrelated));

        Assert.NotNull(
            result.SavedVariablesBackupDirectory);

        Assert.Equal(
            "bag account",
            File.ReadAllText(
                System.IO.Path.Combine(
                    result.SavedVariablesBackupDirectory!,
                    "SavedVariables",
                    "ForeverBag.lua")));

        Assert.Equal(
            "bag character",
            File.ReadAllText(
                System.IO.Path.Combine(
                    result.SavedVariablesBackupDirectory!,
                    "Realm",
                    "Character",
                    "SavedVariables",
                    "ForeverBag.lua")));

        Assert.Equal(
            "mail account",
            File.ReadAllText(
                System.IO.Path.Combine(
                    result.SavedVariablesBackupDirectory!,
                    "SavedVariables",
                    "ForeverMail.lua")));
    }

    private static WowTestInstallWorkflowService
        CreateWorkflow(
            TemporaryEnvironment environment,
            IWowClientProcessDetector processDetector) =>
        new(
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot),
            new WowTestInstallService(
                new FixedTimeProvider(
                    new DateTimeOffset(
                        2026,
                        10,
                        1,
                        10,
                        0,
                        0,
                        TimeSpan.Zero))),
            processDetector);

    private sealed class FakeProcessDetector(
        IReadOnlyList<WowRunningClientProcess> processes)
        : IWowClientProcessDetector
    {
        public IReadOnlyList<WowRunningClientProcess>
            FindRunningClients(
                string wowAddOnsDirectory) =>
            processes;
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
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

            WowClientRoot =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        root,
                        "WoW",
                        "_classic_beta_"))
                    .FullName;

            WowAddOnsDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        WowClientRoot,
                        "Interface",
                        "AddOns"))
                    .FullName;

            AccountDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        WowClientRoot,
                        "WTF",
                        "Account",
                        "12345"))
                    .FullName;

            AccountSavedVariablesDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        AccountDirectory,
                        "SavedVariables"))
                    .FullName;

            BackupRoot =
                System.IO.Path.Combine(
                    root,
                    "StudioBackups");
        }

        public string ProjectDirectory { get; }

        public string WowClientRoot { get; }

        public string WowAddOnsDirectory { get; }

        public string AccountDirectory { get; }

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
                var path =
                    System.IO.Path.Combine(
                        directory,
                        file.Name.Replace(
                            '/',
                            System.IO.Path.DirectorySeparatorChar));

                Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(
                        path)!);

                File.WriteAllText(
                    path,
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

        public string CreateCharacterSavedVariable(
            string realm,
            string character,
            string fileName,
            string content)
        {
            var directory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        AccountDirectory,
                        realm,
                        character,
                        "SavedVariables"))
                    .FullName;

            var path =
                System.IO.Path.Combine(
                    directory,
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
