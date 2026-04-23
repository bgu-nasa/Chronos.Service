FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Chronos.Offboarding/Chronos.Offboarding.csproj", "src/Chronos.Offboarding/"]
COPY ["src/Chronos.Data/Chronos.Data.csproj", "src/Chronos.Data/"]
COPY ["src/Chronos.Domain/Chronos.Domain.csproj", "src/Chronos.Domain/"]
RUN dotnet restore "src/Chronos.Offboarding/Chronos.Offboarding.csproj"
COPY . .
WORKDIR "/src/src/Chronos.Offboarding"
RUN dotnet build "./Chronos.Offboarding.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Chronos.Offboarding.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Chronos.Offboarding.dll"]
