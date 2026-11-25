using System.ComponentModel;
using System.Globalization;
using HealthSyncy.BL;
using HealthSyncy.Core;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace HealthSyncy.Connector.EntryPoint.Tools;

[McpServerToolType]
public sealed class ActivityTools(IActivityBL activity)
{
    [McpServerTool(Name = "get_activity_day", ReadOnly = true), Description("Read one local day's activity totals and capture and receipt times in UTC. Missing metrics are unknown.")]
    public async Task<DayRow> Day(string day, string timeZone, CancellationToken ct)
    {
        Validate(day, timeZone);
        return new(day, await activity.Day(day, timeZone, ct));
    }

    [McpServerTool(Name = "get_activity_week", ReadOnly = true), Description("Read the seven local calendar days ending on the requested date. Missing days have a null snapshot.")]
    public async Task<IReadOnlyList<DayRow>> Week(string endingOn, string timeZone, CancellationToken ct)
    {
        Validate(endingOn, timeZone);
        var end = DateOnly.ParseExact(endingOn, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (end.DayNumber < 6) throw new McpException("Date is outside the reporting range.");
        return await activity.Week(end, timeZone, ct);
    }

    private static void Validate(string day, string zone)
    {
        if (!Reporting.IsDay(day) || !Reporting.TryZone(zone, out _))
            throw new McpException("Use a YYYY-MM-DD date and IANA time zone.");
    }
}
