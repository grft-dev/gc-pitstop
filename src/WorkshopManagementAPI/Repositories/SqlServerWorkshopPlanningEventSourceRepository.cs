using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.WorkshopManagementAPI.Repositories;

// The event store was folded into the WorkshopPlanning and MaintenanceJob tables.
// This type still persists a planning day and its jobs.
internal sealed class SqlServerWorkshopPlanningEventSourceRepository
{
    private readonly string _connectionString;

    public SqlServerWorkshopPlanningEventSourceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    internal async Task<DateTime> RegisterPlanningAsync(DateTime date)
    {
        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var conn = new SqlConnection(_connectionString);
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

    internal async Task<IReadOnlyList<MaintenanceJobRow>> GetJobsByDateAsync(DateTime date)
    {
        using var conn = new SqlConnection(_connectionString);
        var jobs = await conn.QueryAsync<MaintenanceJobRow>(
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

    internal async Task InsertJobAsync(MaintenanceJobRow job)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            """
            if not exists (select 1 from WorkshopPlanning where PlanningDate = @PlanningDate)
                insert into WorkshopPlanning (PlanningDate) values (@PlanningDate)
            """,
            new { PlanningDate = job.WorkshopPlanningDate });

        await conn.ExecuteAsync(
            """
            insert into MaintenanceJob
                (Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description, WorkshopPlanningDate)
            values
                (@Id, @CustomerId, @VehicleLicenseNumber, @StartTime, @EndTime, @Description, @WorkshopPlanningDate)
            """,
            job);
    }

    internal async Task<MaintenanceJobRow> GetJobAsync(Guid id)
    {
        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<MaintenanceJobRow>(
            """
            select Id, CustomerId, VehicleLicenseNumber, StartTime, EndTime, Description,
                   ActualStartTime, ActualEndTime, Notes, WorkshopPlanningDate
            from MaintenanceJob
            where Id = @Id
            """,
            new { Id = id });
    }

    internal async Task UpdateCompletionAsync(MaintenanceJobRow job)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            """
            update MaintenanceJob
            set ActualStartTime = @ActualStartTime,
                ActualEndTime = @ActualEndTime,
                Notes = @Notes
            where Id = @Id
            """,
            job);
    }
}

internal sealed class MaintenanceJobRow
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; }
    public string VehicleLicenseNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Description { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public string Notes { get; set; }
    public DateTime WorkshopPlanningDate { get; set; }
}
