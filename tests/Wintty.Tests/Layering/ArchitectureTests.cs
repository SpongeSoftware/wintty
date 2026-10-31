using System.Reflection;
using System.Text.RegularExpressions;
using Wintty.Core;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Layering;

/// <summary>Fails the build when layering or dependency rules are broken.</summary>
public class ArchitectureTests
{
    private const string NativeBinding = "LibGhostty.Net";

    private static readonly string RepoRoot = FindRepoRoot();

    private static IEnumerable<string> ProjectFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories);

    private static bool References(Assembly assembly, string name) =>
        assembly.GetReferencedAssemblies().Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void OnlyTheGhosttyProject_ReferencesTheNativeBindingPackage()
    {
        var offenders = ProjectFiles()
            .Where(p => !Path.GetFileNameWithoutExtension(p).Equals("Wintty.Ghostty", StringComparison.Ordinal))
            .Where(p => File.ReadAllText(p).Contains(NativeBinding, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void GhosttyProject_IsTheOneThatReferencesTheNativeBinding()
    {
        Assert.True(References(typeof(GhosttyGateway).Assembly, NativeBinding));
    }

    [Fact]
    public void Core_DoesNotDependOnNativeBindingOrOtherWinttyProjects()
    {
        var core = typeof(VersionInfo).Assembly;

        Assert.False(References(core, NativeBinding));
        Assert.DoesNotContain(core.GetReferencedAssemblies(), a => a.Name!.StartsWith("Wintty.", StringComparison.Ordinal));
        Assert.DoesNotContain(core.GetReferencedAssemblies(), a => a.Name!.StartsWith("Microsoft.UI", StringComparison.Ordinal));
        Assert.DoesNotContain(core.GetReferencedAssemblies(), a => a.Name!.StartsWith("Microsoft.Windows", StringComparison.Ordinal));
    }

    [Fact]
    public void NoSourceFile_OutsideGhosttyProject_UsesTheLibGhosttyNamespace()
    {
        var pattern = new Regex(@"\busing\s+LibGhostty\b|\bLibGhostty\.", RegexOptions.Compiled);
        var offenders = Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Wintty.Ghostty{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => pattern.IsMatch(File.ReadAllText(f)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void LibraryProjects_PointInwardOnly()
    {
        // App -> Render/Pty/Ghostty -> Core; Core and Ghostty must never reference the App or Render projects.
        foreach (var project in ProjectFiles())
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var text = File.ReadAllText(project);
            if (name is "Wintty.Core")
            {
                Assert.DoesNotContain("ProjectReference", text);
            }

            if (name is "Wintty.Ghostty" or "Wintty.Render" or "Wintty.Input")
            {
                Assert.DoesNotContain("Wintty.App", text);
            }

            if (name is "Wintty.Input")
            {
                // Input maps platform key codes to Core types only.
                Assert.DoesNotContain("Wintty.Ghostty", text);
                Assert.DoesNotContain("Wintty.Render", text);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "wintty.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate wintty.sln from the test output directory.");
    }
}
