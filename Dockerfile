FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /app
COPY . /app

RUN dotnet publish --configuration Release --output bin

FROM alpine as final
WORKDIR /app
COPY --from=build /app/bin /app/bin

CMD ["cp",  "/app/bin/jellyfin-ani-sync.dll", "/out/jellyfin-ani-sync.dll"]