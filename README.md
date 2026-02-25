# Project Chronos

Client: David Gutman

Lead & Infra: Aaron Iziyaev

Team: Noam Argaman, Adam Rammal, Shalev Kayat and Aaron Iziyaev, or in short - Team NASA.

## Local Development Guide

1. Copy the `.env.example` file to `.local.env` and fill in the required values:

    ```bash
    cp .env.example .local.env
    ```

2. Start all services (PostgreSQL, migrations, and the API):

    ```bash
    docker-compose up --build
    ```

    This will automatically start the database, run EF migrations, and then start the API.

    > **Note:** If you prefer to run migrations manually (e.g., outside of Docker), you can use:
    > ```bash
    > docker-compose up postgres -d
    > dotnet ef database update --project src/Chronos.Data --startup-project src/Chronos.MainApi
    > ```
    > The migration uses `ConnectionStrings__DefaultConnection` env variable if set, otherwise it falls back to the default local connection.
