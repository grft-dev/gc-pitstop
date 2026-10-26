namespace Pitstop.WorkshopManagementAPI.Domain.BusinessRules;

public static class MaintenanceJobRules
{
    public static void PlannedMaintenanceJobShouldFallWithinOneBusinessDay(DateTime start, DateTime end)
    {
        if (end <= start || start.Date != end.Date)
        {
            throw new InvalidOperationException(
                "Start-time and end-time of a Maintenance Job must be within a 1 day.");
        }
    }

    public static void FinishedMaintenanceJobCanNotBeFinished(bool alreadyFinished)
    {
        if (alreadyFinished)
        {
            throw new InvalidOperationException("An already finished job can not be finished.");
        }
    }
}
