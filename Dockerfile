# Build stage: the SDK image restores and publishes the API.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, so the package layer is reused when only source files change.
COPY src/PaymentGateway.Api/PaymentGateway.Api.csproj src/PaymentGateway.Api/
RUN dotnet restore src/PaymentGateway.Api/PaymentGateway.Api.csproj

COPY src/ src/
RUN dotnet publish src/PaymentGateway.Api/PaymentGateway.Api.csproj -c Release -o /app --no-restore

# Runtime stage: only the ASP.NET runtime and the published output. Listens on port 8080.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
