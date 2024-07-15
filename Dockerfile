FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app
COPY . /app

RUN dotnet publish --configuration Release --property:PublishDir=bin

FROM alpine as final
WORKDIR /app
COPY --from=build /app/jellyfin-ani-sync/bin /app/bin

CMD ["cp",  "/app/jellyfin-ani-sync/bin/jellyfin-ani-sync.dll", "/out/jellyfin-ani-sync.dll"]