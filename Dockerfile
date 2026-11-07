# Multi-stage Dockerfile for Ivan (ایوان) - .NET 10 LTS (net10.0)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Create directory for persistent SQLite database and backups
RUN mkdir -p /app/app_data && chown -R $APP_UID /app/app_data

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first for optimal caching
COPY ["Ivan.slnx", "./"]
COPY ["src/Ivan.Domain/Ivan.Domain.csproj", "src/Ivan.Domain/"]
COPY ["src/Ivan.Application/Ivan.Application.csproj", "src/Ivan.Application/"]
COPY ["src/Ivan.Infrastructure/Ivan.Infrastructure.csproj", "src/Ivan.Infrastructure/"]
COPY ["src/Ivan.Web/Ivan.Web.csproj", "src/Ivan.Web/"]

RUN dotnet restore "src/Ivan.Web/Ivan.Web.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/Ivan.Web"
RUN dotnet build "Ivan.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Ivan.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set up healthcheck using the built-in /healthz endpoint
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/healthz || exit 1

VOLUME ["/app/app_data"]
USER $APP_UID
ENTRYPOINT ["dotnet", "Ivan.Web.dll"]
