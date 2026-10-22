using WorkshopCustomer = Pitstop.WorkshopManagementAPI.Repositories.Model.Customer;
using WorkshopVehicle = Pitstop.WorkshopManagementAPI.Repositories.Model.Vehicle;
using Dapper;
using Microsoft.Data.SqlClient;
using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.Services;

public static class WorkshopRefDataService
{
    // Graftcode-facing method replacing GET /api/refdata/customers.
    // Keep primitive wire types for stable GG client code generation.
    public static string[] GetCustomers() =>
        Run(async () =>
        {
            var customers = await QueryCustomersAsync();
            return customers
                .Select(c => $"{c.CustomerId}|{c.Name}|{c.TelephoneNumber}")
                .ToArray();
        });

    // Graftcode-facing method replacing GET /api/refdata/customers/{customerId}.
    public static string GetCustomerById(string customerId) =>
        Run(async () =>
        {
            var customer = await QueryCustomerByIdAsync(customerId);
            return customer == null
                ? string.Empty
                : $"{customer.CustomerId}|{customer.Name}|{customer.TelephoneNumber}";
        });

    // Graftcode-facing method replacing GET /api/refdata/vehicles.
    public static string[] GetVehicles() =>
        Run(async () =>
        {
            var vehicles = await QueryVehiclesAsync();
            return vehicles
                .Select(v => $"{v.LicenseNumber}|{v.Brand}|{v.Type}|{v.OwnerId}")
                .ToArray();
        });

    // Graftcode-facing method replacing GET /api/refdata/vehicles/{licenseNumber}.
    public static string GetVehicleByLicenseNumber(string licenseNumber) =>
        Run(async () =>
        {
            var vehicle = await QueryVehicleByLicenseNumberAsync(licenseNumber);
            return vehicle == null
                ? string.Empty
                : $"{vehicle.LicenseNumber}|{vehicle.Brand}|{vehicle.Type}|{vehicle.OwnerId}";
        });

    private static T Run<T>(Func<Task<T>> func) =>
        Task.Run(func).ConfigureAwait(false).GetAwaiter().GetResult();

    private static async Task<IEnumerable<WorkshopCustomer>> QueryCustomersAsync()
    {
        using var conn = new SqlConnection(GetRequiredEnvironmentVariable("CUSTOMER_MANAGEMENT_CN"));
        var customers = await conn.QueryAsync<WorkshopCustomer>(
            "select CustomerId, Name, TelephoneNumber from Customer");
        return customers ?? [];
    }

    private static async Task<WorkshopCustomer> QueryCustomerByIdAsync(string customerId)
    {
        using var conn = new SqlConnection(GetRequiredEnvironmentVariable("CUSTOMER_MANAGEMENT_CN"));
        return await conn.QueryFirstOrDefaultAsync<WorkshopCustomer>(
            "select CustomerId, Name, TelephoneNumber from Customer where CustomerId = @CustomerId",
            new { CustomerId = customerId });
    }

    private static async Task<IEnumerable<WorkshopVehicle>> QueryVehiclesAsync()
    {
        var repository = new SqlServerRefDataRepository(GetRequiredEnvironmentVariable("WORKSHOP_MANAGEMENT_CN"));
        return await repository.GetVehiclesAsync();
    }

    private static async Task<WorkshopVehicle> QueryVehicleByLicenseNumberAsync(string licenseNumber)
    {
        var repository = new SqlServerRefDataRepository(GetRequiredEnvironmentVariable("WORKSHOP_MANAGEMENT_CN"));
        return await repository.GetVehicleAsync(licenseNumber);
    }

    private static string GetRequiredEnvironmentVariable(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing {key} environment variable.");
        }

        return value;
    }
}
