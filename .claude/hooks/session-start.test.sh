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
# plus the handful of coreutils the hook itself calls.
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
for cmd in bash cat rm seq sleep awk grep dirname ip sudo; do
  target="$(command -v "$cmd" 2>/dev/null)"
  if [[ -n "$target" ]]; then
    ln -sf "$target" "$STUB/$cmd"
  fi
done

STALE_MARKER='{"postgres":"Host=localhost;Port=1111;Database=STALE;Username=stale;Password=stale","redis":"localhost:2222"}'

passed=0
failed=0

# Builds an isolated project root holding a copy of the hook plus a previous session's marker.
setup_case() {
  local root="$WORK/$1"
  rm -rf "$root"
  mkdir -p "$root/.claude/hooks"
  cp "$HOOK_SRC" "$root/.claude/hooks/session-start.sh"
  printf '%s' "$STALE_MARKER" > "$root/.container-info.json"
  echo "$root"
}

run_hook() {
  CLAUDE_CODE_REMOTE=true PATH="$STUB" "$STUB/bash" "$1/.claude/hooks/session-start.sh" > "$1/out.log" 2>&1
}

# assert_marker <case name> <gone|fresh> <project root>
assert_marker() {
  local name="$1" expectation="$2" marker="$3/.container-info.json"
  if [[ "$expectation" == "gone" ]]; then
    if [[ ! -e "$marker" ]]; then
      echo "  PASS: $name — stale marker removed"
      ((passed++))
    else
      echo "  FAIL: $name — stale marker survived: $(tr -d '\n' < "$marker")"
      ((failed++))
    fi
  elif [[ -e "$marker" ]] && ! grep -q STALE "$marker"; then
    echo "  PASS: $name — marker describes this run"
    ((passed++))
  else
    echo "  FAIL: $name — expected a fresh marker, found: $(tr -d '\n' < "$marker" 2>/dev/null)"
    ((failed++))
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

echo "=== session-start.sh marker lifecycle ==="

# 1. Terminal failure: nothing available at all. The hook gives up — and must not leave the
#    previous session's marker behind pointing at services that are not running.
rm -f "$STUB/docker"
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
write_docker_stub vfs "none host" ""
root="$(setup_case full_success)"
run_hook "$root"
assert_marker "docker success (both services up)" fresh "$root"

# 4. Healthy Docker: Testcontainers handles provisioning, so the hook writes no marker of its own
#    and must clear any inherited one before exiting.
write_docker_stub overlay2 bridge ""
root="$(setup_case testcontainers)"
run_hook "$root"
assert_marker "healthy docker (testcontainers path)" gone "$root"

echo "=== $passed passed, $failed failed ==="
[[ $failed -eq 0 ]]
