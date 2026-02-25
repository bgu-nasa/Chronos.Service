# Project Chronos

Client: David Gutman

Lead & Infra: Aaron Iziyaev

Team: Noam Argaman, Adam Rammal, Shalev Kayat and Aaron Iziyaev, or in short - Team NASA.

## Local Development Guide

1. Copy the `.env.example` file to `.local.env` and fill in the required values:

    ```bash
    cp .env.example .local.env
    ```

2. Start PostgreSQL Database

    ```bash
    docker-compose up postgres -d
    ```

3. Run the migrations to create the database schema:

    ```bash
    dotnet ef database update --project src/Chronos.Data --startup-project src/Chronos.MainApi
    ```

    > **Note:** The migration uses `EF_CONNECTION_STRING` env variable if set, otherwise it falls back to the default local connection (`Host=localhost;Port=5432;Database=chronos;Username=chronos;Password=<FILL-IT>`). If your database uses different credentials, set the env variable before running:
    > ```bash
    > export EF_CONNECTION_STRING="Host=localhost;Port=5432;Database=chronos;Username=chronos;Password=<FILL-IT>"
    > ```

4. Start the API service

    ```bash
    docker-compose up --build
    ```
