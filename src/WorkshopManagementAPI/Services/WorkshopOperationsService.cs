#nullable enable
using System.Globalization;
using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.Services;

public static class WorkshopOperationsService
{
    public static string RegisterCustomer(
        string customerId,
        string name,
        string address,
        string postalCode,
        string city,
        string telephoneNumber,
        string emailAddress) =>
        Run(async () =>
        {
            await Repository().RegisterCustomerAsync(
                customerId, name, address, postalCode, city, telephoneNumber, emailAddress);
            return $"{customerId}|{name}|{telephoneNumber}";
        });

    public static string RegisterVehicle(string licenseNumber, string brand, string type, string ownerId) =>
        Run(async () =>
        {
            await Repository().RegisterVehicleAsync(licenseNumber, brand, type, ownerId);
            return $"{licenseNumber}|{brand}|{type}|{ownerId}";
        });

    public static string RegisterPlanning(string planningDate) =>
        Run(async () =>
        {
            var date = await Repository().RegisterPlanningAsync(planningDate);
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        });

    public static string PlanMaintenanceJob(
        string jobId,
        string customerId,
        string licenseNumber,
        string startTime,
        string endTime,
        string description) =>
        Run(async () => FormatJob(await Repository().PlanMaintenanceJobAsync(
            jobId, customerId, licenseNumber, startTime, endTime, description)));

    public static string FinishMaintenanceJob(
        string jobId,
        string actualStartTime,
        string actualEndTime,
        string notes) =>
        Run(async () => FormatJob(await Repository().FinishMaintenanceJobAsync(
            jobId, actualStartTime, actualEndTime, notes)));

    public static string[] GetPlanningByDate(string planningDate) =>
        Run(async () =>
        {
            var jobs = await Repository().GetPlanningByDateAsync(planningDate);
            return jobs.Select(FormatJob).ToArray();
        });

    public static string GetMaintenanceJob(string jobId) =>
        Run(async () =>
        {
            var job = await Repository().GetMaintenanceJobAsync(jobId);
            return job == null ? string.Empty : FormatJob(job);
        });

    private static SqlServerWorkshopOperationsRepository Repository() =>
        new(Required("CUSTOMER_MANAGEMENT_CN"), Required("WORKSHOP_MANAGEMENT_CN"));

    private static T Run<T>(Func<Task<T>> func) =>
        Task.Run(func).ConfigureAwait(false).GetAwaiter().GetResult();

    private static string FormatJob(WorkshopJobRow job) =>
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
