# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files needed for restore
COPY global.json Directory.Build.props ./

# Copy project files — changes rarely, so restore is cached as a separate layer
COPY src/ServiceDeskLite.Domain/ServiceDeskLite.Domain.csproj                               src/ServiceDeskLite.Domain/
COPY src/ServiceDeskLite.Contracts/ServiceDeskLite.Contracts.csproj                         src/ServiceDeskLite.Contracts/
COPY src/ServiceDeskLite.Application/ServiceDeskLite.Application.csproj                     src/ServiceDeskLite.Application/
COPY src/ServiceDeskLite.Infrastructure/ServiceDeskLite.Infrastructure.csproj               src/ServiceDeskLite.Infrastructure/
COPY src/ServiceDeskLite.Infrastructure.InMemory/ServiceDeskLite.Infrastructure.InMemory.csproj src/ServiceDeskLite.Infrastructure.InMemory/
COPY src/ServiceDeskLite.Api/ServiceDeskLite.Api.csproj                                     src/ServiceDeskLite.Api/

# Copy lock files — required because RestorePackagesWithLockFile=true in Directory.Build.props
COPY src/ServiceDeskLite.Domain/packages.lock.json                               src/ServiceDeskLite.Domain/
COPY src/ServiceDeskLite.Contracts/packages.lock.json                            src/ServiceDeskLite.Contracts/
COPY src/ServiceDeskLite.Application/packages.lock.json                          src/ServiceDeskLite.Application/
COPY src/ServiceDeskLite.Infrastructure/packages.lock.json                       src/ServiceDeskLite.Infrastructure/
COPY src/ServiceDeskLite.Infrastructure.InMemory/packages.lock.json              src/ServiceDeskLite.Infrastructure.InMemory/
COPY src/ServiceDeskLite.Api/packages.lock.json                                  src/ServiceDeskLite.Api/

# Restore dependencies in locked mode (no lock file updates, reproducible)
RUN dotnet restore src/ServiceDeskLite.Api/ServiceDeskLite.Api.csproj --locked-mode

# Copy all source files and publish
COPY src/ src/
RUN dotnet publish src/ServiceDeskLite.Api/ServiceDeskLite.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Stage 2: Runtime — only the ASP.NET Core runtime, no SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Port 8080 is the default HTTP port for .NET containers (no HTTPS in containerised dev)
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "ServiceDeskLite.Api.dll"]
