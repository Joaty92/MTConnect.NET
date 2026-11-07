# SDK 10 is required even though the published output targets net8.0: the
# libraries multi-target up to net10.0 in Release, and the SDK evaluates every
# referenced project's full TargetFrameworks list during restore regardless of
# the -f passed to publish. SDK 9 fails with NETSDK1045 on the net10.0 entries.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish examples/MTConnect.NET-Agent-Embedded/MTConnect.NET-Agent-Embedded.csproj \
    -c Release -f net8.0 -r linux-x64 --self-contained false \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime
# apk upgrade: take Alpine's latest security fixes (e.g. zlib, musl) at build time instead of
# waiting for Microsoft to refresh the base image. Stays within the base image's Alpine release.
RUN apk upgrade --no-cache && apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
# Run as the base image's non-root "app" user (UID 1654), not root. The program files stay
# root-owned; the agent writes agent.information.json and buffer/ in /app, plus logs/ and state/.
# Bind-mounted logs/ and state/ must be writable by UID 1654 (on a Linux host:
#   sudo chown -R 1654:1654 logs state).
RUN mkdir -p /app/buffer /app/logs /app/state \
    && chown $APP_UID:$APP_UID /app /app/buffer /app/logs /app/state
# Upstream's health check: requests /probe from the http-server module (port 5000 unless
# MTCONNECT_HEALTHCHECK_PORT is set), so "docker ps" shows unhealthy if the agent stops answering.
COPY --from=build --chmod=755 /src/build/MTConnect.NET.Builder/Parts/agent/docker/docker-healthcheck.sh /usr/local/bin/
USER $APP_UID
EXPOSE 5000
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 CMD ["/usr/local/bin/docker-healthcheck.sh"]
ENTRYPOINT ["dotnet", "MTConnect.NET-Agent-Embedded.dll"]
