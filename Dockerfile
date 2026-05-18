# syntax=docker/dockerfile:1.7

# ---- Build stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as a separate layer so dependency changes invalidate the cache,
# but source-only edits reuse the restore.
COPY StartupSite.csproj ./
RUN dotnet restore StartupSite.csproj

COPY . ./
RUN dotnet publish StartupSite.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# The aspnet image ships a non-root "app" user (UID 1654); run as it.
USER $APP_UID

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StartupSite.dll"]
