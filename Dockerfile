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
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "MTConnect.NET-Agent-Embedded.dll"]
