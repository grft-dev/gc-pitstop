using System.Globalization;
using Pitstop.Graftcode;
using Pitstop.WorkshopManagementAPI.Domain.BusinessRules;
using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.CommandHandlers;

public class FinishMaintenanceJobCommandHandler
{
    public static string FinishMaintenanceJob(
        string jobId,
        string actualStartTime,
        string actualEndTime,
        string notes) =>
        GraftAsync.Run(async () =>
        {
            Require(jobId, nameof(jobId));
            if (!Guid.TryParse(jobId, out var parsedJobId))
            {
                throw new InvalidOperationException($"'{nameof(jobId)}' is not a valid identifier.");
            }

            var actualStart = ParseTimestamp(actualStartTime, nameof(actualStartTime));
            var actualEnd = ParseTimestamp(actualEndTime, nameof(actualEndTime));
            MaintenanceJobRules.PlannedMaintenanceJobShouldFallWithinOneBusinessDay(actualStart, actualEnd);

            var job = await Planning().GetJobAsync(parsedJobId);
            if (job == null)
            {
                throw new InvalidOperationException($"Maintenance job '{jobId}' was not found.");
            }

            MaintenanceJobRules.FinishedMaintenanceJobCanNotBeFinished(
                job.ActualStartTime != null || job.ActualEndTime != null);

            job.ActualStartTime = actualStart;
            job.ActualEndTime = actualEnd;
            job.Notes = notes;
            await Planning().UpdateCompletionAsync(job);
            return FormatJob(job);
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

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"'{name}' is required.");
        }
    }
}
