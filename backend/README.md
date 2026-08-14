# Asnan Backend

ASP.NET Core Web API. See `/ARCHITECTURE.md` at the repo root for the full design.

For the containerized stack (Docker Compose: api/mysql/redis/nginx with local HTTPS), every
required environment variable, and the CD pipeline, see `/DEPLOYMENT.md` at the repo root instead
of the raw local-dev workflow below.

## Local development

Requires a MySQL-protocol-compatible database (MySQL 8 or MariaDB 10.6+).

```bash
docker run -d --name asnan-mysql-dev \
  -e MYSQL_ROOT_PASSWORD=root_dev_only \
  -e MYSQL_DATABASE=asnan_dev \
  -e MYSQL_USER=asnan \
  -e MYSQL_PASSWORD=asnan_dev_only_password \
  -p 3306:3306 mysql:8.0
```

Update `ConnectionStrings:Default` in `src/Asnan.Api/appsettings.Development.json` (or override via the
`ConnectionStrings__Default` environment variable) to match your container's host/port.

## Migrations

Migrations are never applied automatically on application startup — this is a deliberate choice
(see ARCHITECTURE.md §14) so schema changes are always an explicit, reviewable deploy step.

```bash
dotnet tool install --global dotnet-ef   # once

dotnet ef database update \
  --project src/Asnan.Infrastructure \
  --startup-project src/Asnan.Api
```

To add a new migration after changing entities/configurations:

```bash
dotnet ef migrations add <Name> \
  --project src/Asnan.Infrastructure \
  --startup-project src/Asnan.Api \
  --output-dir Persistence/Migrations
```

## Running

```bash
dotnet run --project src/Asnan.Api
```

Swagger UI is available at `/swagger` in the Development environment.

## Tests

```bash
dotnet test
```

Some tests (`DbConstraintTests`) run against a real database (never in-memory — the point is
verifying the database's own constraints) and apply migrations themselves on startup via
`Database.MigrateAsync()`. CI provisions a MySQL service container for this; locally, point
`ConnectionStrings__Default` at your dev database before running `dotnet test`.
