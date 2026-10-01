using AddonStudio.Core.Projects;
using AddonStudio.Wow.Deployment;
using AddonStudio.Wow.Toc;

namespace AddonStudio.Tests;

public sealed class WowTestInstallTests
{
    [Fact]
    public async Task Install_ReplacesAddonDirectoryInsteadOfOverlaying()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();
        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Title: ForeverBag
            ## Version: 1.1.0
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- new"),
            ("NewFile.lua", "-- new file"));

        var installedDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.toc"),
            """
            ## Interface: 16001
            ## Version: 1.0.0
            ForeverBag.lua
            """);

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.lua"),
            "-- old");

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "RemovedFile.lua"),
            "-- stale");

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var plan =
            await planner.CreateAsync(
                new WowTestInstallRequest(
                    environment.ProjectDirectory,
                    environment.WowAddOnsDirectory,
                    ["ForeverBag"]));

        var result =
            await new WowTestInstallService()
                .ExecuteAsync(
                    plan);

        Assert.Equal(
            ["ForeverBag"],
            result.InstalledAddons);
        Assert.Equal(
            "-- new",
            File.ReadAllText(
                System.IO.Path.Combine(
                    installedDirectory,
                    "ForeverBag.lua")));
        Assert.True(
            File.Exists(
                System.IO.Path.Combine(
                    installedDirectory,
                    "NewFile.lua")));
        Assert.False(
            File.Exists(
                System.IO.Path.Combine(
                    installedDirectory,
                    "RemovedFile.lua")));
        Assert.Null(
            result.SavedVariablesBackupDirectory);
    }

    [Fact]
    public async Task Planner_RejectsInstalledAddonWithNewerVersion()
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
            ("ForeverBag.lua", "-- project"));

        var installedDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.toc"),
            """
            ## Interface: 16001
            ## Version: 1.2.0
            ForeverBag.lua
            """);

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.lua"),
            "-- installed newer");

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["ForeverBag"])));

        Assert.Contains(
            "newer version '1.2.0'",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            "-- installed newer",
            File.ReadAllText(
                System.IO.Path.Combine(
                    installedDirectory,
                    "ForeverBag.lua")));
    }

    [Fact]
    public async Task Planner_RejectsInstalledAddonWithoutExpectedToc()
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
            ("ForeverBag.lua", "-- project"));

        var installedDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.lua"),
            "-- installed unknown");

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["ForeverBag"])));

        Assert.Contains(
            "version cannot be verified safely",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            "-- installed unknown",
            File.ReadAllText(
                System.IO.Path.Combine(
                    installedDirectory,
                    "ForeverBag.lua")));
    }

    [Theory]
    [InlineData("1.1.0", "1.1.0")]
    [InlineData("1.0.9", "1.1.0")]
    [InlineData("1.1", "1.1.0")]
    [InlineData("v1.1.0", "1.1.0")]
    public async Task Planner_AllowsInstalledVersionThatIsNotNewer(
        string installedVersion,
        string projectVersion)
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();
        environment.CreateRuntimeAddon(
            "ForeverBag",
            $"""
            ## Interface: 16001
            ## Version: {projectVersion}
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- project"));

        var installedDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.toc"),
            $"""
            ## Interface: 16001
            ## Version: {installedVersion}
            ForeverBag.lua
            """);

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var plan =
            await planner.CreateAsync(
                new WowTestInstallRequest(
                    environment.ProjectDirectory,
                    environment.WowAddOnsDirectory,
                    ["ForeverBag"]));

        Assert.Single(
            plan.Addons);
    }

    [Fact]
    public async Task Planner_RejectsUncomparableInstalledVersion()
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
            ("ForeverBag.lua", "-- project"));

        var installedDirectory =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.WowAddOnsDirectory,
                    "ForeverBag"))
                .FullName;

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.toc"),
            """
            ## Interface: 16001
            ## Version: development
            ForeverBag.lua
            """);

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["ForeverBag"])));

        Assert.Contains(
            "cannot be compared safely",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanInstall_BacksUpAndDeletesOnlyDeclaredSavedVariablesScope()
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
            ("ForeverBag.lua", "-- addon"));

        var globalLua =
            environment.CreateAccountSavedVariable(
                "ForeverBag.lua",
                "global");

        var globalBak =
            environment.CreateAccountSavedVariable(
                "ForeverBag.lua.bak",
                "global backup");

        var characterLua =
            environment.CreateCharacterSavedVariable(
                "Realm",
                "Character",
                "ForeverBag.lua",
                "character");

        var characterBak =
            environment.CreateCharacterSavedVariable(
                "Realm",
                "Character",
                "ForeverBag.lua.bak",
                "character backup");

        var unrelated =
            environment.CreateAccountSavedVariable(
                "OtherAddon.lua",
                "keep");

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var plan =
            await planner.CreateAsync(
                new WowTestInstallRequest(
                    environment.ProjectDirectory,
                    environment.WowAddOnsDirectory,
                    ["ForeverBag"],
                    ResetSavedVariables: true,
                    SavedVariablesDirectory:
                        environment.AccountSavedVariablesDirectory));

        Assert.Equal(
            4,
            plan.SavedVariablesFiles.Count);

        var result =
            await new WowTestInstallService(
                    new FixedTimeProvider(
                        new DateTimeOffset(
                            2026,
                            10,
                            1,
                            9,
                            30,
                            0,
                            TimeSpan.Zero)))
                .ExecuteAsync(
                    plan);

        Assert.False(
            File.Exists(
                globalLua));
        Assert.False(
            File.Exists(
                globalBak));
        Assert.False(
            File.Exists(
                characterLua));
        Assert.False(
            File.Exists(
                characterBak));
        Assert.True(
            File.Exists(
                unrelated));

        Assert.NotNull(
            result.SavedVariablesBackupDirectory);
        Assert.Equal(
            4,
            result.ResetSavedVariablesFiles.Count);

        Assert.Equal(
            "global",
            File.ReadAllText(
                System.IO.Path.Combine(
                    result.SavedVariablesBackupDirectory!,
                    "SavedVariables",
                    "ForeverBag.lua")));

        Assert.Equal(
            "character",
            File.ReadAllText(
                System.IO.Path.Combine(
                    result.SavedVariablesBackupDirectory!,
                    "Realm",
                    "Character",
                    "SavedVariables",
                    "ForeverBag.lua")));
    }

    [Fact]
    public async Task CleanInstall_WithAccountVariablesOnly_DoesNotDeleteCharacterFile()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();
        environment.CreateRuntimeAddon(
            "ForeverMail",
            """
            ## Interface: 16001
            ## SavedVariables: ForeverMailDB
            ForeverMail.lua
            """,
            ("ForeverMail.lua", "-- addon"));

        var globalLua =
            environment.CreateAccountSavedVariable(
                "ForeverMail.lua",
                "global");

        var characterLua =
            environment.CreateCharacterSavedVariable(
                "Realm",
                "Character",
                "ForeverMail.lua",
                "character keep");

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var plan =
            await planner.CreateAsync(
                new WowTestInstallRequest(
                    environment.ProjectDirectory,
                    environment.WowAddOnsDirectory,
                    ["ForeverMail"],
                    ResetSavedVariables: true,
                    SavedVariablesDirectory:
                        environment.AccountSavedVariablesDirectory));

        await new WowTestInstallService()
            .ExecuteAsync(
                plan);

        Assert.False(
            File.Exists(
                globalLua));
        Assert.True(
            File.Exists(
                characterLua));
    }

    [Fact]
    public async Task CleanInstall_RequiresSavedVariablesDirectoryBeforeChangingAddon()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();
        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ## Version: 1.1.0
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

        File.WriteAllText(
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.toc"),
            """
            ## Interface: 16001
            ## Version: 1.0.0
            ForeverBag.lua
            """);

        var installedFile =
            System.IO.Path.Combine(
                installedDirectory,
                "ForeverBag.lua");

        File.WriteAllText(
            installedFile,
            "-- old");

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["ForeverBag"],
                        ResetSavedVariables: true)));

        Assert.Equal(
            "-- old",
            File.ReadAllText(
                installedFile));
    }

    [Fact]
    public async Task Planner_RejectsTargetDirectoryOutsideInterfaceAddOnsShape()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();
        environment.CreateRuntimeAddon(
            "ForeverBag",
            """
            ## Interface: 16001
            ForeverBag.lua
            """,
            ("ForeverBag.lua", "-- addon"));

        var unsafeTarget =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.RootDirectory,
                    "SomeOtherDirectory"))
                .FullName;

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        unsafeTarget,
                        ["ForeverBag"])));

        Assert.Contains(
            "Interface/AddOns",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanInstall_RejectsSavedVariablesOutsideWtfAccountShape()
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
            ("ForeverBag.lua", "-- addon"));

        var unsafeSavedVariables =
            Directory.CreateDirectory(
                System.IO.Path.Combine(
                    environment.RootDirectory,
                    "Unrelated",
                    "SavedVariables"))
                .FullName;

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        var exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["ForeverBag"],
                        ResetSavedVariables: true,
                        SavedVariablesDirectory:
                            unsafeSavedVariables)));

        Assert.Contains(
            "WTF/Account",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Planner_RejectsUnsafeRuntimeAddonName()
    {
        using var environment =
            new TemporaryEnvironment();

        environment.CreateManagedProject();

        var planner =
            new WowTestInstallPlanner(
                new TocDocumentReader(),
                environment.BackupRoot);

        await Assert.ThrowsAsync<
            InvalidDataException>(
                () => planner.CreateAsync(
                    new WowTestInstallRequest(
                        environment.ProjectDirectory,
                        environment.WowAddOnsDirectory,
                        ["../Outside"])));
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

            WowAddOnsDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        root,
                        "WoW",
                        "Interface",
                        "AddOns"))
                    .FullName;

            AccountDirectory =
                Directory.CreateDirectory(
                    System.IO.Path.Combine(
                        root,
                        "WoW",
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

        public string RootDirectory =>
            root;

        public string ProjectDirectory { get; }

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
