#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.WorkshopManagementAPI.Repositories;

public sealed class SqlServerWorkshopOperationsRepository
{
    private const string LicensePattern = @"^((\d{1,3}|[a-z]{1,3})-){2}(\d{1,3}|[a-z]{1,3})$";
    private const string JobSpansMoreThanOneDay = "Start-time and end-time of a Maintenance Job must be within a 1 day.";
    private const string TooManyParallelJobs = "Maintenancejob overlaps with more than 3 other jobs.";
    private const string VehicleAlreadyBooked = "Only 1 maintenance job can be executed on a vehicle during a certain time-slot.";
    private const string JobAlreadyFinished = "An already finished job can not be finished.";

    private readonly string _customerConnectionString;
    private readonly string _workshopConnectionString;

    public SqlServerWorkshopOperationsRepository(string customerConnectionString, string workshopConnectionString)
    {
        _customerConnectionString = customerConnectionString;
        _workshopConnectionString = workshopConnectionString;
    }

    public async Task RegisterCustomerAsync(
        string customerId,
        string name,
        string address,
        string postalCode,
        string city,
        string telephoneNumber,
        string emailAddress)
    {
        Require(customerId, nameof(customerId));
        Require(name, nameof(name));

        using var conn = new SqlConnection(_customerConnectionString);
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
    }

    public async Task RegisterVehicleAsync(string licenseNumber, string brand, string type, string ownerId)
    {
        Require(licenseNumber, nameof(licenseNumber));
        Require(ownerId, nameof(ownerId));
        if (!Regex.IsMatch(licenseNumber, LicensePattern, RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException(
                $"The specified license-number '{licenseNumber}' was not in the correct format.");
        }

        using (var customers = new SqlConnection(_customerConnectionString))
        {
            var owner = await customers.QueryFirstOrDefaultAsync<string>(
                "select CustomerId from Customer where CustomerId = @CustomerId",
                new { CustomerId = ownerId });
            if (owner == null)
            {
                throw new InvalidOperationException($"Customer '{ownerId}' was not found.");
            }
        }

        using var conn = new SqlConnection(_workshopConnectionString);
        await conn.ExecuteAsync(
            """
            insert into Vehicle (LicenseNumber, Brand, [Type], OwnerId)
            values (@LicenseNumber, @Brand, @Type, @OwnerId)
            """,
            new { LicenseNumber = licenseNumber, Brand = brand, Type = type, OwnerId = ownerId });
    }

    public async Task<DateTime> RegisterPlanningAsync(string planningDate)
    {
        var date = ParseTimestamp(planningDate, nameof(planningDate)).Date;
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var conn = new SqlConnection(_workshopConnectionString);
        var existing = await conn.QueryFirstOrDefaultAsync<DateTime?>(
            "select PlanningDate from WorkshopPlanning where PlanningDate = @PlanningDate",
            new { PlanningDate = date });
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"Workshop planning for '{dateText}' is already registered.");
        }

        await conn.ExecuteAsync(
            "insert into WorkshopPlanning (PlanningDate) values (@PlanningDate)",
            new { PlanningDate = date });
        return date;
    }

    public async Task<WorkshopJobRow> PlanMaintenanceJobAsync(
        string jobId,
        string customerId,
        string licenseNumber,
        string startTime,
        string endTime,
        string description)
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

        using (var customers = new SqlConnection(_customerConnectionString))
        {
            var owner = await customers.QueryFirstOrDefaultAsync<string>(
                "select CustomerId from Customer where CustomerId = @CustomerId",
                new { CustomerId = customerId });
            if (owner == null)
            {
                throw new InvalidOperationException($"Customer '{customerId}' was not found.");
            }
        }

        using var conn = new SqlConnection(_workshopConnectionString);
        var vehicle = await conn.QueryFirstOrDefaultAsync<string>(
            "select LicenseNumber from Vehicle where LicenseNumber = @LicenseNumber",
            new { LicenseNumber = licenseNumber });
        if (vehicle == null)
        {
            throw new InvalidOperationException($"Vehicle '{licenseNumber}' was not found.");
        }

        var existing = (await conn.QueryAsync<WorkshopJobRow>(
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

        var planned = new WorkshopJobRow
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
            if not exists (select 1 from WorkshopPlanning where PlanningDate = @PlanningDate)
                insert into WorkshopPlanning (PlanningDate) values (@PlanningDate)
            """,
            new { PlanningDate = start.Date });

        await conn.ExecuteAsync(
            """
            insert into MaintenanceJob
                (Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description, WorkshopPlanningDate)
            values
                (@Id, @CustomerId, @VehicleLicenseNumber, @StartTime, @EndTime, @Description, @WorkshopPlanningDate)
            """,
            planned);

        return planned;
    }

    public async Task<WorkshopJobRow> FinishMaintenanceJobAsync(
        string jobId,
        string actualStartTime,
        string actualEndTime,
        string notes)
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

        using var conn = new SqlConnection(_workshopConnectionString);
        var job = await conn.QueryFirstOrDefaultAsync<WorkshopJobRow>(
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

        return job;
    }

    public async Task<IReadOnlyList<WorkshopJobRow>> GetPlanningByDateAsync(string planningDate)
    {
        var date = ParseTimestamp(planningDate, nameof(planningDate)).Date;
        using var conn = new SqlConnection(_workshopConnectionString);
        var jobs = await conn.QueryAsync<WorkshopJobRow>(
            """
            select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                   ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
            from MaintenanceJob
            where cast(WorkshopPlanningDate as date) = cast(@PlanningDate as date)
            order by StartTime
            """,
            new { PlanningDate = date });
        return jobs.ToArray();
    }

    public async Task<WorkshopJobRow?> GetMaintenanceJobAsync(string jobId)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            return null;
        }

        using var conn = new SqlConnection(_workshopConnectionString);
        return await conn.QueryFirstOrDefaultAsync<WorkshopJobRow>(
            """
            select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                   ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
            from MaintenanceJob
            where Id = @Id
            """,
            new { Id = parsedJobId });
    }

    private static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) =>
        start < otherEnd && end > otherStart;

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
}

public sealed class WorkshopJobRow
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
