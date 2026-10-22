using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.CustomerManagementAPI.Services;

public static class CustomerManagementService
{
    // Graftcode-facing method replacing GET /api/customers.
    public static string[] GetCustomers() =>
        Run(async () =>
        {
            var customers = await QueryCustomersAsync();
            return customers.Select(FormatCustomer).ToArray();
        });

    // Graftcode-facing method replacing GET /api/customers/{customerId}.
    public static string GetCustomerById(string customerId) =>
        Run(async () =>
        {
            var customer = await QueryCustomerByIdAsync(customerId);
            return customer == null ? string.Empty : FormatCustomer(customer);
        });

    private static T Run<T>(Func<Task<T>> func) =>
        Task.Run(func).ConfigureAwait(false).GetAwaiter().GetResult();

    private static async Task<IEnumerable<Customer>> QueryCustomersAsync()
    {
        using var conn = new SqlConnection(Required("CUSTOMER_MANAGEMENT_CN"));
        var customers = await conn.QueryAsync<Customer>(
            """
            select CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress
            from Customer
            """);
        return customers ?? [];
    }

    private static async Task<Customer> QueryCustomerByIdAsync(string customerId)
    {
        using var conn = new SqlConnection(Required("CUSTOMER_MANAGEMENT_CN"));
        return await conn.QueryFirstOrDefaultAsync<Customer>(
            """
            select CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress
            from Customer
            where CustomerId = @CustomerId
            """,
            new { CustomerId = customerId });
    }

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
