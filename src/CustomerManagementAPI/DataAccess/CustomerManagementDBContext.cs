using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.CustomerManagementAPI.DataAccess;

public class CustomerManagementDBContext
{
    private readonly string _connectionString;

    public CustomerManagementDBContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<Customer>> GetCustomersAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var customers = await conn.QueryAsync<Customer>(
            """
            select CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress
            from Customer
            """);
        return customers ?? [];
    }

    public async Task<Customer> GetCustomerByIdAsync(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<Customer>(
            """
            select CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress
            from Customer
            where CustomerId = @CustomerId
            """,
            new { CustomerId = customerId });
    }

    public async Task RegisterCustomerAsync(
        string customerId,
        string name,
        string address,
        string postalCode,
        string city,
        string telephoneNumber,
        string emailAddress)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            """
            insert into Customer (CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress)
            values (@CustomerId, @Name, @Address, @PostalCode, @City, @TelephoneNumber, @EmailAddress)
            """,
            new
            {
                CustomerId = customerId,
                Name = name,
                Address = address,
                PostalCode = postalCode,
                City = city,
                TelephoneNumber = telephoneNumber,
                EmailAddress = emailAddress
            });
    }
}
