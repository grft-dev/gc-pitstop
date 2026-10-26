using Pitstop.Graftcode;
using Pitstop.WorkshopManagementAPI.Repositories;

namespace Pitstop.WorkshopManagementAPI.Controllers;

public class RefDataController
{
    public static string[] GetCustomers() =>
        GraftAsync.Run(async () =>
        {
            var customers = await Repository().GetCustomersAsync();
            return customers
                .Select(c => $"{c.CustomerId}|{c.Name}|{c.TelephoneNumber}")
                .ToArray();
        });

    public static string GetCustomerById(string customerId) =>
        GraftAsync.Run(async () =>
        {
            var customer = await Repository().GetCustomerAsync(customerId);
            return customer == null
                ? string.Empty
                : $"{customer.CustomerId}|{customer.Name}|{customer.TelephoneNumber}";
        });

    public static string[] GetVehicles() =>
        GraftAsync.Run(async () =>
        {
            var vehicles = await Repository().GetVehiclesAsync();
            return vehicles
                .Select(v => $"{v.LicenseNumber}|{v.Brand}|{v.Type}|{v.OwnerId}")
                .ToArray();
        });

    public static string GetVehicleByLicenseNumber(string licenseNumber) =>
        GraftAsync.Run(async () =>
        {
            var vehicle = await Repository().GetVehicleAsync(licenseNumber);
            return vehicle == null
                ? string.Empty
                : $"{vehicle.LicenseNumber}|{vehicle.Brand}|{vehicle.Type}|{vehicle.OwnerId}";
        });

    private static SqlServerRefDataRepository Repository() =>
        new(Required("CUSTOMER_MANAGEMENT_CN"), Required("WORKSHOP_MANAGEMENT_CN"));

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
