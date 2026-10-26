using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Pitstop.Graftcode;

namespace Pitstop.VehicleManagement.Controllers;

public class VehiclesController
{
    private const string NumberPattern = @"^((\d{1,3}|[a-z]{1,3})-){2}(\d{1,3}|[a-z]{1,3})$";

    public static string RegisterVehicle(string licenseNumber, string brand, string type, string ownerId) =>
        GraftAsync.Run(async () =>
        {
            Require(licenseNumber, nameof(licenseNumber));
            Require(ownerId, nameof(ownerId));
            if (!Regex.IsMatch(licenseNumber, NumberPattern, RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The specified license-number '{licenseNumber}' was not in the correct format.");
            }

            if (!await OwnerExistsAsync(ownerId))
            {
                throw new InvalidOperationException($"Customer '{ownerId}' was not found.");
            }

            await new VehicleManagementDBContext(Required("WORKSHOP_MANAGEMENT_CN"))
                .RegisterVehicleAsync(licenseNumber, brand, type, ownerId);
            return $"{licenseNumber}|{brand}|{type}|{ownerId}";
        });

    private static async Task<bool> OwnerExistsAsync(string ownerId)
    {
        using var conn = new SqlConnection(Required("CUSTOMER_MANAGEMENT_CN"));
        var owner = await conn.QueryFirstOrDefaultAsync<string>(
            "select CustomerId from Customer where CustomerId = @CustomerId",
            new { CustomerId = ownerId });
        return owner != null;
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
