# Realm Weaver API. Build from the repo root:
#   docker build -f src/backend/RealmWeaver.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore first, from the project files only, so the layer is cached until a dependency changes.
# .editorconfig carries analyzer settings (generated migrations are exempt), and warnings are errors.
COPY global.json Directory.Build.props .editorconfig ./
COPY src/backend/RealmWeaver.Api/RealmWeaver.Api.csproj                         src/backend/RealmWeaver.Api/
COPY src/backend/RealmWeaver.Generation/RealmWeaver.Generation.csproj           src/backend/RealmWeaver.Generation/
COPY src/backend/RealmWeaver.Infrastructure/RealmWeaver.Infrastructure.csproj   src/backend/RealmWeaver.Infrastructure/
COPY src/backend/RealmWeaver.Modules.Catalog/RealmWeaver.Modules.Catalog.csproj       src/backend/RealmWeaver.Modules.Catalog/
COPY src/backend/RealmWeaver.Modules.Inventory/RealmWeaver.Modules.Inventory.csproj   src/backend/RealmWeaver.Modules.Inventory/
COPY src/backend/RealmWeaver.Modules.Realms/RealmWeaver.Modules.Realms.csproj         src/backend/RealmWeaver.Modules.Realms/
COPY src/backend/RealmWeaver.Modules.Rituals/RealmWeaver.Modules.Rituals.csproj       src/backend/RealmWeaver.Modules.Rituals/
RUN dotnet restore src/backend/RealmWeaver.Api/RealmWeaver.Api.csproj

COPY src/backend/ src/backend/
RUN dotnet publish src/backend/RealmWeaver.Api/RealmWeaver.Api.csproj --no-restore -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# The base image runs as the non-root "app" user and listens on 8080.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "RealmWeaver.Api.dll"]
