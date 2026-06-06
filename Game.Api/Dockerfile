# Multi-stage build for the Game.Api service.
# The build context is expected to be the repository root (see docker-compose.yml) so that the
# referenced projects (Game.Abstractions, Game.Core, Game.DataAccess, Game.Application,
# Game.Infrastructure) are available for restore/publish.
# See https://aka.ms/customizecontainer for how Visual Studio uses this Dockerfile.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore from the project manifests *before* copying the rest of the source. NuGet restore is the
# slow part of the build, and its inputs are only the .csproj files — so isolating it in its own
# layer lets Docker (and the CI buildx GHA cache) reuse the restored packages on every build where
# the dependency graph is unchanged, even when source files change. The set below is Game.Api's full
# restore graph (it references Abstractions/Core/DataAccess/Application; DataAccess pulls in
# Infrastructure). Add a line here if Game.Api gains a new project reference.
COPY Game.Abstractions/Game.Abstractions.csproj Game.Abstractions/
COPY Game.Core/Game.Core.csproj Game.Core/
COPY Game.Infrastructure/Game.Infrastructure.csproj Game.Infrastructure/
COPY Game.DataAccess/Game.DataAccess.csproj Game.DataAccess/
COPY Game.Application/Game.Application.csproj Game.Application/
COPY Game.Api/Game.Api.csproj Game.Api/
RUN dotnet restore "Game.Api/Game.Api.csproj"

COPY . .
RUN dotnet publish "Game.Api/Game.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Game.Api.dll"]
