#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.WorkshopManagementAPI.Services;

public static class WorkshopOperationsService
{
    private const string LicensePattern = @"^((\d{1,3}|[a-z]{1,3})-){2}(\d{1,3}|[a-z]{1,3})$";
    private const string JobSpansMoreThanOneDay = "Start-time and end-time of a Maintenance Job must be within a 1 day.";
    private const string TooManyParallelJobs = "Maintenancejob overlaps with more than 3 other jobs.";
    private const string VehicleAlreadyBooked = "Only 1 maintenance job can be executed on a vehicle during a certain time-slot.";
    private const string JobAlreadyFinished = "An already finished job can not be finished.";

    public static string RegisterCustomerAsync(
        string customerId,
        string name,
        string address,
        string postalCode,
        string city,
        string telephoneNumber,
        string emailAddress) =>
        Run(async () =>
        {
            Require(customerId, nameof(customerId));
            Require(name, nameof(name));

            using var conn = new SqlConnection(Required("CUSTOMER_MANAGEMENT_CN"));
            await conn.ExecuteAsync(
                """
                insert into Customer (CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress)
                values (@CustomerId, @Name, @Address, @PostalCode, @City, @TelephoneNumber, @EmailAddress)
                """,
                new
                {
                    CustomerId = customerId,
                    Name = name,
                    Address = address,
                    PostalCode = postalCode,
                    City = city,
                    TelephoneNumber = telephoneNumber,
                    EmailAddress = emailAddress
                });

            return $"{customerId}|{name}|{telephoneNumber}";
        });

    public static string RegisterVehicleAsync(string licenseNumber, string brand, string type, string ownerId) =>
        Run(async () =>
        {
            Require(licenseNumber, nameof(licenseNumber));
            Require(ownerId, nameof(ownerId));
            if (!Regex.IsMatch(licenseNumber, LicensePattern, RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The specified license-number '{licenseNumber}' was not in the correct format.");
            }

            using (var customers = new SqlConnection(Required("CUSTOMER_MANAGEMENT_CN")))
            {
                var owner = await customers.QueryFirstOrDefaultAsync<string>(
                    "select CustomerId from Customer where CustomerId = @CustomerId",
                    new { CustomerId = ownerId });
                if (owner == null)
                {
                    throw new InvalidOperationException($"Customer '{ownerId}' was not found.");
                }
            }

            using var conn = new SqlConnection(Required("WORKSHOP_MANAGEMENT_CN"));
            await conn.ExecuteAsync(
                """
                insert into Vehicle (LicenseNumber, Brand, [Type], OwnerId)
                values (@LicenseNumber, @Brand, @Type, @OwnerId)
                """,
                new { LicenseNumber = licenseNumber, Brand = brand, Type = type, OwnerId = ownerId });

            return $"{licenseNumber}|{brand}|{type}|{ownerId}";
        });

    public static string PlanMaintenanceJobAsync(
        string jobId,
        string customerId,
        string licenseNumber,
        string startTime,
        string endTime,
        string description) =>
        Run(async () =>
        {
            Require(jobId, nameof(jobId));
            Require(customerId, nameof(customerId));
            Require(licenseNumber, nameof(licenseNumber));
            var start = ParseTimestamp(startTime, nameof(startTime));
            var end = ParseTimestamp(endTime, nameof(endTime));
            if (end <= start || start.Date != end.Date)
            {
                throw new InvalidOperationException(JobSpansMoreThanOneDay);
            }

            if (!Guid.TryParse(jobId, out var parsedJobId))
            {
                throw new InvalidOperationException($"'{nameof(jobId)}' is not a valid identifier.");
            }

            using (var customers = new SqlConnection(Required("CUSTOMER_MANAGEMENT_CN")))
            {
                var owner = await customers.QueryFirstOrDefaultAsync<string>(
                    "select CustomerId from Customer where CustomerId = @CustomerId",
                    new { CustomerId = customerId });
                if (owner == null)
                {
                    throw new InvalidOperationException($"Customer '{customerId}' was not found.");
                }
            }

            using var conn = new SqlConnection(Required("WORKSHOP_MANAGEMENT_CN"));
            var vehicle = await conn.QueryFirstOrDefaultAsync<string>(
                "select LicenseNumber from Vehicle where LicenseNumber = @LicenseNumber",
                new { LicenseNumber = licenseNumber });
            if (vehicle == null)
            {
                throw new InvalidOperationException($"Vehicle '{licenseNumber}' was not found.");
            }

            var existing = (await conn.QueryAsync<JobRow>(
                """
                select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                       ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
                from MaintenanceJob
                where cast(WorkshopPlanningDate as date) = cast(@PlanningDate as date)
                """,
                new { PlanningDate = start.Date })).ToArray();

            var overlaps = existing.Where(job => Overlaps(start, end, job.StartTime, job.EndTime)).ToArray();
            if (overlaps.Any(job => string.Equals(job.VehicleLicenseNumber, licenseNumber, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(VehicleAlreadyBooked);
            }

            if (overlaps.Length >= 3)
            {
                throw new InvalidOperationException(TooManyParallelJobs);
            }

            var planned = new JobRow
            {
                Id = parsedJobId,
                CustomerId = customerId,
                VehicleLicenseNumber = licenseNumber,
                StartTime = start,
                EndTime = end,
                Description = description,
                WorkshopPlanningDate = start.Date
            };

            await conn.ExecuteAsync(
                """
                insert into MaintenanceJob
                    (Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description, WorkshopPlanningDate)
                values
                    (@Id, @CustomerId, @VehicleLicenseNumber, @StartTime, @EndTime, @Description, @WorkshopPlanningDate)
                """,
                planned);

            return FormatJob(planned);
        });

    public static string FinishMaintenanceJobAsync(
        string jobId,
        string actualStartTime,
        string actualEndTime,
        string notes) =>
        Run(async () =>
        {
            Require(jobId, nameof(jobId));
            if (!Guid.TryParse(jobId, out var parsedJobId))
            {
                throw new InvalidOperationException($"'{nameof(jobId)}' is not a valid identifier.");
            }

            var actualStart = ParseTimestamp(actualStartTime, nameof(actualStartTime));
            var actualEnd = ParseTimestamp(actualEndTime, nameof(actualEndTime));
            if (actualEnd <= actualStart)
            {
                throw new InvalidOperationException(JobSpansMoreThanOneDay);
            }

            using var conn = new SqlConnection(Required("WORKSHOP_MANAGEMENT_CN"));
            var job = await conn.QueryFirstOrDefaultAsync<JobRow>(
                """
                select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                       ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
                from MaintenanceJob
                where Id = @Id
                """,
                new { Id = parsedJobId });
            if (job == null)
            {
                throw new InvalidOperationException($"Maintenance job '{jobId}' was not found.");
            }

            if (job.ActualStartTime != null || job.ActualEndTime != null)
            {
                throw new InvalidOperationException(JobAlreadyFinished);
            }

            job.ActualStartTime = actualStart;
            job.ActualEndTime = actualEnd;
            job.Notes = notes;
            await conn.ExecuteAsync(
                """
                update MaintenanceJob
                set ActualStartTime = @ActualStartTime,
                    ActualEndTime = @ActualEndTime,
                    Notes = @Notes
                where Id = @Id
                """,
                job);

            return FormatJob(job);
        });

    public static string[] GetPlanningByDateAsync(string planningDate) =>
        Run(async () =>
        {
            var date = ParseTimestamp(planningDate, nameof(planningDate)).Date;
            using var conn = new SqlConnection(Required("WORKSHOP_MANAGEMENT_CN"));
            var jobs = await conn.QueryAsync<JobRow>(
                """
                select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                       ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
                from MaintenanceJob
                where cast(WorkshopPlanningDate as date) = cast(@PlanningDate as date)
                order by StartTime
                """,
                new { PlanningDate = date });
            return jobs.Select(FormatJob).ToArray();
        });

    public static string GetMaintenanceJobAsync(string jobId) =>
        Run(async () =>
        {
            if (!Guid.TryParse(jobId, out var parsedJobId))
            {
                return string.Empty;
            }

            using var conn = new SqlConnection(Required("WORKSHOP_MANAGEMENT_CN"));
            var job = await conn.QueryFirstOrDefaultAsync<JobRow>(
                """
                select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                       ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
                from MaintenanceJob
                where Id = @Id
                """,
                new { Id = parsedJobId });
            return job == null ? string.Empty : FormatJob(job);
        });

    private static T Run<T>(Func<Task<T>> func) =>
        Task.Run(func).ConfigureAwait(false).GetAwaiter().GetResult();

    private static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;

    private static string FormatJob(JobRow job) =>
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

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"'{name}' is required.");
        }
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

    private sealed class JobRow
    {
        public Guid Id { get; set; }
        public string? CustomerId { get; set; }
        public string? VehicleLicenseNumber { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Description { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string? Notes { get; set; }
        public DateTime WorkshopPlanningDate { get; set; }
    }
}
