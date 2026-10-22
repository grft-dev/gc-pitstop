namespace Pitstop.CustomerManagementAPI.Services;

public class CustomerManagementService
{
    private readonly CustomerManagementDBContext _dbContext;

    public CustomerManagementService()
        : this(CreateDbContextFromEnvironment())
    {
    }

    public CustomerManagementService(CustomerManagementDBContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Graftcode-facing method replacing GET /api/customers.
    public List<Customer> GetCustomersAsync()
    {
        return GetCustomersCoreAsync().GetAwaiter().GetResult();
    }

    // Graftcode-facing method replacing GET /api/customers/{customerId}.
    public Customer GetCustomerByIdAsync(string customerId)
    {
        return GetCustomerByIdCoreAsync(customerId).GetAwaiter().GetResult();
    }

    private async Task<List<Customer>> GetCustomersCoreAsync() =>
        await _dbContext.Customers.ToListAsync();

    private async Task<Customer> GetCustomerByIdCoreAsync(string customerId) =>
        await _dbContext.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);

    private static CustomerManagementDBContext CreateDbContextFromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("CUSTOMER_MANAGEMENT_CN");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing CUSTOMER_MANAGEMENT_CN environment variable.");
        }

        var options = new DbContextOptionsBuilder<CustomerManagementDBContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new CustomerManagementDBContext(options);
    }
}
