
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["PerfectRide.csproj", "./"]
RUN dotnet restore "PerfectRide.csproj"

# Copy the remaining source files
COPY . .

# Publish the application
RUN dotnet publish "PerfectRide.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN mkdir -p /app/Data

COPY --from=build /app/publish .

# Render uses port 10000 by default.
EXPOSE 10000

ENTRYPOINT ["sh", "-c", "dotnet PerfectRide.dll --urls http://0.0.0.0:${PORT:-10000}"]
