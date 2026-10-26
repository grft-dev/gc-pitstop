namespace Pitstop.WorkshopManagementAPI.Repositories;

using Dapper;
using Microsoft.Data.SqlClient;
using Pitstop.WorkshopManagementAPI.Repositories.Model;
using System.Runtime.ExceptionServices;

public class SqlServerRefDataRepository
{
    // Customer rows live in CustomerManagement. Vehicle rows live in WorkshopManagement.
    // The event-handler projection that used to copy both into one database is not running.
    private readonly string _customerConnectionString;
    private readonly string _workshopConnectionString;

    public SqlServerRefDataRepository(string customerConnectionString, string workshopConnectionString)
    {
        _customerConnectionString = customerConnectionString;
        _workshopConnectionString = workshopConnectionString;
    }

    public async Task<IEnumerable<Customer>> GetCustomersAsync()
    {
        List<Customer> customers = new List<Customer>();
        using (SqlConnection conn = new SqlConnection(_customerConnectionString))
        {
            try
            {
                var customersSelection = await conn.QueryAsync<Customer>(
                    "select CustomerId, Name, TelephoneNumber from Customer");

                if (customersSelection != null)
                {
                    customers.AddRange(customersSelection);
                }
            }
            catch (SqlException ex)
            {
                HandleSqlException(ex);
            }
        }

        return customers;
    }

    public async Task<Customer> GetCustomerAsync(string customerId)
    {
        using (SqlConnection conn = new SqlConnection(_customerConnectionString))
        {
            try
            {
                return await conn.QueryFirstOrDefaultAsync<Customer>(
                    "select CustomerId, Name, TelephoneNumber from Customer where CustomerId = @CustomerId",
                    new { CustomerId = customerId });
            }
            catch (SqlException ex)
            {
                HandleSqlException(ex);
            }

            return null;
        }
    }

    public async Task<IEnumerable<Model.Vehicle>> GetVehiclesAsync()
    {
        List<Vehicle> vehicles = new List<Vehicle>();
        using (SqlConnection conn = new SqlConnection(_workshopConnectionString))
        {
            try
            {
                var vehicleSelection = await conn.QueryAsync<Vehicle>("select * from Vehicle");

                if (vehicleSelection != null)
                {
                    vehicles.AddRange(vehicleSelection);
                }
            }
            catch (SqlException ex)
            {
                HandleSqlException(ex);
            }
        }

        return vehicles;
    }

    public async Task<Vehicle> GetVehicleAsync(string licenseNumber)
    {
        using (SqlConnection conn = new SqlConnection(_workshopConnectionString))
        {
            try
            {
                return await conn.QueryFirstOrDefaultAsync<Vehicle>("select * from Vehicle where LicenseNumber = @LicenseNumber",
                    new { LicenseNumber = licenseNumber });

            }
            catch (SqlException ex)
            {
                HandleSqlException(ex);
            }
            return null;
        }
    }

    private static void HandleSqlException(SqlException ex)
    {
        if (ex.Errors.Count > 0)
        {
            for (int i = 0; i < ex.Errors.Count; i++)
            {
                if (ex.Errors[i].Number == 4060)
                {
                    throw new DatabaseNotCreatedException("WorkshopManagement database not found. This database is automatically created by the WorkshopManagementEventHandler. Run this service first.");
                }
            }
        }

        // rethrow original exception without poluting the stacktrace
        ExceptionDispatchInfo.Capture(ex).Throw();
    }
}
