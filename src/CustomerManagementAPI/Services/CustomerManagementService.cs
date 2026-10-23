using Pitstop.CustomerManagementAPI.Repositories;
using Pitstop.Graftcode;

namespace Pitstop.CustomerManagementAPI.Services;

public static class CustomerManagementService
{
    // Graftcode-facing method replacing GET /api/customers.
    public static string[] GetCustomers() =>
        GraftAsync.Run(async () =>
        {
            var customers = await Repository().GetCustomersAsync();
            return customers.Select(FormatCustomer).ToArray();
        });

    // Graftcode-facing method replacing GET /api/customers/{customerId}.
    public static string GetCustomerById(string customerId) =>
        GraftAsync.Run(async () =>
        {
            var customer = await Repository().GetCustomerByIdAsync(customerId);
            return customer == null ? string.Empty : FormatCustomer(customer);
        });

    private static ICustomerRepository Repository() =>
        new SqlServerCustomerRepository(Required("CUSTOMER_MANAGEMENT_CN"));

    private static string FormatCustomer(Customer customer) =>
        string.Join('|',
            customer.CustomerId ?? string.Empty,
            customer.Name ?? string.Empty,
            customer.Address ?? string.Empty,
            customer.PostalCode ?? string.Empty,
            customer.City ?? string.Empty,
            customer.TelephoneNumber ?? string.Empty,
            customer.EmailAddress ?? string.Empty);

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
