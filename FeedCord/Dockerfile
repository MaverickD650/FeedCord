# Set the desired platform for the build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["FeedCord.csproj", "./"]
COPY ["src", "./src"]
COPY ["restart-first-run.sh", "./"]

RUN dotnet restore && \
    dotnet build -c Release -o /app/build && \
    dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Copy the restart script into the container
COPY restart-first-run.sh /app

# Make the script executable
RUN chmod +x /app/restart-first-run.sh

# Set the entry point to your application
ENTRYPOINT ["/app/restart-first-run.sh"]
