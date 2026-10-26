using Pitstop.CustomerManagementAPI.DataAccess;
using Pitstop.Graftcode;

namespace Pitstop.Application.CustomerManagementAPI.Controllers;

public class CustomersController
{
    public static string[] GetCustomers() =>
        GraftAsync.Run(async () =>
        {
            var customers = await Context().GetCustomersAsync();
            return customers.Select(FormatCustomer).ToArray();
        });

    public static string GetCustomerById(string customerId) =>
        GraftAsync.Run(async () =>
        {
            var customer = await Context().GetCustomerByIdAsync(customerId);
            return customer == null ? string.Empty : FormatCustomer(customer);
        });

    public static string RegisterCustomer(
        string customerId,
        string name,
        string address,
        string postalCode,
        string city,
        string telephoneNumber,
        string emailAddress) =>
        GraftAsync.Run(async () =>
        {
            Require(customerId, nameof(customerId));
            Require(name, nameof(name));
            await Context().RegisterCustomerAsync(
                customerId, name, address, postalCode, city, telephoneNumber, emailAddress);
            return $"{customerId}|{name}|{telephoneNumber}";
        });

    private static CustomerManagementDBContext Context() =>
        new(Required("CUSTOMER_MANAGEMENT_CN"));

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

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"'{name}' is required.");
        }
    }
}
