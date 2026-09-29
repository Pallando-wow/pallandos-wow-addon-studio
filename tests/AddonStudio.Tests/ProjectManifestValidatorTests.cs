using AddonStudio.Core.Components;
using AddonStudio.Core.Projects;

namespace AddonStudio.Tests;

public class ProjectManifestValidatorTests
{
    [Fact]
    public void AddonWithoutComponents_IsValid()
    {
        var manifest = new ProjectManifest
        {
            Project = new ProjectIdentity
            {
                Id = "simple-addon",
                Name = "Simple Addon",
                Type = ProjectType.Addon
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = "SimpleAddon",
                Addons = ["SimpleAddon"]
            }
        };

        var issues = ProjectManifestValidator.Validate(manifest);

        Assert.DoesNotContain(
            issues,
            issue => issue.Severity == ProjectValidationSeverity.Error);
    }

    [Fact]
    public void LibraryWithoutComponents_IsValid()
    {
        var manifest = new ProjectManifest
        {
            Project = new ProjectIdentity
            {
                Id = "my-library",
                Name = "My Library",
                Type = ProjectType.Library
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = "MyLibrary-1.0",
                Addons = ["MyLibrary-1.0"]
            }
        };

        var issues = ProjectManifestValidator.Validate(manifest);

        Assert.DoesNotContain(
            issues,
            issue => issue.Severity == ProjectValidationSeverity.Error);
    }

    [Fact]
    public void ComponentTargetMustReferenceKnownRuntimeAddon()
    {
        var manifest = new ProjectManifest
        {
            Project = new ProjectIdentity
            {
                Id = "component-test",
                Name = "Component Test",
                Type = ProjectType.Addon
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = "ComponentTest",
                Addons = ["ComponentTest"]
            },
            Components =
            [
                new ComponentUsage
                {
                    Id = "shared-ui",
                    Targets =
                    [
                        new ComponentPlacement
                        {
                            RuntimeAddon = "MissingAddon",
                            Path = "SharedUI"
                        }
                    ]
                }
            ]
        };

        var issues = ProjectManifestValidator.Validate(manifest);

        Assert.Contains(
            issues,
            issue => issue.Code == "component.target.runtime-addon.unknown");
    }

    [Fact]
    public void PrimaryAddonMustBeInRuntimeList()
    {
        var manifest = new ProjectManifest
        {
            Project = new ProjectIdentity
            {
                Id = "broken-addon",
                Name = "Broken Addon",
                Type = ProjectType.Addon
            },
            Runtime = new RuntimeLayout
            {
                PrimaryAddon = "MissingAddon",
                Addons = ["ActualAddon"]
            }
        };

        var issues = ProjectManifestValidator.Validate(manifest);

        Assert.Contains(
            issues,
            issue => issue.Code == "runtime.primary.unknown");
    }
}
