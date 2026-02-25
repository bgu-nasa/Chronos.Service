FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

# Copy project files and restore
COPY ["src/Chronos.Data/Chronos.Data.csproj", "src/Chronos.Data/"]
COPY ["src/Chronos.Domain/Chronos.Domain.csproj", "src/Chronos.Domain/"]
COPY ["src/Chronos.Shared/Chronos.Shared.csproj", "src/Chronos.Shared/"]
COPY ["src/Chronos.MainApi/Chronos.MainApi.csproj", "src/Chronos.MainApi/"]
RUN dotnet restore "src/Chronos.MainApi/Chronos.MainApi.csproj"

# Copy everything
COPY . .

ENTRYPOINT ["sh", "-c", "dotnet ef database update --project src/Chronos.Data --startup-project src/Chronos.MainApi --connection \"$ConnectionStrings__DefaultConnection\""]
