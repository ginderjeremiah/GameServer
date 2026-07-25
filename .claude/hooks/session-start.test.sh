#!/usr/bin/env bash
#
# Tests for session-start.sh's marker-file lifecycle.
#
# Invariant under test: after the hook runs, .container-info.json exists ONLY if that run actually
# brought backing services up, and its contents always describe THAT run. A marker inherited from a
# previous session must never survive into a session whose services failed — the integration
# fixtures (PreexistingContainerInfo.TryLoad) would skip Testcontainers and point every test at
# dead connection strings, which reads as a test-infra bug rather than a startup failure (#1813).
#
# The hook is driven through its real code paths; only the external services it shells out to
# (docker / postgres / redis) are stubbed, via a minimal PATH containing just this harness's stubs
# plus the handful of coreutils the hook itself calls. Each case also gets a throwaway $HOME so the
# hook's nvm/Node block can't touch the host's real one.
#
# Usage: bash .claude/hooks/session-start.test.sh

set -u

HOOK_SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/session-start.sh"
WORK="$(mktemp -d)"
STUB="$WORK/stubbin"
mkdir -p "$STUB"
trap 'rm -rf "$WORK"' EXIT

# A restricted PATH is what gives the tests control over `command -v docker/dockerd/pg_isready/
# redis-server`: only what is linked here is visible to the hook.
for cmd in bash cat rm seq sleep awk grep dirname; do
  target="$(command -v "$cmd" 2>/dev/null)"
  if [[ -n "$target" ]]; then
    ln -sf "$target" "$STUB/$cmd"
  fi
done

# Deliberately no-op rather than linked to the real binaries: both are privileged/host-mutating
# (`ip link delete docker0`, `sudo -u postgres psql -c "CREATE USER ..."`), so the harness should
# never offer a path to the genuine article even if a future case makes those branches reachable.
for cmd in ip sudo; do
  printf '#!/usr/bin/env bash\nexit 0\n' > "$STUB/$cmd"
  chmod +x "$STUB/$cmd"
done

# Ports the hook is hard-coded to use on the Docker path, and the port the native-fallback case
# feeds it via pg_lsclusters — deliberately not 5432, so a marker built from the default rather
# than the discovered cluster port fails the assertion.
DOCKER_PG_PORT=5499
REDIS_PORT=6399
NATIVE_PG_PORT=5433

STALE_MARKER='{"postgres":"Host=localhost;Port=1111;Database=STALE;Username=stale;Password=stale","redis":"localhost:2222"}'

passed=0
failed=0

# Builds an isolated project root holding a copy of the hook plus a previous session's marker.
setup_case() {
  local root="$WORK/$1"
  rm -rf "$root"
  mkdir -p "$root/.claude/hooks" "$root/home"
  cp "$HOOK_SRC" "$root/.claude/hooks/session-start.sh"
  printf '%s' "$STALE_MARKER" > "$root/.container-info.json"
  echo "$root"
}

# HOME is redirected into the case's own directory: the hook's nvm/Node block symlinks into
# $HOME/.local/bin and is gated on $HOME/.nvm/nvm.sh, neither of which may resolve to the host's.
run_hook() {
  HOME="$1/home" CLAUDE_CODE_REMOTE=true PATH="$STUB" \
    "$STUB/bash" "$1/.claude/hooks/session-start.sh" > "$1/out.log" 2>&1
}

fail_case() {
  echo "  FAIL: $1 — $2"
  sed 's/^/    | /' "$3/out.log" 2>/dev/null
  failed=$((failed + 1))
}

pass_case() {
  echo "  PASS: $1 — $2"
  passed=$((passed + 1))
}

# assert_marker <case name> gone <project root>
# assert_marker <case name> fresh <project root> <expected pg port> <expected redis port>
#
# "fresh" asserts the ports the fixtures will actually dial, not merely that the stale marker was
# replaced — the native path derives its Postgres port dynamically, so the port is the failure mode.
assert_marker() {
  local name="$1" expectation="$2" root="$3" marker="$3/.container-info.json"
  if [[ "$expectation" == "gone" ]]; then
    if [[ ! -e "$marker" ]]; then
      pass_case "$name" "stale marker removed"
    else
      fail_case "$name" "stale marker survived: $(tr -d '\n' < "$marker")" "$root"
    fi
    return
  fi

  local pg_port="$4" redis_port="$5"
  if [[ ! -e "$marker" ]]; then
    fail_case "$name" "expected a fresh marker, found none" "$root"
  elif grep -q "Port=$pg_port" "$marker" && grep -q "localhost:$redis_port" "$marker"; then
    pass_case "$name" "marker describes this run (pg $pg_port, redis $redis_port)"
  else
    fail_case "$name" "marker has wrong contents: $(tr -d '\n' < "$marker")" "$root"
  fi
}

# write_docker_stub <storage driver> <network list> [substring marking a `docker run` that fails]
#
# The storage driver and network list are what the hook inspects to choose a strategy
# (overlay2 + bridge → Testcontainers; anything else → host-networked containers). The third
# argument simulates a partial bring-up: any `docker run` whose command line contains it fails.
# All three are interpolated when the stub is written, so escape anything meant for the stub's
# own runtime.
write_docker_stub() {
  local storage="$1" networks="$2" failing_run="${3:-}"
  cat > "$STUB/docker" <<EOF
#!/usr/bin/env bash
case "\$1" in
  info)    echo "Storage Driver: $storage"; exit 0 ;;
  network) echo "$networks"; exit 0 ;;
  ps)      exit 0 ;;
  rm)      exit 0 ;;
  run)     [[ -n "$failing_run" && "\$*" == *"$failing_run"* ]] && exit 1; exit 0 ;;
  exec)    [[ "\$*" == *redis-cli* ]] && echo PONG; exit 0 ;;
esac
exit 1
EOF
  chmod +x "$STUB/docker"
}

# Stubs the natively-installed services the hook falls back to. Postgres reports itself already
# running on $NATIVE_PG_PORT (advertised only via pg_lsclusters, so the hook has to read it from
# there); Redis starts out down and only answers PONG once `redis-server` has "daemonized", which
# exercises the start path rather than the already-running shortcut.
write_native_stubs() {
  printf '#!/usr/bin/env bash\nexit 0\n' > "$STUB/pg_isready"
  # Columns match `pg_lsclusters -h`: version, cluster, port, status, owner.
  printf '#!/usr/bin/env bash\necho "18 main %s online postgres"\n' "$NATIVE_PG_PORT" > "$STUB/pg_lsclusters"
  printf '#!/usr/bin/env bash\nexit 0\n' > "$STUB/psql"
  # `: >` rather than touch: the stubs run under the same restricted PATH as the hook.
  printf '#!/usr/bin/env bash\n: > "%s/redis-up"\nexit 0\n' "$WORK" > "$STUB/redis-server"
  printf '#!/usr/bin/env bash\n[[ -e "%s/redis-up" ]] && echo PONG\nexit 0\n' "$WORK" > "$STUB/redis-cli"
  chmod +x "$STUB/pg_isready" "$STUB/pg_lsclusters" "$STUB/psql" "$STUB/redis-server" "$STUB/redis-cli"
  rm -f "$WORK/redis-up"
}

clear_native_stubs() {
  rm -f "$STUB/pg_isready" "$STUB/pg_lsclusters" "$STUB/psql" "$STUB/redis-server" "$STUB/redis-cli"
}

echo "=== session-start.sh marker lifecycle ==="

# 1. Terminal failure: nothing available at all. The hook gives up — and must not leave the
#    previous session's marker behind pointing at services that are not running.
rm -f "$STUB/docker"
clear_native_stubs
root="$(setup_case terminal_failure)"
run_hook "$root"
assert_marker "terminal failure (no docker, no native services)" gone "$root"

# 2. Mixed success: Docker brings Postgres up but Redis fails, and the native fallback is also
#    unavailable. Partial bring-up is still a failed session — no usable marker may remain.
write_docker_stub vfs "none host" redis
root="$(setup_case mixed_success)"
run_hook "$root"
assert_marker "mixed success (postgres up, redis down)" gone "$root"

# 3. Full success: both containers up. The marker must be rewritten to describe this run rather
#    than the stale one it replaces.
write_docker_stub vfs "none host"
root="$(setup_case full_success)"
run_hook "$root"
assert_marker "docker success (both services up)" fresh "$root" "$DOCKER_PG_PORT" "$REDIS_PORT"

# 4. Healthy Docker: Testcontainers handles provisioning, so the hook writes no marker of its own
#    and must clear any inherited one before exiting.
write_docker_stub overlay2 bridge
root="$(setup_case testcontainers)"
run_hook "$root"
assert_marker "healthy docker (testcontainers path)" gone "$root"

# 5. Native fallback: no Docker at all, but both services are installed natively. This is the
#    second (and more fragile) marker-writing path — its Postgres port comes from parsing
#    pg_lsclusters, so the marker must carry the discovered port, not the 5432 default.
rm -f "$STUB/docker"
write_native_stubs
root="$(setup_case native_fallback)"
run_hook "$root"
assert_marker "native fallback (discovered cluster port)" fresh "$root" "$NATIVE_PG_PORT" "$REDIS_PORT"
clear_native_stubs

echo "=== $passed passed, $failed failed ==="
[[ $failed -eq 0 ]]
