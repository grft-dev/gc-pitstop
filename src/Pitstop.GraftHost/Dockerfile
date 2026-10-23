FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /usr/src

ARG BUILD_CONFIGURATION=Release

COPY . ./
RUN dotnet restore ./Pitstop.GraftHost/Pitstop.GraftHost.csproj --configfile ./NuGet.Config
RUN dotnet publish ./Pitstop.GraftHost/Pitstop.GraftHost.csproj -c ${BUILD_CONFIGURATION} --no-restore -o /usr/src/publish -p:ErrorOnDuplicatePublishOutputFiles=false
# GG loads the flat Microsoft.Data.SqlClient.dll and ignores the RID graph in deps.json.
# The flat file is a platform stub. Replace it with the highest unix implementation.
RUN impl="$(find /usr/src/publish/runtimes/unix/lib -name Microsoft.Data.SqlClient.dll | sort -V | tail -n 1)" \
 && test -n "$impl" \
 && cp "$impl" /usr/src/publish/Microsoft.Data.SqlClient.dll

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /usr/app

RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates curl tar libcap2-bin libxml2 openssl libxmlsec1 && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build-env /usr/src/publish ./dll/

RUN curl -fsSL -o gg.deb "https://github.com/grft-dev/graftcode-gateway/releases/latest/download/gg_linux_amd64.deb" \
    && dpkg -i gg.deb \
    && rm gg.deb

RUN groupadd -r appuser && useradd -r -g appuser appuser && chown -R appuser:appuser /usr/app
RUN setcap 'cap_net_bind_service=+ep' "$(command -v gg)"

WORKDIR /usr/app/dll
USER appuser

EXPOSE 80
EXPOSE 81

CMD ["gg", "--modules", "Pitstop.WorkshopManagementAPI.dll,Pitstop.CustomerManagementAPI.dll", "--types", "Pitstop.WorkshopManagementAPI.Services.WorkshopRefDataService,Pitstop.WorkshopManagementAPI.Services.WorkshopOperationsService,Pitstop.CustomerManagementAPI.Services.CustomerManagementService", "--methods", "GetCustomers,GetCustomerById,GetVehicles,GetVehicleByLicenseNumber,RegisterCustomer,RegisterVehicle,RegisterPlanning,PlanMaintenanceJob,FinishMaintenanceJob,GetPlanningByDate,GetMaintenanceJob"]
