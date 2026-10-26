using System.Globalization;
using Pitstop.Graftcode;
using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.Controllers;

public class WorkshopPlanningController
{
    public static string RegisterPlanning(string planningDate) =>
        GraftAsync.Run(async () =>
        {
            var date = ParseTimestamp(planningDate, nameof(planningDate)).Date;
            var stored = await Planning().RegisterPlanningAsync(date);
            return stored.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        });

    public static string[] GetPlanningByDate(string planningDate) =>
        GraftAsync.Run(async () =>
        {
            var date = ParseTimestamp(planningDate, nameof(planningDate)).Date;
            var jobs = await Planning().GetJobsByDateAsync(date);
            return jobs.Select(FormatJob).ToArray();
        });

    public static string GetMaintenanceJob(string jobId) =>
        GraftAsync.Run(async () =>
        {
            if (!Guid.TryParse(jobId, out var parsedJobId))
            {
                return string.Empty;
            }

            var job = await Planning().GetJobAsync(parsedJobId);
            return job == null ? string.Empty : FormatJob(job);
        });

    private static SqlServerWorkshopPlanningEventSourceRepository Planning() =>
        new(Required("WORKSHOP_MANAGEMENT_CN"));

    private static string FormatJob(MaintenanceJobRow job) =>
        string.Join('|',
            job.Id.ToString("D"),
            job.CustomerId ?? string.Empty,
            job.VehicleLicenseNumber ?? string.Empty,
            FormatTimestamp(job.StartTime),
            FormatTimestamp(job.EndTime),
            job.Description ?? string.Empty,
            job.ActualStartTime == null ? string.Empty : FormatTimestamp(job.ActualStartTime.Value),
            job.ActualEndTime == null ? string.Empty : FormatTimestamp(job.ActualEndTime.Value),
            job.Notes ?? string.Empty,
            job.WorkshopPlanningDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static string FormatTimestamp(DateTime value) =>
        value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseTimestamp(string value, string name)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new InvalidOperationException($"'{name}' is not a valid timestamp.");
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
    }

    private static string Required(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing {key} environment variable.");
        }

        return value;
    }
}
