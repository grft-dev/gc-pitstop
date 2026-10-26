namespace Pitstop.WorkshopManagementAPI.Domain.BusinessRules;

public static class WorkshopPlanningRules
{
    /// <summary>
    /// The maximum number of parallel jobs in the workshop (restricted by the available workstations).
    /// </summary>
    private const int AvailableWorkstations = 3;

    public static void NumberOfParallelMaintenanceJobsMustNotExceedAvailableWorkStations(
        IEnumerable<(DateTime Start, DateTime End)> existingJobs,
        DateTime start,
        DateTime end)
    {
        var overlaps = existingJobs.Count(job => start < job.End && end > job.Start);
        if (overlaps >= AvailableWorkstations)
        {
            throw new InvalidOperationException(
                $"Maintenancejob overlaps with more than {AvailableWorkstations} other jobs.");
        }
    }

    public static void NumberOfParallelMaintenanceJobsOnAVehicleMustNotExceedOne(
        IEnumerable<(string LicenseNumber, DateTime Start, DateTime End)> existingJobs,
        string licenseNumber,
        DateTime start,
        DateTime end)
    {
        var vehicleAlreadyBooked = existingJobs.Any(job =>
            string.Equals(job.LicenseNumber, licenseNumber, StringComparison.OrdinalIgnoreCase) &&
            start < job.End && end > job.Start);
        if (vehicleAlreadyBooked)
        {
            throw new InvalidOperationException(
                "Only 1 maintenance job can be executed on a vehicle during a certain time-slot.");
        }
    }
}
