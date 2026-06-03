#!/usr/bin/env bash
#
# Claude Code SessionStart hook — backing services for integration tests.
#
# The integration test suites (Game.Api.Tests, Game.Application.Tests) normally use
# Testcontainers to spin up PostgreSQL + Redis. Testcontainers relies on Docker bridge
# networking, which is unavailable in constrained sandboxes such as the Claude Code web
# environment (old kernel, no iptables, no bridge). See:
#   https://github.com/anthropics/claude-code/issues/29515
#
# In those environments this hook starts a Docker daemon with sandbox-compatible flags,
# launches PostgreSQL + Redis with host networking on fixed ports, and writes a
# `.container-info.json` marker at the repo root. The test fixtures detect that marker
# (PreexistingContainerInfo.TryLoad) and reuse these services instead of Testcontainers.
#
# In normal environments (local dev, CI) Docker bridge networking works, so this hook is a
# no-op and Testcontainers handles everything. The hook is best-effort: it never fails the
# session — it always exits 0.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MARKER_FILE="$PROJECT_ROOT/.container-info.json"

# Backing-service configuration. Images match the Testcontainers fixtures so the reuse path
# behaves identically to the normal path.
POSTGRES_IMAGE="postgres:18-alpine"
POSTGRES_CONTAINER="gameserver-test-postgres"
POSTGRES_PORT=5499
POSTGRES_USER="test"
POSTGRES_PASSWORD="test"
POSTGRES_DB="game_test"

REDIS_IMAGE="redis:7-alpine"
REDIS_CONTAINER="gameserver-test-redis"
REDIS_PORT=6399

log() { echo "[session-start] $*"; }

write_marker() {
  cat > "$MARKER_FILE" <<EOF
{
  "postgres": "Host=localhost;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD",
  "redis": "localhost:$REDIS_PORT"
}
EOF
  log "Wrote container marker to $MARKER_FILE"
}

container_running() {
  docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^$1$"
}

# ── Guard: Docker already fully functional (bridge networking) → use Testcontainers ──
if docker info >/dev/null 2>&1 && docker network ls 2>/dev/null | grep -q bridge; then
  log "Docker is fully functional; Testcontainers will be used. No setup needed."
  # Remove any stale marker so the fixtures don't wrongly enter reuse mode.
  rm -f "$MARKER_FILE"
  exit 0
fi

# ── Start a sandbox-compatible Docker daemon if one isn't running ──
if ! docker info >/dev/null 2>&1; then
  if ! command -v dockerd >/dev/null 2>&1; then
    log "Docker unavailable and dockerd not installed; skipping. Container-backed integration tests will not run."
    exit 0
  fi

  log "Starting dockerd (vfs storage driver, no iptables) for constrained environment..."
  # Remove a stale docker0 bridge left by a previous daemon, which otherwise blocks startup.
  ip link delete docker0 >/dev/null 2>&1 || true
  # --storage-driver=vfs : overlay2 fails on old kernels; vfs is slower but always works.
  # --iptables / --ip6tables=false : the sandbox kernel can't manage iptables in containers.
  dockerd --iptables=false --ip6tables=false --storage-driver=vfs >/tmp/dockerd.log 2>&1 &

  for i in $(seq 1 30); do
    if docker info >/dev/null 2>&1; then
      log "Docker daemon ready after ${i}s"
      break
    fi
    sleep 1
  done

  if ! docker info >/dev/null 2>&1; then
    log "Docker daemon failed to start; see /tmp/dockerd.log. Skipping."
    exit 0
  fi
fi

# ── PostgreSQL ──
if container_running "$POSTGRES_CONTAINER"; then
  log "PostgreSQL container already running."
else
  log "Pulling $POSTGRES_IMAGE (first pull is slow with the vfs storage driver)..."
  docker pull "$POSTGRES_IMAGE" >/dev/null 2>&1 || log "Could not pull $POSTGRES_IMAGE; using cached image if present."

  docker rm -f "$POSTGRES_CONTAINER" >/dev/null 2>&1 || true
  log "Starting PostgreSQL on localhost:$POSTGRES_PORT (host networking)..."
  # Host networking bypasses the bridge/iptables requirement. The trailing "-p PORT" is passed
  # to the postgres server so it listens on our fixed port instead of the default 5432.
  if docker run -d --rm --network host --name "$POSTGRES_CONTAINER" \
      -e POSTGRES_USER="$POSTGRES_USER" \
      -e POSTGRES_PASSWORD="$POSTGRES_PASSWORD" \
      -e POSTGRES_DB="$POSTGRES_DB" \
      "$POSTGRES_IMAGE" -p "$POSTGRES_PORT" >/dev/null 2>&1; then
    for i in $(seq 1 30); do
      if docker exec "$POSTGRES_CONTAINER" pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" -p "$POSTGRES_PORT" >/dev/null 2>&1; then
        log "PostgreSQL ready after ${i}s"
        break
      fi
      sleep 1
    done
  else
    log "Failed to start PostgreSQL container; skipping."
    exit 0
  fi
fi

# ── Redis ──
if container_running "$REDIS_CONTAINER"; then
  log "Redis container already running."
else
  log "Pulling $REDIS_IMAGE..."
  docker pull "$REDIS_IMAGE" >/dev/null 2>&1 || log "Could not pull $REDIS_IMAGE; using cached image if present."

  docker rm -f "$REDIS_CONTAINER" >/dev/null 2>&1 || true
  log "Starting Redis on localhost:$REDIS_PORT (host networking)..."
  if docker run -d --rm --network host --name "$REDIS_CONTAINER" \
      "$REDIS_IMAGE" --port "$REDIS_PORT" >/dev/null 2>&1; then
    for i in $(seq 1 15); do
      if docker exec "$REDIS_CONTAINER" redis-cli -p "$REDIS_PORT" ping 2>/dev/null | grep -q PONG; then
        log "Redis ready after ${i}s"
        break
      fi
      sleep 1
    done
  else
    log "Failed to start Redis container; skipping."
    exit 0
  fi
fi

write_marker
log "Backing services ready. Integration tests will reuse them via $MARKER_FILE."
exit 0
