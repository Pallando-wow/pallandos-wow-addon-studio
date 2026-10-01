using System.ComponentModel;
using System.Diagnostics;

namespace AddonStudio.Wow.Deployment;

public sealed record WowRunningClientProcess(
    int ProcessId,
    string ProcessName,
    string ExecutablePath);

public interface IWowClientProcessDetector
{
    IReadOnlyList<WowRunningClientProcess>
        FindRunningClients(
            string wowAddOnsDirectory);
}

public sealed class SystemWowClientProcessDetector :
    IWowClientProcessDetector
{
    public IReadOnlyList<WowRunningClientProcess>
        FindRunningClients(
            string wowAddOnsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            wowAddOnsDirectory);

        var addOnsDirectory =
            Path.GetFullPath(
                wowAddOnsDirectory);

        var interfaceDirectory =
            Directory.GetParent(
                addOnsDirectory)
            ?? throw new InvalidDataException(
                "WoW AddOns directory has no Interface parent directory.");

        var clientRoot =
            interfaceDirectory.Parent
            ?? throw new InvalidDataException(
                "WoW Interface directory has no client root directory.");

        var matches =
            new List<WowRunningClientProcess>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var executablePath =
                    TryGetExecutablePath(
                        process);

                if (string.IsNullOrWhiteSpace(
                        executablePath))
                {
                    continue;
                }

                var executableName =
                    Path.GetFileNameWithoutExtension(
                        executablePath);

                if (!executableName.StartsWith(
                        "Wow",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsPathBelow(
                        executablePath,
                        clientRoot.FullName))
                {
                    continue;
                }

                matches.Add(
                    new WowRunningClientProcess(
                        process.Id,
                        process.ProcessName,
                        executablePath));
            }
            finally
            {
                process.Dispose();
            }
        }

        return matches
            .OrderBy(
                item =>
                    item.ProcessName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                item =>
                    item.ProcessId)
            .ToArray();
    }

    private static string? TryGetExecutablePath(
        Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception exception)
            when (exception is
                Win32Exception or
                InvalidOperationException or
                NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsPathBelow(
        string candidatePath,
        string rootPath)
    {
        var candidate =
            Path.GetFullPath(
                candidatePath);

        var root =
            Path.GetFullPath(
                    rootPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return candidate.StartsWith(
            root,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }
}
