FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Chronos.MainApi/Chronos.MainApi.csproj", "src/Chronos.MainApi/"]
COPY ["src/Chronos.Agent/Chronos.Agent.csproj", "src/Chronos.Agent/"]
COPY ["src/Chronos.Data/Chronos.Data.csproj", "src/Chronos.Data/"]
COPY ["src/Chronos.Domain/Chronos.Domain.csproj", "src/Chronos.Domain/"]
COPY ["src/Chronos.Shared/Chronos.Shared.csproj", "src/Chronos.Shared/"]
RUN dotnet restore "src/Chronos.MainApi/Chronos.MainApi.csproj"
COPY . .
WORKDIR "/src/src/Chronos.MainApi"
RUN dotnet build "./Chronos.MainApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Chronos.MainApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Chronos.MainApi.dll"]
