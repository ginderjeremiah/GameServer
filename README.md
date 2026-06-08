# GameServer

An idle incremental RPG where players progress through zones, defeat monsters, and unlock new content. The game revolves around a continuous battle mechanic where characters idle in zones and automatically fight enemies while accumulating experience, items, and skill upgrades.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Svelte 5 / SvelteKit |
| Backend | C# ASP.NET Core (.NET 10) |
| Database | PostgreSQL (Entity Framework Core, code-first) |
| Cache / Pub-Sub | Redis |
| Real-time | WebSockets (with a Redis backplane for multi-instance support) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 26+](https://nodejs.org/)
- PostgreSQL and Redis — either installed natively or via Docker (see [Docker option](#option-a-docker-compose-recommended) below)

## Local Development Setup

### 1. Clone the repository

```sh
git clone https://github.com/ginderjeremiah/gameserver.git
cd gameserver
```

### 2. Start PostgreSQL and Redis

#### Option A: Docker Compose (recommended)

Use `docker-compose.local.yml` to spin up PostgreSQL, Redis, **and** the API together via Docker Desktop (bridge networking required — Windows/Mac or Linux with iptables enabled):

```sh
docker compose -f docker-compose.local.yml up -d --build
```

This starts the API on `localhost:5008` alongside its dependencies. Skip to [step 4](#4-install-frontend-dependencies) — no manual backend configuration or `dotnet run` needed.

> **Note:** `docker-compose.local.yml` uses Docker bridge networking and requires Docker Desktop or a standard Linux Docker install. It does **not** work in constrained environments (GitHub Actions, Claude Code remote sessions) — use `docker-compose.yml` (host networking) for those.

#### Option B: Native install

Install PostgreSQL and Redis locally, then configure the API to point at them (step 3).

### 3. Configure the backend (native install only)

The API reads its settings from `appsettings.Development.json` (gitignored) or via [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets). At minimum you need:

```json
{
  "HashPepper": "<any-random-string>",
  "Jwt": {
    "SigningKey": "<at-least-32-character-secret>",
    "Issuer": "game-server-api",
    "Audience": "game-server-api"
  },
  "DataAccessOptions": {
    "DatabaseSystem": 1,
    "CacheSystem": 0,
    "PubSubSystem": 0,
    "DbConnectionString": "Host=localhost;Port=5432;Database=game;Username=<user>;Password=<password>",
    "CacheConnectionString": "localhost:6379",
    "PubSubConnectionString": "localhost:6379"
  }
}
```

> `DatabaseSystem: 1` = PostgreSQL, `CacheSystem: 0` / `PubSubSystem: 0` = Redis.

Then start the API (migrations are applied automatically in the `Development` environment):

```sh
dotnet run --project Game.Api
```

### 4. Install frontend dependencies

```sh
cd UI
npm install
```

### 5. Start the frontend dev server

```sh
cd UI
npm run dev
```

The Vite dev server proxies `/api` and `/socket` to `localhost:5008` (the default API port), so you only need to open the SvelteKit URL (typically `http://localhost:5173`).

## Running Tests

### Backend

Run all backend tests (unit + integration):

```sh
dotnet test Game.sln
```

Integration tests require a running PostgreSQL and Redis instance. In constrained environments (e.g. CI without Docker bridge networking) the session-start hook handles this automatically — see [`docs/backend.md`](docs/backend.md#integration-test-containers-in-constrained-environments).

### Frontend

Unit tests (Vitest):

```sh
cd UI
npm run test:unit
```

End-to-end tests (Playwright) — requires the backend stack running via Docker Compose.

**Docker Desktop (bridge networking):**

```sh
docker compose -f docker-compose.local.yml up -d --build
# apply the e2e seed data once the API is ready:
docker compose -f docker-compose.local.yml exec -T postgres psql -U game -d game -f - < e2e-seed.sql
cd UI
npm run test:e2e
```

**CI / constrained environments (host networking):**

```sh
docker compose up -d --build
# apply the e2e seed data once the API is ready:
docker compose exec -T postgres psql -U game -d game -p 5544 -f - < e2e-seed.sql
cd UI
npm run test:e2e
```

## TypeScript Codegen

The backend auto-generates the frontend's TypeScript API client types from the controller and WebSocket command metadata. To regenerate without standing up the full API stack:

```sh
dotnet run --project Game.Api -- codegen
```

This writes the generated files directly to `UI/src/lib/api/types`. After changing any API DTO or WebSocket command, regenerate and commit the result.

## Docker Compose

Two compose files are provided depending on the environment:

| File | Networking | Use when |
|---|---|---|
| [`docker-compose.local.yml`](docker-compose.local.yml) | Bridge (Docker Desktop) | Local development on Windows/Mac or standard Linux |
| [`docker-compose.yml`](docker-compose.yml) | Host | CI (GitHub Actions) or constrained environments without bridge networking |

Both start the API, PostgreSQL, and Redis (the SvelteKit frontend is started separately by Playwright or `npm run dev`). See [`docs/backend.md`](docs/backend.md#dockerized-api-stack-for-end-to-end-playwright-runs) for configuration details and environment variable overrides.

## Project Structure

```
GameServer/
├── Game.Abstractions/        # Shared interfaces, domain contracts, and repository definitions
├── Game.Api/                 # ASP.NET Core Web API — controllers, WebSocket handlers, codegen
├── Game.Api.Tests/           # API unit and integration tests
├── Game.Application/         # Application layer — orchestration services
├── Game.Application.Tests/   # Application-layer tests
├── Game.Core/                # Core domain logic — battle simulation, game mechanics
├── Game.Core.Tests/          # Domain unit tests
├── Game.DataAccess/          # EF Core repositories and data-access implementations
├── Game.Infrastructure/      # Redis cache/pub-sub, EF DbContext, entity models, migrations
├── Game.TestInfrastructure/  # Shared test fixtures (Testcontainers, integration base classes)
├── UI/                       # SvelteKit frontend
│   ├── src/
│   │   ├── components/       # Shared Svelte components
│   │   ├── lib/              # API client, battle logic, game engine, utilities
│   │   ├── routes/           # SvelteKit pages and route-specific components
│   │   ├── stores/           # Svelte stores for application state
│   │   └── styles/           # Global SCSS and theming
│   └── e2e-tests/            # Playwright end-to-end tests
├── docs/                     # Architecture and design documentation
├── docker-compose.local.yml  # Local dev stack (Docker Desktop, bridge networking)
├── docker-compose.yml        # CI / constrained-environment stack (host networking)
├── e2e-seed.sql              # Minimal seed data for Playwright runs
└── Game.sln                  # .NET solution file
```

## Documentation

Detailed design decisions and guidelines live in [`docs/`](docs/):

- [`docs/backend.md`](docs/backend.md) — backend architecture, DDD layer responsibilities, auth, caching, and admin tooling
- [`docs/frontend.md`](docs/frontend.md) — frontend structure, theming, component conventions, and auth client
- [`docs/frontend-screens.md`](docs/frontend-screens.md) — per-screen design notes
- [`docs/game-design.md`](docs/game-design.md) — game mechanics and design document
