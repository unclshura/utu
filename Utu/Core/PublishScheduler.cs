using Utu.Configuration;

namespace Utu.Core;

/// <summary>
/// Hands the publication times of a crontab expression to the videos of a
/// configuration: the first time the expression matches after <c>now</c> goes to
/// the first video, the second time to the second video, and so on.
/// </summary>
public static class PublishScheduler
{
    /// <summary>
    /// Gives every video that has no publication time yet the next time the
    /// expression matches. A video that already has one keeps it and does not
    /// consume a time. This is what <c>utu init</c> does.
    /// </summary>
    /// <param name="videos">Videos, in the order they are scheduled.</param>
    /// <param name="expression">Crontab expression; blank schedules nothing.</param>
    /// <param name="now">Moment the first publication time is counted from.</param>
    /// <returns>How many publication times were set.</returns>
    /// <exception cref="UtuException">
    /// The expression is not valid, or it names fewer times than there are videos
    /// to schedule.
    /// </exception>
    public static int Fill(IReadOnlyList<VideoEntry> videos, string? expression, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return 0;
        }

        return Assign(videos, CronSchedule.Parse(expression), now, replace: false);
    }

    /// <summary>
    /// Gives every video the next time the expression matches, replacing the
    /// publication times that are already set. This is what <c>utu schedule</c>
    /// does.
    /// </summary>
    /// <param name="videos">Videos, in the order they are scheduled.</param>
    /// <param name="expression">Crontab expression; it is required.</param>
    /// <param name="now">Moment the first publication time is counted from.</param>
    /// <returns>How many publication times changed.</returns>
    /// <exception cref="UtuException">
    /// The expression is blank or not valid, or it names fewer times than there
    /// are videos to schedule.
    /// </exception>
    public static int Replace(IReadOnlyList<VideoEntry> videos, string? expression, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new UtuException("a crontab expression is required to compute the publication times", 2);
        }

        return Assign(videos, CronSchedule.Parse(expression), now, replace: true);
    }

    private static int Assign(
        IReadOnlyList<VideoEntry> videos,
        CronSchedule schedule,
        DateTimeOffset now,
        bool replace)
    {
        IReadOnlyList<VideoEntry> targets = replace
            ? videos
            : [.. videos.Where(entry => entry.PublishAt is null)];

        using var occurrences = schedule.Occurrences(now).GetEnumerator();
        var changed = 0;

        foreach (var entry in targets)
        {
            if (!occurrences.MoveNext())
            {
                throw new UtuException(
                    $"the publish schedule '{schedule.Expression}' names too few times for the {targets.Count} videos",
                    2);
            }

            if (entry.PublishAt != occurrences.Current)
            {
                entry.PublishAt = occurrences.Current;
                changed++;
            }
        }

        return changed;
    }
}
