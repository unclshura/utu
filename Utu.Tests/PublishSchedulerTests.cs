using Utu.Configuration;
using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class PublishSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 30, 0, TimeSpan.Zero);

    private static List<VideoEntry> Videos(params DateTimeOffset?[] times) =>
        [.. times.Select((time, index) => new VideoEntry { File = $"v{index}.mp4", PublishAt = time })];

    private static List<DateTimeOffset> Occurrences(string expression, int count) =>
        [.. CronSchedule.Parse(expression, TimeZoneInfo.Local).Occurrences(Now).Take(count)];

    [TestMethod]
    public void Fill_gives_every_video_without_a_time_the_next_occurrence()
    {
        var videos = Videos(null, null, null);

        var changed = PublishScheduler.Fill(videos, "0 * * * *", Now);

        Assert.AreEqual(3, changed);
        CollectionAssert.AreEqual(
            Occurrences("0 * * * *", 3),
            videos.Select(entry => entry.PublishAt!.Value).ToList());
    }

    [TestMethod]
    public void Fill_leaves_the_videos_that_already_have_a_time_alone()
    {
        var fixedTime = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
        var videos = Videos(fixedTime, null, null);

        var changed = PublishScheduler.Fill(videos, "@daily", Now);

        var daily = Occurrences("@daily", 2);
        Assert.AreEqual(2, changed);
        Assert.AreEqual(fixedTime, videos[0].PublishAt, "a video that already had a time");
        Assert.AreEqual(daily[0], videos[1].PublishAt, "the first video without a time");
        Assert.AreEqual(daily[1], videos[2].PublishAt, "the second video without a time");
    }

    [TestMethod]
    public void Fill_without_an_expression_schedules_nothing()
    {
        var videos = Videos(null, null);

        Assert.AreEqual(0, PublishScheduler.Fill(videos, null, Now));
        Assert.AreEqual(0, PublishScheduler.Fill(videos, "   ", Now));
        Assert.IsTrue(videos.All(entry => entry.PublishAt is null));
    }

    [TestMethod]
    public void Replace_rewrites_the_time_of_every_video_in_order()
    {
        var videos = Videos(Now.AddYears(5), Now.AddYears(6), null);

        var changed = PublishScheduler.Replace(videos, "0 * * * *", Now);

        Assert.AreEqual(3, changed);
        CollectionAssert.AreEqual(
            Occurrences("0 * * * *", 3),
            videos.Select(entry => entry.PublishAt!.Value).ToList());
    }

    [TestMethod]
    public void Replace_reports_the_times_that_did_not_change()
    {
        var hourly = Occurrences("0 * * * *", 4);
        var videos = Videos(hourly[0], null, hourly[2], hourly[3]);

        var changed = PublishScheduler.Replace(videos, "0 * * * *", Now);

        Assert.AreEqual(1, changed);
        CollectionAssert.AreEqual(hourly, videos.Select(entry => entry.PublishAt!.Value).ToList());
    }

    [TestMethod]
    public void Replace_is_idempotent_when_the_times_already_match()
    {
        var hourly = Occurrences("0 * * * *", 3);
        var videos = Videos(hourly[0], hourly[1], hourly[2]);

        Assert.AreEqual(0, PublishScheduler.Replace(videos, "0 * * * *", Now));
        CollectionAssert.AreEqual(hourly, videos.Select(entry => entry.PublishAt!.Value).ToList());
    }

    [TestMethod]
    public void Replace_requires_an_expression()
    {
        var videos = Videos((DateTimeOffset?)null);

        var blank = Assert.ThrowsExactly<UtuException>(() => PublishScheduler.Replace(videos, "", Now));
        Assert.AreEqual(2, blank.ExitCode);
        StringAssert.Contains(blank.Message, "crontab");

        var missing = Assert.ThrowsExactly<UtuException>(() => PublishScheduler.Replace(videos, null, Now));
        Assert.AreEqual(2, missing.ExitCode);
        Assert.IsNull(videos[0].PublishAt);
    }

    [TestMethod]
    public void An_invalid_expression_is_reported()
    {
        var videos = Videos(null, null);

        var exception = Assert.ThrowsExactly<UtuException>(() => PublishScheduler.Fill(videos, "every hour", Now));

        Assert.AreEqual(2, exception.ExitCode);
        StringAssert.Contains(exception.Message, "crontab");
    }

    [TestMethod]
    public void A_schedule_that_names_too_few_times_is_reported()
    {
        var videos = Videos(null, null, null, null, null);

        var fill = Assert.ThrowsExactly<UtuException>(() => PublishScheduler.Fill(videos, "0 0 30 2 *", Now));
        Assert.AreEqual(2, fill.ExitCode);
        StringAssert.Contains(fill.Message, "0 0 30 2 *");
        StringAssert.Contains(fill.Message, "5 videos");

        var replace = Assert.ThrowsExactly<UtuException>(() => PublishScheduler.Replace(videos, "0 0 30 2 *", Now));
        Assert.AreEqual(2, replace.ExitCode);
    }

    [TestMethod]
    public void Fill_does_not_count_the_times_it_leaves_alone()
    {
        var hourly = Occurrences("0 * * * *", 1);
        var videos = Videos(hourly[0]);

        Assert.AreEqual(0, PublishScheduler.Fill(videos, "0 * * * *", Now));
        Assert.AreEqual(hourly[0], videos[0].PublishAt);
    }
}
