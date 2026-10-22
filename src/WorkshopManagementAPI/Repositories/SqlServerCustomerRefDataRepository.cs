using Dapper;
using Microsoft.Data.SqlClient;
using Pitstop.WorkshopManagementAPI.Repositories.Model;

namespace Pitstop.WorkshopManagementAPI.Repositories;

public sealed class SqlServerCustomerRefDataRepository : ICustomerRefDataRepository
{
    private readonly string _connectionString;

    public SqlServerCustomerRefDataRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<Customer>> GetCustomersAsync()
    {
        using var conn = new SqlConnection(_connectionString);
        var customers = await conn.QueryAsync<Customer>(
            "select CustomerId, Name, TelephoneNumber from Customer");
        return customers ?? [];
    }

    public async Task<Customer> GetCustomerByIdAsync(string customerId)
    {
        using var conn = new SqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<Customer>(
            "select CustomerId, Name, TelephoneNumber from Customer where CustomerId = @CustomerId",
            new { CustomerId = customerId });
    }
}
