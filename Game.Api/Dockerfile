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
COPY . .
RUN dotnet restore "Game.Api/Game.Api.csproj"
RUN dotnet publish "Game.Api/Game.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Game.Api.dll"]
