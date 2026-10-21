var builder = WebApplication.CreateBuilder(args);

// setup logging
builder.Host.UseSerilog((context, logContext) => 
    logContext
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.WithMachineName()
);

// add DBContext
var sqlConnectionString = builder.Configuration.GetConnectionString("CustomerManagementCN");
builder.Services.AddDbContext<CustomerManagementDBContext>(options => options.UseSqlServer(sqlConnectionString));

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CustomerManagementDBContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// auto migrate db
using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
{
    scope.ServiceProvider.GetService<CustomerManagementDBContext>().MigrateDB();
}

app.UseHealthChecks("/hc");

app.Run();
