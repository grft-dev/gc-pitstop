using Pitstop.WorkshopManagementAPI.Repositories.Model;

namespace Pitstop.WorkshopManagementAPI.Repositories;

public interface ICustomerRefDataRepository
{
    Task<IEnumerable<Customer>> GetCustomersAsync();
    Task<Customer> GetCustomerByIdAsync(string customerId);
}
