namespace WebApp.RESTClients;

public class CustomerManagementAPI : ICustomerManagementAPI
{
    private readonly string _customerConnectionString;

    public CustomerManagementAPI(IConfiguration config)
    {
        _customerConnectionString = config.GetConnectionString("CustomerManagementCN");
    }

    public async Task<List<Customer>> GetCustomers()
    {
        var customers = new List<Customer>();
        const string query = "select CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress from Customer";

        await using var conn = new SqlConnection(_customerConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            customers.Add(MapCustomer(reader));
        }

        return customers;
    }

    public async Task<Customer> GetCustomerById([AliasAs("id")] string customerId)
    {
        const string query = "select CustomerId, Name, Address, PostalCode, City, TelephoneNumber, EmailAddress from Customer where CustomerId = @CustomerId";

        await using var conn = new SqlConnection(_customerConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@CustomerId", customerId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapCustomer(reader);
    }

    public async Task RegisterCustomer(RegisterCustomer command)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("Customer registration was omitted in this migration slice because it depends on unsupported queue-based event publishing.");
    }

    private static Customer MapCustomer(SqlDataReader reader)
    {
        return new Customer
        {
            CustomerId = reader.GetString(0),
            Name = reader.GetString(1),
            Address = reader.GetString(2),
            PostalCode = reader.GetString(3),
            City = reader.GetString(4),
            TelephoneNumber = reader.GetString(5),
            EmailAddress = reader.GetString(6)
        };
    }
}