using Spectre.Console;
using Utu.Cli;
using Utu.Configuration;
using Utu.Core;
using Utu.YouTubeApi;

namespace Utu.Commands;

/// <summary>Uploads the videos described by <c>utu.json</c>.</summary>
public static class UploadCommand
{
    /// <summary>Runs <c>utu upload</c>.</summary>
    public static async Task<int> RunAsync(CommandLine line, ConsoleUi ui, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(line.Folder);

        ui.Heading("utu upload");

        var loaded = ConfigurationLoader.Load(root, line.ConfigFile);
        var config = ApplyOverrides(loaded.Resolved, line);

        ui.Info($"configuration: {loaded.ConfigPath}");
        ui.Info(loaded.GlobalPath is null
            ? $"global: {ConfigurationLoader.GlobalConfigPath()} (not found, folder settings only)"
            : $"global: {loaded.GlobalPath}");

        if (ui.IsVerbose)
        {
            var defaults = SettingsResolver.Resolve(new VideoEntry(), config.Defaults);

            ui.Detail($"channel: {config.Channel.Name ?? "(unnamed)"}, {config.Channel.ParallelUploads} parallel uploads");
            ui.Detail($"secrets: {config.Channel.ClientSecretsFile}");
            ui.Detail($"token cache: {config.Channel.TokenCacheDirectory}");
            ui.Detail(
                $"defaults: category={defaults.Category ?? "-"}, privacy={defaults.Privacy}, " +
                $"license={defaults.License}, madeForKids={defaults.MadeForKids}, " +
                $"description={(string.IsNullOrEmpty(defaults.Description) ? "-" : "set")}");
        }

        var validation = UtuConfigValidator.Validate(config);
        ReportWriter.WriteValidation(ui, validation);

        if (validation.HasErrors)
        {
            return 1;
        }

        var statePath = config.Channel.StateFile;
        var stateLoad = UploadStateStore.Load(statePath);

        if (stateLoad.Warning is { Length: > 0 } warning)
        {
            ui.Warning(warning);
        }

        var state = new UploadStateStore(statePath, stateLoad.Document);
        ui.Detail($"state: {state.Path}");

        var plan = new UploadPlanner(state).CreatePlan(
            config,
            new UploadPlanOptions
            {
                Force = line.Force,
                Resume = !line.NoResume && config.Channel.ResumeUploads,
                Only = line.Only,
            });

        ReportWriter.WritePlan(ui, plan);

        if (line.DryRun)
        {
            ui.Success($"dry run: {plan.Work.Count()} videos would be uploaded");
            return 0;
        }

        if (!plan.HasWork)
        {
            ui.Success("nothing to do");
            return plan.Blocked.Any() ? 1 : 0;
        }

        if (plan.Blocked.Any())
        {
            ui.Warning($"{plan.Blocked.Count()} entries are blocked and will not be uploaded");
        }

        ui.Blank();

        using var service = await GoogleAuthorization.CreateServiceAsync(
            config.Channel,
            tokenDirectory => ui.Info($"No cached token in '{tokenDirectory}'; opening the browser to sign in."),
            cancellationToken)
            .ConfigureAwait(false);

        var client = new YouTubeClient(service);

        if (ui.IsVerbose)
        {
            var channel = await client.GetChannelTitleAsync(cancellationToken).ConfigureAwait(false);
            if (channel is { Length: > 0 })
            {
                ui.Info($"channel: {channel}");
            }
        }

        var options = new UploadRunnerOptions
        {
            Parallelism = line.Jobs ?? config.Channel.ParallelUploads,
            Retries = line.Retries ?? UploadRunnerOptions.DefaultRetries,
        };

        var summary = await RunAsync(ui, client, state, plan, options, cancellationToken).ConfigureAwait(false);

        ui.Blank();
        ReportWriter.WriteSummary(ui, summary);
        ReportWriter.WriteManualFollowUps(ui, summary);

        var failed = summary.Failed.Count();
        var blocked = plan.Blocked.Count();

        if (failed == 0 && blocked == 0)
        {
            ui.Success($"{summary.Outcomes.Count} videos processed");
            return 0;
        }

        ui.Error($"{failed} failed, {blocked} blocked");
        return 1;
    }

    private static async Task<UploadSummary> RunAsync(
        ConsoleUi ui,
        IYouTubeClient client,
        UploadStateStore state,
        UploadPlan plan,
        UploadRunnerOptions options,
        CancellationToken cancellationToken)
    {
        if (!ui.IsColor || Console.IsOutputRedirected)
        {
            var observer = new ConsoleUploadObserver(ui);
            return await new UploadRunner(client, state, options, observer).RunAsync(plan, cancellationToken)
                .ConfigureAwait(false);
        }

        // One progress bar per video; the bars are created by the observer.
        return await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn())
            .StartAsync(context => new UploadRunner(
                client,
                state,
                options,
                new ConsoleUploadObserver(ui, context))
                .RunAsync(plan, cancellationToken))
            .ConfigureAwait(false);
    }

    private static ResolvedConfiguration ApplyOverrides(ResolvedConfiguration config, CommandLine line) =>
        line.ClientSecrets is { Length: > 0 } secrets
            ? config with
            {
                Channel = config.Channel with
                {
                    ClientSecretsFile = PathResolver.Resolve(config.WorkingFolder, secrets),
                },
            }
            : config;
}
