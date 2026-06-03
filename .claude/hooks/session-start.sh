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
# Strategy (in priority order):
#   1. Docker bridge works (local dev / CI) → no marker; Testcontainers handles everything.
#   2. Docker available but constrained (vfs / no-iptables sandbox) → start containers with
#      host networking and write .container-info.json for the test fixtures.
#      NOTE: Docker images are expected to be pre-pulled by the environment setup script.
#   3. Docker containers fail (no cached image, etc.) → fall back to natively-installed
#      PostgreSQL + Redis, write .container-info.json.
#   4. Nothing works → exit 0 (best-effort; session continues without integration tests).
#
# The hook is best-effort: it never fails the session — it always exits 0.

if [ "$CLAUDE_CODE_REMOTE" != "true" ]; then
  exit 0
fi

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
  local pg_port="${1:-$POSTGRES_PORT}"
  local redis_port="${2:-$REDIS_PORT}"
  cat > "$MARKER_FILE" <<EOF
{
  "postgres": "Host=localhost;Port=$pg_port;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD",
  "redis": "localhost:$redis_port"
}
EOF
  log "Wrote container marker to $MARKER_FILE"
}

container_running() {
  docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^$1$"
}

# ── Guard: Docker already fully functional (overlay2 + bridge) → use Testcontainers ──
# We check for overlay2 specifically: our sandbox-compatible dockerd uses vfs (--storage-driver=vfs),
# so vfs indicates a constrained environment we started ourselves. overlay2 means a proper Docker
# installation where Testcontainers bridge networking will work.
if docker info >/dev/null 2>&1 \
    && docker info 2>/dev/null | grep -q "Storage Driver: overlay2" \
    && docker network ls 2>/dev/null | grep -q bridge; then
  log "Docker is fully functional (overlay2 + bridge); Testcontainers will be used. No setup needed."
  # Remove any stale marker so the fixtures don't wrongly enter reuse mode.
  rm -f "$MARKER_FILE"
  exit 0
fi

# ── Start a sandbox-compatible Docker daemon if one isn't running ──
if ! docker info >/dev/null 2>&1; then
  if ! command -v dockerd >/dev/null 2>&1; then
    log "Docker unavailable and dockerd not installed; will attempt native services."
  else
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
      log "Docker daemon failed to start; see /tmp/dockerd.log. Falling back to native services."
    fi
  fi
fi

# ── Attempt Docker containers (host networking) ──
DOCKER_POSTGRES_OK=false
DOCKER_REDIS_OK=false

if docker info >/dev/null 2>&1; then
  # PostgreSQL
  if container_running "$POSTGRES_CONTAINER"; then
    log "PostgreSQL container already running."
    DOCKER_POSTGRES_OK=true
  else
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
          log "PostgreSQL container ready after ${i}s"
          DOCKER_POSTGRES_OK=true
          break
        fi
        sleep 1
      done
      if ! $DOCKER_POSTGRES_OK; then
        log "PostgreSQL container started but did not become ready in time."
      fi
    else
      log "Failed to start PostgreSQL container; will try native fallback."
    fi
  fi

  # Redis
  if container_running "$REDIS_CONTAINER"; then
    log "Redis container already running."
    DOCKER_REDIS_OK=true
  else
    docker rm -f "$REDIS_CONTAINER" >/dev/null 2>&1 || true
    log "Starting Redis on localhost:$REDIS_PORT (host networking)..."
    if docker run -d --rm --network host --name "$REDIS_CONTAINER" \
        "$REDIS_IMAGE" --port "$REDIS_PORT" >/dev/null 2>&1; then
      for i in $(seq 1 15); do
        if docker exec "$REDIS_CONTAINER" redis-cli -p "$REDIS_PORT" ping 2>/dev/null | grep -q PONG; then
          log "Redis container ready after ${i}s"
          DOCKER_REDIS_OK=true
          break
        fi
        sleep 1
      done
      if ! $DOCKER_REDIS_OK; then
        log "Redis container started but did not become ready in time."
      fi
    else
      log "Failed to start Redis container; will try native fallback."
    fi
  fi
fi

if $DOCKER_POSTGRES_OK && $DOCKER_REDIS_OK; then
  write_marker "$POSTGRES_PORT" "$REDIS_PORT"
  log "Docker containers ready. Integration tests will reuse them via $MARKER_FILE."
  exit 0
fi

# ── Fallback: natively-installed PostgreSQL + Redis ──
log "Docker containers unavailable; attempting native PostgreSQL + Redis."

NATIVE_POSTGRES_OK=false
NATIVE_REDIS_OK=false
NATIVE_POSTGRES_PORT=5432

# Native PostgreSQL
if command -v pg_isready >/dev/null 2>&1; then
  if ! pg_isready -h localhost -q 2>/dev/null; then
    if command -v pg_ctlcluster >/dev/null 2>&1; then
      # Identify the first available cluster version and start it.
      PG_VERSION=$(pg_lsclusters -h 2>/dev/null | awk 'NR==1{print $1}')
      NATIVE_POSTGRES_PORT=$(pg_lsclusters -h 2>/dev/null | awk 'NR==1{print $3}')
      if [[ -n "$PG_VERSION" ]]; then
        log "Starting native PostgreSQL cluster $PG_VERSION..."
        pg_ctlcluster "$PG_VERSION" main start >/dev/null 2>&1 || true
      fi
    fi
  else
    NATIVE_POSTGRES_PORT=$(pg_lsclusters -h 2>/dev/null | awk 'NR==1{print $3}')
    [[ -z "$NATIVE_POSTGRES_PORT" ]] && NATIVE_POSTGRES_PORT=5432
  fi

  if pg_isready -h localhost -p "$NATIVE_POSTGRES_PORT" -q 2>/dev/null; then
    log "Native PostgreSQL is running on port $NATIVE_POSTGRES_PORT; creating test user/database..."
    sudo -u postgres psql -p "$NATIVE_POSTGRES_PORT" \
      -c "CREATE USER $POSTGRES_USER WITH PASSWORD '$POSTGRES_PASSWORD';" >/dev/null 2>&1 || true
    sudo -u postgres psql -p "$NATIVE_POSTGRES_PORT" \
      -c "CREATE DATABASE $POSTGRES_DB OWNER $POSTGRES_USER;" >/dev/null 2>&1 || true
    sudo -u postgres psql -p "$NATIVE_POSTGRES_PORT" \
      -c "GRANT ALL PRIVILEGES ON DATABASE $POSTGRES_DB TO $POSTGRES_USER;" >/dev/null 2>&1 || true

    if pg_isready -h localhost -p "$NATIVE_POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -q 2>/dev/null; then
      log "Native PostgreSQL ready (port $NATIVE_POSTGRES_PORT)."
      NATIVE_POSTGRES_OK=true
    else
      log "Native PostgreSQL is running but test database/user setup failed."
    fi
  else
    log "Native PostgreSQL not available or failed to start."
  fi
else
  log "pg_isready not found; native PostgreSQL unavailable."
fi

# Native Redis
if command -v redis-server >/dev/null 2>&1; then
  if redis-cli -p "$REDIS_PORT" ping 2>/dev/null | grep -q PONG; then
    log "Redis already running on port $REDIS_PORT."
    NATIVE_REDIS_OK=true
  else
    log "Starting native Redis on port $REDIS_PORT..."
    redis-server --daemonize yes --port "$REDIS_PORT" --logfile /tmp/redis-test.log >/dev/null 2>&1
    sleep 1
    if redis-cli -p "$REDIS_PORT" ping 2>/dev/null | grep -q PONG; then
      log "Native Redis ready (port $REDIS_PORT)."
      NATIVE_REDIS_OK=true
    else
      log "Native Redis failed to start."
    fi
  fi
else
  log "redis-server not found; native Redis unavailable."
fi

if $NATIVE_POSTGRES_OK && $NATIVE_REDIS_OK; then
  write_marker "$NATIVE_POSTGRES_PORT" "$REDIS_PORT"
  log "Native backing services ready. Integration tests will reuse them via $MARKER_FILE."
  exit 0
fi

log "Could not start backing services (Docker containers and native fallback both unavailable)."
log "Container-backed integration tests will not run this session."
exit 0
