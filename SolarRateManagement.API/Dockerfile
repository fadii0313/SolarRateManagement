# Multi-stage Dockerfile for ASP.NET Core 8 Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project definition files for restoring packages
COPY ["../SolarRateManagement.Domain/SolarRateManagement.Domain.csproj", "SolarRateManagement.Domain/"]
COPY ["../SolarRateManagement.Application/SolarRateManagement.Application.csproj", "SolarRateManagement.Application/"]
COPY ["../SolarRateManagement.Infrastructure/SolarRateManagement.Infrastructure.csproj", "SolarRateManagement.Infrastructure/"]
COPY ["SolarRateManagement.API.csproj", "SolarRateManagement.API/"]

# Restore packages
RUN dotnet restore "SolarRateManagement.API/SolarRateManagement.API.csproj"

# Copy full source code and build production output
COPY .. .
WORKDIR "/src/SolarRateManagement.API"
RUN dotnet publish "SolarRateManagement.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "SolarRateManagement.API.dll"]
