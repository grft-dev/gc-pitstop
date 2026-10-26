using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.VehicleManagement.DataAccess;

// Ref data reads WorkshopManagement.Vehicle. The event-handler projection is not on the graft,
// so this context writes that table through WORKSHOP_MANAGEMENT_CN.
public class VehicleManagementDBContext
{
    private readonly string _connectionString;

    public VehicleManagementDBContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task RegisterVehicleAsync(string licenseNumber, string brand, string type, string ownerId)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            """
            insert into Vehicle (LicenseNumber, Brand, [Type], OwnerId)
            values (@LicenseNumber, @Brand, @Type, @OwnerId)
            """,
            new { LicenseNumber = licenseNumber, Brand = brand, Type = type, OwnerId = ownerId });
    }
}
