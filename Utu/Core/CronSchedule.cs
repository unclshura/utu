using System.Diagnostics.CodeAnalysis;

namespace Utu.Core;

/// <summary>
/// A crontab expression of five fields (<c>minute hour day-of-month month
/// day-of-week</c>) or of six fields with a leading <c>second</c>. It names the
/// publication times that <c>utu init</c> spreads over the videos.
/// </summary>
/// <remarks>
/// A field accepts <c>*</c>, a list separated by <c>,</c>, a range
/// (<c>8-10</c>), a step (<c>*/15</c>, <c>10-40/10</c> or <c>5/10</c>), and the
/// three letter names of a month (<c>JAN</c>) or of a weekday (<c>MON</c>;
/// <c>7</c> is Sunday). The macros <c>@yearly</c>, <c>@monthly</c>,
/// <c>@weekly</c>, <c>@daily</c> and <c>@hourly</c> are understood too.
/// Day of month and day of week are combined the way cron does: when neither of
/// them starts with <c>*</c> a day matches when either of them matches.
/// Times are read in the time zone of the schedule and carry the offset that
/// zone has at that time.
/// </remarks>
public sealed class CronSchedule
{
    /// <summary>How far ahead a single publication time is searched for.</summary>
    private const int SearchDays = (366 * 10) + 1;

    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JAN"] = 1,
        ["FEB"] = 2,
        ["MAR"] = 3,
        ["APR"] = 4,
        ["MAY"] = 5,
        ["JUN"] = 6,
        ["JUL"] = 7,
        ["AUG"] = 8,
        ["SEP"] = 9,
        ["OCT"] = 10,
        ["NOV"] = 11,
        ["DEC"] = 12,
    };

    private static readonly Dictionary<string, int> DayOfWeekNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUN"] = 0,
        ["MON"] = 1,
        ["TUE"] = 2,
        ["WED"] = 3,
        ["THU"] = 4,
        ["FRI"] = 5,
        ["SAT"] = 6,
    };

    private static readonly Dictionary<string, string> Macros = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@yearly"] = "0 0 1 1 *",
        ["@annually"] = "0 0 1 1 *",
        ["@monthly"] = "0 0 1 * *",
        ["@weekly"] = "0 0 * * 0",
        ["@daily"] = "0 0 * * *",
        ["@midnight"] = "0 0 * * *",
        ["@hourly"] = "0 * * * *",
    };

    private readonly Fields _fields;
    private readonly TimeZoneInfo _timeZone;

    private CronSchedule(string expression, TimeZoneInfo timeZone, Fields fields)
    {
        Expression = expression;
        _timeZone = timeZone;
        _fields = fields;
    }

    /// <summary>The expression as it was written, without the surrounding whitespace.</summary>
    public string Expression { get; }

    /// <summary>
    /// Parses <paramref name="expression" />.
    /// </summary>
    /// <param name="expression">The crontab expression.</param>
    /// <param name="timeZone">Zone the times are read in; the local zone by default.</param>
    /// <exception cref="UtuException">The expression is not a valid crontab expression.</exception>
    public static CronSchedule Parse(string expression, TimeZoneInfo? timeZone = null) =>
        TryParse(expression, out var schedule, out var error, timeZone)
            ? schedule
            : throw new UtuException($"'{expression}' is not a valid crontab expression: {error}", 2);

    /// <summary>
    /// Parses <paramref name="expression" />, returning <see langword="false" />
    /// instead of throwing when it is not valid.
    /// </summary>
    /// <param name="expression">The crontab expression.</param>
    /// <param name="schedule">The parsed schedule.</param>
    /// <param name="error">Why the expression was rejected.</param>
    /// <param name="timeZone">Zone the times are read in; the local zone by default.</param>
    public static bool TryParse(
        string? expression,
        [NotNullWhen(true)] out CronSchedule? schedule,
        [NotNullWhen(false)] out string? error,
        TimeZoneInfo? timeZone = null)
    {
        schedule = null;
        error = null;

        var text = expression?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            error = "it is empty";
            return false;
        }

        var written = text;

        if (text[0] == '@')
        {
            if (!Macros.TryGetValue(text, out var macro))
            {
                error = $"'{text}' is not a known macro; use {string.Join(", ", Macros.Keys)}";
                return false;
            }

            text = macro;
        }

        var fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (fields.Length is not (5 or 6))
        {
            error = $"expected 5 fields (minute hour day-of-month month day-of-week) or 6 with a leading second but found {fields.Length}";
            return false;
        }

        var index = 0;
        int[] seconds = [0];

        if (fields.Length == 6 && !ParseField(fields[index++], "second", 0, 59, null, out seconds, out _, out error))
        {
            return false;
        }

        if (!ParseField(fields[index++], "minute", 0, 59, null, out var minutes, out _, out error)
            || !ParseField(fields[index++], "hour", 0, 23, null, out var hours, out _, out error)
            || !ParseField(fields[index++], "day of month", 1, 31, null, out var daysOfMonth, out var dayOfMonthRestricted, out error)
            || !ParseField(fields[index++], "month", 1, 12, MonthNames, out var months, out _, out error)
            || !ParseField(fields[index++], "day of week", 0, 7, DayOfWeekNames, out var daysOfWeek, out var dayOfWeekRestricted, out error))
        {
            return false;
        }

        daysOfWeek = [.. daysOfWeek.Select(day => day == 7 ? 0 : day).Distinct().Order()];

        schedule = new CronSchedule(
            written,
            timeZone ?? TimeZoneInfo.Local,
            new Fields(seconds, minutes, hours, daysOfMonth, dayOfMonthRestricted, months, daysOfWeek, dayOfWeekRestricted));

        return true;
    }

    /// <summary>The publication times after <paramref name="after" />, in order.</summary>
    /// <param name="after">Moment the first publication time has to be after.</param>
    public IEnumerable<DateTimeOffset> Occurrences(DateTimeOffset after)
    {
        var cursor = after;

        while (Next(cursor) is { } next)
        {
            yield return next;
            cursor = next;
        }
    }

    /// <summary>
    /// The first time the schedule matches after <paramref name="after" />, or
    /// <see langword="null" /> when it never matches within the next
    /// <see cref="SearchDays" /> days.
    /// </summary>
    /// <param name="after">Moment the publication time has to be after.</param>
    public DateTimeOffset? Next(DateTimeOffset after)
    {
        var wall = TimeZoneInfo.ConvertTime(after, _timeZone).DateTime;
        wall = wall.AddTicks(-(wall.Ticks % TimeSpan.TicksPerSecond));

        var first = wall.Date;

        for (var offset = 0; offset < SearchDays; offset++)
        {
            var date = first.AddDays(offset);

            if (!Matches(date))
            {
                continue;
            }

            foreach (var hour in _fields.Hours)
            {
                foreach (var minute in _fields.Minutes)
                {
                    foreach (var second in _fields.Seconds)
                    {
                        var candidate = date.AddHours(hour).AddMinutes(minute).AddSeconds(second);

                        if (candidate <= wall)
                        {
                            continue;
                        }

                        var moment = new DateTimeOffset(candidate, _timeZone.GetUtcOffset(candidate));

                        if (moment > after)
                        {
                            return moment;
                        }
                    }
                }
            }
        }

        return null;
    }

    private bool Matches(DateTime date)
    {
        if (Array.BinarySearch(_fields.Months, date.Month) < 0)
        {
            return false;
        }

        var dayOfMonth = Array.BinarySearch(_fields.DaysOfMonth, date.Day) >= 0;
        var dayOfWeek = Array.BinarySearch(_fields.DaysOfWeek, (int)date.DayOfWeek) >= 0;

        return _fields.DayOfMonthRestricted && _fields.DayOfWeekRestricted
            ? dayOfMonth || dayOfWeek
            : dayOfMonth && dayOfWeek;
    }

    private static bool ParseField(
        string text,
        string name,
        int minimum,
        int maximum,
        IReadOnlyDictionary<string, int>? names,
        out int[] values,
        out bool restricted,
        [NotNullWhen(false)] out string? error)
    {
        values = [];
        error = null;

        // cron treats a field that starts with '*' as unrestricted, which is
        // what decides how day of month and day of week are combined.
        restricted = !text.StartsWith('*');

        var result = new SortedSet<int>();

        foreach (var token in text.Split(','))
        {
            if (token.Length == 0)
            {
                error = $"{name}: '{text}' has an empty item";
                return false;
            }

            var parts = token.Split('/');

            if (parts.Length > 2 || parts.Any(part => part.Length == 0))
            {
                error = $"{name}: '{token}' is not a step";
                return false;
            }

            var step = 1;

            if (parts.Length == 2 && (!int.TryParse(parts[1], out step) || step < 1))
            {
                error = $"{name}: '{parts[1]}' is not a step size";
                return false;
            }

            int first;
            int last;

            if (parts[0] == "*")
            {
                first = minimum;
                last = maximum;
            }
            else
            {
                var range = parts[0].Split('-', 2);

                if (!Value(name, range[0], minimum, maximum, names, out first, out error))
                {
                    return false;
                }

                if (range.Length == 1)
                {
                    // 'a/n' means 'a' to the end of the field.
                    last = parts.Length == 2 ? maximum : first;
                }
                else if (!Value(name, range[1], minimum, maximum, names, out last, out error))
                {
                    return false;
                }
                else if (last < first)
                {
                    error = $"{name}: the range '{parts[0]}' runs backwards";
                    return false;
                }
            }

            for (var value = first; value <= last; value += step)
            {
                result.Add(value);
            }
        }

        values = [.. result];
        return true;
    }

    private static bool Value(
        string name,
        string text,
        int minimum,
        int maximum,
        IReadOnlyDictionary<string, int>? names,
        out int value,
        [NotNullWhen(false)] out string? error)
    {
        error = null;

        if (names is not null && names.TryGetValue(text, out value))
        {
            return true;
        }

        if (int.TryParse(text, out value))
        {
            if (value < minimum || value > maximum)
            {
                error = $"{name}: {value} is outside {minimum}-{maximum}";
                return false;
            }

            return true;
        }

        error = names is null
            ? $"{name}: '{text}' is not a number"
            : $"{name}: '{text}' is not a number or one of {string.Join(", ", names.Keys)}";

        return false;
    }

    private sealed record Fields(
        int[] Seconds,
        int[] Minutes,
        int[] Hours,
        int[] DaysOfMonth,
        bool DayOfMonthRestricted,
        int[] Months,
        int[] DaysOfWeek,
        bool DayOfWeekRestricted);
}
