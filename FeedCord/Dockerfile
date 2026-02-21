FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FeedCord.csproj ./
RUN dotnet restore

COPY src ./src
COPY restart-first-run.sh ./
RUN dotnet publish FeedCord.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN adduser --disabled-password --home /app --gecos "" appuser
COPY --from=build /app/publish ./
COPY --from=build /src/restart-first-run.sh ./
RUN chmod +x /app/restart-first-run.sh && chown -R appuser:appuser /app

USER appuser
ENTRYPOINT ["/app/restart-first-run.sh"]
