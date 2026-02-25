# Docker Setup for Local Development

This guide explains how to run the Chronos service with PostgreSQL using Docker Compose for local development.

## Prerequisites

- Docker Desktop installed and running
- Docker Compose (included with Docker Desktop)
- .NET 8 SDK (for running migrations)

## First-Time Setup

When setting up the project for the first time, follow these steps:

### 1. Start PostgreSQL Database

```bash
docker-compose up postgres -d
```

This starts only the PostgreSQL container in detached mode.

### 2. Apply Database Migrations

```bash
dotnet ef database update --project src/Chronos.Data --startup-project src/Chronos.MainApi
```

This creates all the necessary database tables and schema.

### 3. Start All Services

```bash
docker-compose up --build
```

This builds and starts both the API and PostgreSQL containers.

## Quick Start (After First-Time Setup)

1. **Build and start all services:**

    ```bash
    docker-compose up --build
    ```

2. **Access the API:**
    - API: http://localhost:5000
    - Swagger UI: http://localhost:5000/swagger
    - PostgreSQL: localhost:5432

3. **Stop the services:**
    ```bash
    docker-compose down
    ```

## Services

### PostgreSQL Database

- **Image:** postgres:16-alpine
- **Container Name:** chronos-postgres
- **Port:** 5432
- **Database:** chronos
- **Username:** chronos
- **Password:** chronos123
- **Volume:** postgres_data (persists data between restarts)

### Main API

- **Container Name:** chronos-api
- **Port:** 5000 (mapped to container port 8080)
- **Environment:** Development
- **Connection:** Automatically connects to the postgres service

## Configuration

### Database Connection

The API connects to PostgreSQL using the hostname `postgres` (Docker service name). The connection string is configured in the docker-compose.yml:

```yaml
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=chronos;Username=chronos;Password=chronos123
```

### Local Development (without Docker)

If you want to run the API locally (outside Docker) but connect to the Docker PostgreSQL:

1. Start only PostgreSQL:

    ```bash
    docker-compose up postgres
    ```

2. Run the API from your IDE or command line - it will use the connection string from `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=chronos;Username=chronos;Password=chronos123"
     }
   }
   ```

## Useful Commands

### View logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f api
docker-compose logs -f postgres
```

### Rebuild containers

```bash
docker-compose up --build
```

### Stop and remove containers, networks, and volumes

```bash
docker-compose down -v
```

### Access PostgreSQL directly

```bash
docker exec -it chronos-postgres psql -U chronos -d chronos
```

### Reset database (remove volume)

```bash
docker-compose down -v
docker-compose up
```

## Database Migrations

To run Entity Framework migrations with the Docker database:

1. Make sure PostgreSQL is running:

    ```bash
    docker-compose up postgres -d
    ```

2. Run migrations from your host machine:
    ```bash
    dotnet ef database update --project src/Chronos.MainApi
    ```

Or create a new migration:

```bash
dotnet ef migrations add MigrationName --project src/Chronos.Data --startup-project src/Chronos.MainApi
```

## Troubleshooting

### Port conflicts

If port 5000 or 5432 is already in use, modify the ports in `docker-compose.yml`:

```yaml
ports:
    - "5001:8080" # Change host port to 5001
```

### Database connection issues

- Ensure PostgreSQL is healthy: `docker-compose ps`
- Check logs: `docker-compose logs postgres`
- Verify the healthcheck: The API waits for PostgreSQL to be ready before starting

### Rebuild after code changes

```bash
docker-compose up --build api
```

### Clean slate

```bash
docker-compose down -v
docker system prune -a
docker-compose up --build
```

## Network Architecture

The services communicate through a Docker bridge network (`chronos-network`):

- The API service references the PostgreSQL service by hostname `postgres`
- Both services are isolated from other Docker networks
- Ports are exposed to the host machine for development access

## Security Notes

⚠️ **Important:** The credentials in this setup are for local development only. **Never use these in production!**

For production:

- Use strong, unique passwords
- Store credentials in secure secret management systems
- Use environment-specific configurations
- Enable SSL/TLS for database connections
