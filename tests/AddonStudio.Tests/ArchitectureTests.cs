using System.Reflection;

namespace AddonStudio.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Core_DoesNotReferenceOtherAddonStudioProjects()
    {
        var assembly = Assembly.Load("AddonStudio.Core");

        var addonStudioReferences = assembly
            .GetReferencedAssemblies()
            .Where(reference =>
                reference.Name?.StartsWith("AddonStudio.", StringComparison.Ordinal) == true)
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Empty(addonStudioReferences);
    }
}
