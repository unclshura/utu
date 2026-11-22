using Utu.Core;

namespace Utu.Tests;

[TestClass]
public sealed class CronScheduleTests
{
    private static readonly DateTimeOffset Start = new(2024, 1, 1, 10, 30, 45, TimeSpan.Zero);

    [TestMethod]
    public void An_hourly_schedule_names_the_next_hour_first()
    {
        Assert.AreEqual(
            new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero),
            Next("0 * * * *"));
    }

    [TestMethod]
    public void Times_are_strictly_after_the_moment_they_are_counted_from()
    {
        var moment = new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero);

        Assert.AreEqual(
            new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
            CronSchedule.Parse("0 * * * *", TimeZoneInfo.Utc).Next(moment));
    }

    [TestMethod]
    public void Occurrences_are_spread_over_the_videos_in_order()
    {
        var expected = new List<DateTimeOffset?>
        {
            new DateTimeOffset(2024, 1, 1, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.Zero),
        };

        CollectionAssert.AreEqual(expected, Occurrences("0 * * * *", 3));
    }

    [TestMethod]
    [DataRow("*/15 * * * *", "2024-01-01T10:45:00+00:00")]
    [DataRow("5/10 * * * *", "2024-01-01T10:35:00+00:00")]
    [DataRow("0 10-12 * * *", "2024-01-01T11:00:00+00:00")]
    [DataRow("0 0 1 * *", "2024-02-01T00:00:00+00:00")]
    [DataRow("0 0 13 * 5", "2024-01-05T00:00:00+00:00")]
    [DataRow("0 0 * * MON-FRI", "2024-01-02T00:00:00+00:00")]
    [DataRow("0 0 1 JAN,APR *", "2024-04-01T00:00:00+00:00")]
    [DataRow("0 0 * * 7", "2024-01-07T00:00:00+00:00")]
    [DataRow("0 0 29 2 *", "2024-02-29T00:00:00+00:00")]
    [DataRow("30 0 0 * * *", "2024-01-02T00:00:30+00:00")]
    [DataRow("@hourly", "2024-01-01T11:00:00+00:00")]
    [DataRow("@daily", "2024-01-02T00:00:00+00:00")]
    [DataRow("@weekly", "2024-01-07T00:00:00+00:00")]
    [DataRow("@monthly", "2024-02-01T00:00:00+00:00")]
    [DataRow("@yearly", "2025-01-01T00:00:00+00:00")]
    public void A_schedule_names_the_expected_moment(string expression, string expected) =>
        Assert.AreEqual(DateTimeOffset.Parse(expected), Next(expression));

    [TestMethod]
    public void Day_of_month_and_day_of_week_are_combined_the_way_cron_does()
    {
        // Both fields are restricted, so a day matches when either of them matches.
        CollectionAssert.AreEqual(
            new List<DateTimeOffset?>
            {
                new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2024, 1, 12, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2024, 1, 13, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2024, 1, 19, 0, 0, 0, TimeSpan.Zero),
            },
            Occurrences("0 0 13 * 5", 4));

        // One of them is unrestricted, so the other one alone decides.
        Assert.AreEqual(new DateTimeOffset(2024, 1, 13, 0, 0, 0, TimeSpan.Zero), Next("0 0 13 * *"));
    }

    [TestMethod]
    public void Every_leap_day_is_named()
    {
        CollectionAssert.AreEqual(
            new List<DateTimeOffset?>
            {
                new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2028, 2, 29, 0, 0, 0, TimeSpan.Zero),
            },
            Occurrences("0 0 29 2 *", 2));
    }

    [TestMethod]
    public void A_schedule_that_never_matches_has_no_occurrences()
    {
        var schedule = CronSchedule.Parse("0 0 30 2 *", TimeZoneInfo.Utc);

        Assert.IsNull(schedule.Next(Start));
        Assert.AreEqual(0, schedule.Occurrences(Start).Take(1).Count());
    }

    [TestMethod]
    public void Times_are_read_in_the_zone_of_the_schedule()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("utu-test", TimeSpan.FromHours(2), "utu test", "utu test");
        var schedule = CronSchedule.Parse("0 9 * * *", zone);

        var next = schedule.Next(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.FromHours(2)), next);
        Assert.AreEqual(new DateTime(2024, 1, 1, 7, 0, 0), next!.Value.UtcDateTime);
    }

    [TestMethod]
    public void The_local_zone_is_used_when_none_is_given()
    {
        var next = CronSchedule.Parse("0 9 * * *").Next(Start.AddDays(1));

        Assert.AreEqual(9, next!.Value.Hour);
        Assert.AreEqual(0, next.Value.Minute);
    }

    [TestMethod]
    public void The_expression_is_kept_the_way_it_was_written()
    {
        Assert.AreEqual("@hourly", CronSchedule.Parse("@hourly", TimeZoneInfo.Utc).Expression);
        Assert.AreEqual("0  9 * * 1-5", CronSchedule.Parse(" 0  9 * * 1-5 ", TimeZoneInfo.Utc).Expression);
    }

    [TestMethod]
    public void Parsing_an_invalid_expression_reports_it()
    {
        var exception = Assert.ThrowsExactly<UtuException>(() => CronSchedule.Parse("every hour"));

        Assert.AreEqual(2, exception.ExitCode);
        StringAssert.Contains(exception.Message, "crontab");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("@reboot")]
    [DataRow("0 0 *")]
    [DataRow("0 0 * * * * *")]
    [DataRow("60 0 * * *")]
    [DataRow("0 24 * * *")]
    [DataRow("0 0 0 * *")]
    [DataRow("0 0 * 13 *")]
    [DataRow("0 0 * * 8")]
    [DataRow("x 0 * * *")]
    [DataRow("*/0 * * * *")]
    [DataRow("5-1 * * * *")]
    [DataRow("1,,2 * * * *")]
    [DataRow("0 0 * * FOO")]
    public void An_expression_outside_the_syntax_is_rejected(string expression)
    {
        Assert.IsFalse(
            CronSchedule.TryParse(expression, out var schedule, out var error, TimeZoneInfo.Utc),
            $"{expression} was accepted");

        Assert.IsNull(schedule);
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    private static DateTimeOffset? Next(string expression) =>
        CronSchedule.Parse(expression, TimeZoneInfo.Utc).Next(Start);

    private static List<DateTimeOffset?> Occurrences(string expression, int count) =>
        [.. CronSchedule.Parse(expression, TimeZoneInfo.Utc).Occurrences(Start).Take(count).Select(moment => (DateTimeOffset?)moment)];
}
