# Multi-stage build for Asnan.Api — see DEPLOYMENT.md for the full
# deploy story (env vars, migration step, compose usage).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Asnan.sln .
COPY src/Asnan.Domain/Asnan.Domain.csproj src/Asnan.Domain/
COPY src/Asnan.Application/Asnan.Application.csproj src/Asnan.Application/
COPY src/Asnan.Infrastructure/Asnan.Infrastructure.csproj src/Asnan.Infrastructure/
COPY src/Asnan.Api/Asnan.Api.csproj src/Asnan.Api/
COPY tests/Asnan.Api.Tests/Asnan.Api.Tests.csproj tests/Asnan.Api.Tests/
RUN dotnet restore src/Asnan.Api/Asnan.Api.csproj

COPY . .
RUN dotnet publish src/Asnan.Api/Asnan.Api.csproj -c Release -o /app --no-restore

# Separate SDK-based stage carrying only the `dotnet ef` tool, used exclusively
# for the explicit migration step (`docker compose run --rm migrator` — see
# DEPLOYMENT.md). Kept out of the runtime image entirely: the API container
# never has the ability to migrate itself, by construction, not just by convention.
FROM build AS migrator
WORKDIR /src/src/Asnan.Api
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH="$PATH:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "/src/src/Asnan.Infrastructure/Asnan.Infrastructure.csproj", "--startup-project", "/src/src/Asnan.Api/Asnan.Api.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Runs as a non-root user — the image ships no secrets and needs no root
# capability; only PhotoStorage:RootPath (mounted as a volume) needs to be
# writable by this user.
RUN useradd --uid 10001 --user-group --no-create-home asnan
COPY --from=build /app .
RUN mkdir -p /app/App_Data/patient-photos && chown -R asnan:asnan /app/App_Data
USER asnan

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Asnan.Api.dll"]
