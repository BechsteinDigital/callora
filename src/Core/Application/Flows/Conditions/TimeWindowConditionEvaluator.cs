using System.Globalization;
using Callora.Host.PluginContracts.Application.Flows;

namespace Callora.Core.Application.Flows.Conditions;

/// <summary>
/// Matches when the event time falls into a weekly window — business hours.
/// Parameters: "days" (csv: mon,tue,wed,thu,fri,sat,sun; default all),
/// "from"/"to" ("HH:mm", default full day), "timezone" (IANA/Windows id,
/// default UTC). Windows crossing midnight (22:00→06:00) are supported.
/// </summary>
public sealed class TimeWindowConditionEvaluator : IRuleConditionEvaluator
{
    private static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mon"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["sun"] = DayOfWeek.Sunday
    };

    public string Type => "time.window";

    public bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var localTime = ResolveLocalTime(context.Now, parameters);

        if (parameters.TryGetValue("days", out var daysCsv) && !string.IsNullOrWhiteSpace(daysCsv))
        {
            var allowedDays = daysCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(DayNames.ContainsKey)
                .Select(day => DayNames[day])
                .ToHashSet();
            if (!allowedDays.Contains(localTime.DayOfWeek))
            {
                return false;
            }
        }

        var from = ParseTime(parameters, "from") ?? TimeOnly.MinValue;
        var to = ParseTime(parameters, "to") ?? TimeOnly.MaxValue;
        var now = TimeOnly.FromDateTime(localTime);

        return from <= to
            ? now >= from && now <= to
            : now >= from || now <= to;
    }

    private static DateTime ResolveLocalTime(DateTimeOffset now, IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("timezone", out var timezoneId) && !string.IsNullOrWhiteSpace(timezoneId))
        {
            try
            {
                var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId.Trim());
                return TimeZoneInfo.ConvertTime(now, timezone).DateTime;
            }
            catch (TimeZoneNotFoundException)
            {
                // Unknown zone falls back to UTC below.
            }
        }

        return now.UtcDateTime;
    }

    private static TimeOnly? ParseTime(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var raw) &&
        TimeOnly.TryParseExact(raw?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
}
