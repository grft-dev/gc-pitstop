using System.Globalization;
using Pitstop.Graftcode;
using Pitstop.WorkshopManagementAPI.Domain.BusinessRules;
using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.CommandHandlers;

public class PlanMaintenanceJobCommandHandler
{
    public static string PlanMaintenanceJob(
        string jobId,
        string customerId,
        string licenseNumber,
        string startTime,
        string endTime,
        string description) =>
        GraftAsync.Run(async () =>
        {
            Require(jobId, nameof(jobId));
            Require(customerId, nameof(customerId));
            Require(licenseNumber, nameof(licenseNumber));
            var start = ParseTimestamp(startTime, nameof(startTime));
            var end = ParseTimestamp(endTime, nameof(endTime));
            MaintenanceJobRules.PlannedMaintenanceJobShouldFallWithinOneBusinessDay(start, end);
            if (!Guid.TryParse(jobId, out var parsedJobId))
            {
                throw new InvalidOperationException($"'{nameof(jobId)}' is not a valid identifier.");
            }

            var customer = await RefData().GetCustomerAsync(customerId);
            if (customer == null)
            {
                throw new InvalidOperationException($"Customer '{customerId}' was not found.");
            }

            var vehicle = await RefData().GetVehicleAsync(licenseNumber);
            if (vehicle == null)
            {
                throw new InvalidOperationException($"Vehicle '{licenseNumber}' was not found.");
            }

            var existing = await Planning().GetJobsByDateAsync(start.Date);
            WorkshopPlanningRules.NumberOfParallelMaintenanceJobsOnAVehicleMustNotExceedOne(
                existing.Select(job => (LicenseNumber: job.VehicleLicenseNumber, Start: job.StartTime, End: job.EndTime)),
                licenseNumber,
                start,
                end);
            WorkshopPlanningRules.NumberOfParallelMaintenanceJobsMustNotExceedAvailableWorkStations(
                existing.Select(job => (Start: job.StartTime, End: job.EndTime)),
                start,
                end);

            var planned = new MaintenanceJobRow
            {
                Id = parsedJobId,
                CustomerId = customerId,
                VehicleLicenseNumber = licenseNumber,
                StartTime = start,
                EndTime = end,
                Description = description,
                WorkshopPlanningDate = start.Date
            };
            await Planning().InsertJobAsync(planned);
            return FormatJob(planned);
        });

    private static SqlServerRefDataRepository RefData() =>
        new(Required("CUSTOMER_MANAGEMENT_CN"), Required("WORKSHOP_MANAGEMENT_CN"));

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
