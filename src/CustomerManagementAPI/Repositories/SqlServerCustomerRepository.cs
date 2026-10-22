using Dapper;
using Microsoft.Data.SqlClient;

namespace Pitstop.CustomerManagementAPI.Repositories;

public sealed class SqlServerCustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;

    public SqlServerCustomerRepository(string connectionString)
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
}
