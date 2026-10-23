namespace WebApp.RESTClients;

public class WorkshopManagementAPI : IWorkshopManagementAPI
{
    private IWorkshopManagementAPI _restClient;
    private readonly ICustomerManagementAPI _customerManagementAPI;
    private readonly string _workshopConnectionString;

    public WorkshopManagementAPI(IConfiguration config, HttpClient httpClient, ICustomerManagementAPI customerManagementAPI)
    {
        string apiHostAndPort = config.GetSection("APIServiceLocations").GetValue<string>("WorkshopManagementAPI");
        httpClient.BaseAddress = new Uri($"http://{apiHostAndPort}/api");
        _customerManagementAPI = customerManagementAPI;
        _workshopConnectionString = config.GetConnectionString("WorkshopManagementCN");
        _restClient = RestService.For<IWorkshopManagementAPI>(
            httpClient,
            new RefitSettings
            {
                ContentSerializer = new NewtonsoftJsonContentSerializer()
            });
    }

    public async Task<WorkshopPlanning> GetWorkshopPlanning(string planningDate)
    {
        try
        {
            return await _restClient.GetWorkshopPlanning(planningDate);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                throw;
            }
        }
    }

    public async Task<MaintenanceJob> GetMaintenanceJob(string planningDate, string jobId)
    {
        try
        {
            return await _restClient.GetMaintenanceJob(planningDate, jobId);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                throw;
            }
        }
    }

    public async Task RegisterPlanning(string planningDate, RegisterPlanning cmd)
    {
        await _restClient.RegisterPlanning(planningDate, cmd);
    }

    public async Task PlanMaintenanceJob(string planningDate, PlanMaintenanceJob cmd)
    {
        await _restClient.PlanMaintenanceJob(planningDate, cmd);
    }

    public async Task FinishMaintenanceJob(string planningDate, string jobId, FinishMaintenanceJob cmd)
    {
        await _restClient.FinishMaintenanceJob(planningDate, jobId, cmd);
    }

    public async Task<List<Customer>> GetCustomers()
    {
        return await _customerManagementAPI.GetCustomers();
    }

    public async Task<Customer> GetCustomerById(string id)
    {
        return await _customerManagementAPI.GetCustomerById(id);
    }

    public async Task<List<Vehicle>> GetVehicles()
    {
        var vehicles = new List<Vehicle>();
        const string query = "select LicenseNumber, Brand, Type, OwnerId from Vehicle";

        await using var conn = new SqlConnection(_workshopConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vehicles.Add(MapVehicle(reader));
        }

        return vehicles;
    }

    public async Task<Vehicle> GetVehicleByLicenseNumber([AliasAs("id")] string licenseNumber)
    {
        const string query = "select LicenseNumber, Brand, Type, OwnerId from Vehicle where LicenseNumber = @LicenseNumber";

        await using var conn = new SqlConnection(_workshopConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@LicenseNumber", licenseNumber);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapVehicle(reader);
    }

    private static Vehicle MapVehicle(SqlDataReader reader)
    {
        return new Vehicle
        {
            LicenseNumber = reader.GetString(0),
            Brand = reader.GetString(1),
            Type = reader.GetString(2),
            OwnerId = reader.IsDBNull(3) ? null : reader.GetString(3)
        };
    }
}
