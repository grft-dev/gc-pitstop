namespace Pitstop.CustomerManagementAPI.Repositories;

public interface ICustomerRepository
{
    Task<IEnumerable<Customer>> GetCustomersAsync();
    Task<Customer> GetCustomerByIdAsync(string customerId);
}
