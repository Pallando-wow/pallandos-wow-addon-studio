namespace AddonStudio.Wow.Deployment;

public sealed class WowTestInstallWorkflowService(
    WowTestInstallPlanner planner,
    WowTestInstallService installer,
    IWowClientProcessDetector processDetector)
{
    public async Task<WowTestInstallResult> ExecuteAsync(
        WowTestInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var plan =
            await planner.CreateAsync(
                request,
                cancellationToken);

        var runningClients =
            processDetector.FindRunningClients(
                plan.WowAddOnsDirectory);

        if (runningClients.Count > 0)
        {
            var processes =
                string.Join(
                    ", ",
                    runningClients.Select(
                        process =>
                            $"{process.ProcessName} (PID {process.ProcessId})"));

            throw new InvalidOperationException(
                "WoW must be closed before installing addons for testing" +
                (plan.ResetSavedVariables
                    ? " or resetting SavedVariables"
                    : string.Empty) +
                $". Running client processes: {processes}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await installer.ExecuteAsync(
            plan,
            cancellationToken);
    }
}
