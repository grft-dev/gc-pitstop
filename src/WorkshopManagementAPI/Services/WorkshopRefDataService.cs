using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.Services;

public static class WorkshopRefDataService
{
    // Graftcode-facing method replacing GET /api/refdata/customers.
    // Keep primitive wire types for stable GG client code generation.
    public static string[] GetCustomers() =>
        Run(async () =>
        {
            var customers = await CustomerRepository().GetCustomersAsync();
            return customers
                .Select(c => $"{c.CustomerId}|{c.Name}|{c.TelephoneNumber}")
                .ToArray();
        });

    // Graftcode-facing method replacing GET /api/refdata/customers/{customerId}.
    public static string GetCustomerById(string customerId) =>
        Run(async () =>
        {
            var customer = await CustomerRepository().GetCustomerByIdAsync(customerId);
            return customer == null
                ? string.Empty
                : $"{customer.CustomerId}|{customer.Name}|{customer.TelephoneNumber}";
        });

    // Graftcode-facing method replacing GET /api/refdata/vehicles.
    public static string[] GetVehicles() =>
        Run(async () =>
        {
            var vehicles = await VehicleRepository().GetVehiclesAsync();
            return vehicles
                .Select(v => $"{v.LicenseNumber}|{v.Brand}|{v.Type}|{v.OwnerId}")
                .ToArray();
        });

    // Graftcode-facing method replacing GET /api/refdata/vehicles/{licenseNumber}.
    public static string GetVehicleByLicenseNumber(string licenseNumber) =>
        Run(async () =>
        {
            var vehicle = await VehicleRepository().GetVehicleAsync(licenseNumber);
            return vehicle == null
                ? string.Empty
                : $"{vehicle.LicenseNumber}|{vehicle.Brand}|{vehicle.Type}|{vehicle.OwnerId}";
        });

    private static T Run<T>(Func<Task<T>> func) =>
        Task.Run(func).ConfigureAwait(false).GetAwaiter().GetResult();

    private static ICustomerRefDataRepository CustomerRepository() =>
        new SqlServerCustomerRefDataRepository(GetRequiredEnvironmentVariable("CUSTOMER_MANAGEMENT_CN"));

    private static IVehicleRepository VehicleRepository() =>
        new SqlServerRefDataRepository(GetRequiredEnvironmentVariable("WORKSHOP_MANAGEMENT_CN"));

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
