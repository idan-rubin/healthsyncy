using System.ComponentModel;
using System.Globalization;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Connector.Domain;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HealthSyncy.Connector.EntryPoint.Tools;

[McpServerToolType]
public sealed class ActivityTools(IActivityService activity)
{
    [McpServerTool(Name = "get_activity_day", ReadOnly = true)]
    [Description(
        "Read one local day's activity totals and capture and receipt times in UTC. Missing metrics are unknown.")]
    public async Task<DayRow> GetDay(string day, string timeZone, CancellationToken cancellationToken)
    {
        Validate(day, timeZone);
        return new(day, await activity.GetDayAsync(day, timeZone, cancellationToken));
    }

    [McpServerTool(Name = "get_activity_week", ReadOnly = true)]
    [Description("Read the seven local calendar days ending on the requested date. Missing days have a null snapshot.")]
    public async Task<IReadOnlyList<DayRow>> GetWeek(
        string endingOn,
        string timeZone,
        CancellationToken cancellationToken)
    {
        Validate(endingOn, timeZone);
        var end = DateOnly.ParseExact(endingOn, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (end.DayNumber < 6)
            throw new McpException("Date is outside the reporting range.");
        return await activity.GetWeekAsync(end, timeZone, cancellationToken);
    }

    private static void Validate(string day, string zone)
    {
        if (!Reporting.IsDay(day) || !Reporting.IsZone(zone))
            throw new McpException("Use a YYYY-MM-DD date and IANA time zone.");
    }
}
