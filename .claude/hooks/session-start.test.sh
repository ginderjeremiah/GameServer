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

# `ip link delete docker0` is host-mutating and nothing here needs its effect, so it is a hard no-op
# rather than a link to the real binary.
printf '#!/usr/bin/env bash\nexit 0\n' > "$STUB/ip"

# `sudo` drops the `-u <user>` and runs the rest, so `sudo -u postgres psql -c "CREATE USER ..."`
# actually reaches the psql stub instead of being swallowed. Safe because the hook always runs under
# the restricted PATH, where `psql` can only resolve to this harness's stub.
cat > "$STUB/sudo" <<'EOF'
#!/usr/bin/env bash
[[ "$1" == "-u" ]] && shift 2
exec "$@"
EOF
chmod +x "$STUB/ip" "$STUB/sudo"

# Ports the hook is hard-coded to use on the Docker path, and the port the native-fallback case
# feeds it via pg_lsclusters — deliberately not 5432, so a marker built from the default rather
# than the discovered cluster port fails the assertion.
DOCKER_PG_PORT=5499
REDIS_PORT=6399
NATIVE_PG_PORT=5433

STALE_MARKER='{"postgres":"Host=localhost;Port=1111;Database=STALE;Username=stale;Password=stale","redis":"localhost:2222"}'

passed=0
failed=0

# Builds an isolated project root holding a copy of the hook plus a previous session's marker, and
# clears every service stub along with the sentinels/logs they share. Each case therefore starts from
# "nothing installed" and sees exactly the stubs it declares afterwards — no case can inherit another
# case's, and inserting one in the wrong place cannot silently change which hook path it exercises.
setup_case() {
  local root="$WORK/$1"
  rm -f "$STUB/docker" "$STUB/pg_isready" "$STUB/pg_lsclusters" "$STUB/pg_ctlcluster" \
        "$STUB/psql" "$STUB/redis-server" "$STUB/redis-cli"
  rm -f "$WORK/pg-up" "$WORK/redis-up" "$WORK/pg_ctlcluster.log" "$WORK/psql.log"
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

# Stubs the natively-installed services the hook falls back to. Both services start out *down* and
# only come up once the hook has started them, so the start paths are exercised rather than the
# already-running shortcuts: pg_isready fails until pg_ctlcluster creates its sentinel, and
# redis-cli stays silent until redis-server creates its own. The stubs log the arguments they were
# called with so a case can assert on what the hook actually issued.
#
# `: >` rather than touch throughout: the stubs run under the same restricted PATH as the hook.
write_native_stubs() {
  printf '#!/usr/bin/env bash\n[[ -e "%s/pg-up" ]]\n' "$WORK" > "$STUB/pg_isready"
  # Columns match `pg_lsclusters -h`: version, cluster, port, status, owner.
  printf '#!/usr/bin/env bash\necho "18 main %s online postgres"\n' "$NATIVE_PG_PORT" > "$STUB/pg_lsclusters"
  printf '#!/usr/bin/env bash\necho "$*" >> "%s/pg_ctlcluster.log"\n: > "%s/pg-up"\n' "$WORK" "$WORK" > "$STUB/pg_ctlcluster"
  printf '#!/usr/bin/env bash\necho "$*" >> "%s/psql.log"\nexit 0\n' "$WORK" > "$STUB/psql"
  printf '#!/usr/bin/env bash\n: > "%s/redis-up"\nexit 0\n' "$WORK" > "$STUB/redis-server"
  printf '#!/usr/bin/env bash\n[[ -e "%s/redis-up" ]] && echo PONG\nexit 0\n' "$WORK" > "$STUB/redis-cli"
  chmod +x "$STUB/pg_isready" "$STUB/pg_lsclusters" "$STUB/pg_ctlcluster" \
           "$STUB/psql" "$STUB/redis-server" "$STUB/redis-cli"
}

# assert_logged <case name> <project root> <stub log file> <expected substring>
#
# Asserts the hook actually issued a given command to a stub, for the branches whose effect is a
# side effect on the host rather than a change to the marker.
assert_logged() {
  local name="$1" root="$2" log="$WORK/$3" expected="$4"
  if [[ -e "$log" ]] && grep -qF -- "$expected" "$log"; then
    pass_case "$name" "issued: $expected"
  else
    fail_case "$name" "never issued '$expected' (got: $(tr '\n' ';' < "$log" 2>/dev/null))" "$root"
  fi
}

echo "=== session-start.sh marker lifecycle ==="

# Every case declares its own stubs *after* setup_case, which resets them — order of the cases is
# therefore irrelevant to what each one exercises.

# 1. Terminal failure: nothing available at all. The hook gives up — and must not leave the
#    previous session's marker behind pointing at services that are not running.
root="$(setup_case terminal_failure)"
run_hook "$root"
assert_marker "terminal failure (no docker, no native services)" gone "$root"

# 2. Mixed success: Docker brings Postgres up but Redis fails, and the native fallback is also
#    unavailable. Partial bring-up is still a failed session — no usable marker may remain.
root="$(setup_case mixed_success)"
write_docker_stub vfs "none host" redis
run_hook "$root"
assert_marker "mixed success (postgres up, redis down)" gone "$root"

# 3. Full success: both containers up. The marker must be rewritten to describe this run rather
#    than the stale one it replaces.
root="$(setup_case full_success)"
write_docker_stub vfs "none host"
run_hook "$root"
assert_marker "docker success (both services up)" fresh "$root" "$DOCKER_PG_PORT" "$REDIS_PORT"

# 4. Healthy Docker: Testcontainers handles provisioning, so the hook writes no marker of its own
#    and must clear any inherited one before exiting.
root="$(setup_case testcontainers)"
write_docker_stub overlay2 bridge
run_hook "$root"
assert_marker "healthy docker (testcontainers path)" gone "$root"

# 5. Native fallback: no Docker at all, but both services are installed natively. This is the
#    second (and more fragile) marker-writing path — its Postgres port comes from parsing
#    pg_lsclusters, so the marker must carry the discovered port, not the 5432 default. The cluster
#    starts down, so the same run also covers the cluster-start branch: the version handed to
#    pg_ctlcluster and the port given to psql both come from parsing pg_lsclusters, and a marker
#    with the right port would otherwise hide either field being misread.
root="$(setup_case native_fallback)"
write_native_stubs
run_hook "$root"
assert_marker "native fallback (discovered cluster port)" fresh "$root" "$NATIVE_PG_PORT" "$REDIS_PORT"
assert_logged "native fallback (cluster start)" "$root" pg_ctlcluster.log "18 main start"
assert_logged "native fallback (test user)" "$root" psql.log "-p $NATIVE_PG_PORT -c CREATE USER test"

echo "=== $passed passed, $failed failed ==="
[[ $failed -eq 0 ]]
