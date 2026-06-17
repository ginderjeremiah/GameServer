#!/usr/bin/env python3
"""Generate a game icon via the Antigravity CLI (`agy`) -- on your subscription, not the API.

`agy` signs in with your Google account, so its built-in `generate_image` tool draws on your
Antigravity/Pro subscription's image quota instead of billing a GEMINI_API_KEY.

How it works: the `generate_image` tool can NOT be told an output path -- it always saves a
1024x1024 JPEG into agy's per-session artifact ("brain") folder. So this script asks the agent
to do ONLY the generation (one tool call -- no format conversion, no copying, which otherwise
sends it into a brittle PowerShell-quoting dance), then locates that native JPEG itself and
copies it into the staging dir. With --strip it chains strip-bg.py, which keys the lime/magenta
backdrop straight from the JPEG (no PNG conversion needed) to a transparent PNG.

IMPORTANT: `agy` needs a real console. By default the script inherits the console so agy works
and shows its progress -- run it from an INTERACTIVE terminal, or it hangs in a headless/piped
shell with no TTY. It finds the result by scanning the brain folder, not by parsing agy's text.

--detached lets a HEADLESS caller drive it: the script re-spawns itself attached to its own hidden
console (which IS a real TTY, so agy runs), then polls a status file the child writes on exit and
returns. Nothing appears on the desktop and no interactive typing is needed.

--model picks the agy SESSION model that drives the generate_image tool (not the image model
itself). It defaults to Claude Sonnet, and if that model has no usage left the run produces no
image, so the script automatically retries once on --fallback-model (Gemini Flash) before failing.

agy's generate_image tool also takes base images (its ImagePaths arg, max 3), so this script can
edit and combine too: --edit feeds the latest <name>-v<N> back in to refine it, and --input <path>
(repeatable) adds reference images to combine. Output is still a square 1024 JPEG, next version.

Generate:        python generate-agy.py --prompt "<full prompt>" --name snow-fox
Generate+strip:  python generate-agy.py --prompt "<full prompt>" --name snow-fox --strip
From headless:   python generate-agy.py --prompt "<full prompt>" --name snow-fox --strip --detached
Edit latest:     python generate-agy.py --prompt "make the scarf red, keep all else" --name snow-fox --edit
Combine inputs:  python generate-agy.py --prompt "put this fox in this forest" --name scene --input fox.png --input forest.png
Pick a model:    python generate-agy.py ... --model "Gemini 3.5 Flash (Low)" --fallback-model ""
Extra strip arg: python generate-agy.py ... --strip --strip-arg=--trim-corners
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

BRAIN = Path.home() / ".gemini" / "antigravity-cli" / "brain"

# agy session/agent model (the LLM that calls the generate_image tool, NOT the image model). We
# default to Sonnet; when its usage is exhausted the run yields no image, so we retry on the Flash
# fallback. These are agy's display names -- see `agy models` for the current list.
DEFAULT_MODEL = "Claude Sonnet 4.6 (Thinking)"
FALLBACK_MODEL = "Gemini 3.5 Flash (Low)"

# One tool call, nothing else. The old "save as PNG to <path>" wording made the agent run a
# fragile format-conversion command; here it only generates and the wrapper handles the file.
# {paths_clause} is empty for a fresh generation, or sets ImagePaths for an edit/combine.
INSTRUCTION = """\
Produce a single image and nothing else. Do NOT explore the workspace, list directories, read \
project files, convert image formats, copy or move files, or run any shell command. Your ONLY \
action is to call the built-in generate_image tool exactly once, with ImageName set to exactly \
"{image_name}"{paths_clause}, and this exact Prompt:

{prompt}

After the tool reports the image was generated, reply with only the word DONE."""

# Appended to the instruction when editing/combining, to set the tool's ImagePaths argument.
PATHS_CLAUSE = (
    ", with ImagePaths set to exactly these {n} absolute image path(s) as the base to "
    "edit/combine: [{joined}] (pass the paths straight to the tool; do not open or read them "
    "yourself)"
)


def find_agy(override):
    if override:
        return override
    cand = os.path.expandvars(r"%LOCALAPPDATA%\agy\bin\agy.exe")
    if os.path.isfile(cand):
        return cand
    found = shutil.which("agy")
    if found:
        return found
    raise RuntimeError("could not find 'agy'. Pass --agy <path-to-agy.exe>.")


def safe_image_name(name):
    """generate_image uses ImageName as the artifact filename stem; keep it filesystem-safe."""
    return re.sub(r"[^A-Za-z0-9_-]", "_", name)


def next_version(out_dir, name):
    """Lowest unused N for <name>-v<N>.* (raw or -cut) so repeated runs never clobber."""
    pat = re.compile(rf"^{re.escape(name)}-v(\d+)(?:-cut)?\.[a-z0-9]+$", re.IGNORECASE)
    latest = 0
    if out_dir.exists():
        for f in out_dir.iterdir():
            m = pat.match(f.name)
            if m and int(m.group(1)) > latest:
                latest = int(m.group(1))
    return latest + 1


def latest_raw(out_dir, name):
    """Newest raw <name>-v<N>.<ext> file (excludes -cut variants), for --edit input. None if absent."""
    pat = re.compile(rf"^{re.escape(name)}-v(\d+)\.[a-z0-9]+$", re.IGNORECASE)
    best, best_n = None, 0
    if out_dir.exists():
        for f in out_dir.iterdir():
            m = pat.match(f.name)
            if m and int(m.group(1)) >= best_n:
                best, best_n = f, int(m.group(1))
    return best


def find_artifact(image_name, since):
    """Newest brain JPEG created since `since` (epoch secs).

    Prefers files named <image_name>_*.jpg, but falls back to the newest JPEG created since
    `since` (the agent occasionally adjusts the ImageName it passes to the tool).
    """
    if not BRAIN.exists():
        return None
    named, any_new = [], []
    for f in BRAIN.glob("**/*.jpg"):
        try:
            mtime = f.stat().st_mtime
        except OSError:
            continue
        if mtime + 1 < since:  # 1s slack for mtime granularity
            continue
        any_new.append((mtime, f))
        if f.name.lower().startswith(image_name.lower() + "_"):
            named.append((mtime, f))
    pool = named or any_new
    return max(pool, key=lambda t: t[0])[1] if pool else None


def resolve_input_paths(args, out_dir):
    """Absolute base-image paths for generate_image's ImagePaths: the --edit latest version first,
    then any --input references. Validates existence and the tool's 3-image cap."""
    paths = []
    if args.edit:
        prev = latest_raw(out_dir, args.name)
        if not prev:
            raise RuntimeError(f"--edit: no existing {args.name}-v<N> image in {out_dir} to edit.")
        paths.append(str(prev.resolve()))
    for p in args.input:
        ip = Path(p)
        if not ip.is_file():
            raise RuntimeError(f"--input image not found: {p}")
        paths.append(str(ip.resolve()))
    if len(paths) > 3:
        raise RuntimeError(
            f"generate_image accepts at most 3 input images; got {len(paths)} (--edit counts as one)."
        )
    return paths


def build_paths_clause(paths):
    """Render the ImagePaths sentence appended to the instruction (empty for a fresh generation)."""
    if not paths:
        return ""
    joined = ", ".join(f'"{p}"' for p in paths)
    return PATHS_CLAUSE.format(n=len(paths), joined=joined)


def run_agy_once(agy, instruction, model, image_name, timeout, cwd):
    """Run agy a single time on the given session model; return its newest brain artifact or None.

    Returns None (rather than raising) on timeout or no-artifact so the caller can fall back to
    another model. A model with no usage left simply produces no image -- the same None path."""
    start = time.time()
    print(f"Generating via agy (model={model!r}, ImageName={image_name}) ...")
    # Inherit stdio: agy needs the real console, and we want its progress visible.
    try:
        subprocess.run(
            [agy, "-p", instruction, "--model", model, "--dangerously-skip-permissions"],
            cwd=cwd, timeout=timeout,
        )
    except subprocess.TimeoutExpired:
        print(f"  agy timed out after {timeout}s on model {model!r}.")
        return None
    return find_artifact(image_name, start)


def generate(args):
    """Run agy (in the inherited console), copy the artifact, optionally strip. Returns the final
    path. Raises RuntimeError on any failure so callers can report it (detached mode needs this).

    Tries args.model first; if it yields no image (e.g. that model's usage is exhausted) and a
    distinct args.fallback_model is set, retries once on the fallback before giving up."""
    agy = find_agy(args.agy)
    out_dir = Path(args.dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    version = next_version(out_dir, args.name)
    image_name = safe_image_name(args.name)
    instruction = INSTRUCTION.format(
        image_name=image_name, prompt=args.prompt,
        paths_clause=build_paths_clause(resolve_input_paths(args, out_dir)),
    )

    artifact = run_agy_once(agy, instruction, args.model, image_name, args.timeout, str(out_dir))
    if not artifact and args.fallback_model and args.fallback_model != args.model:
        print(f"No image from {args.model!r} (its usage may be exhausted); "
              f"retrying on fallback {args.fallback_model!r} ...")
        artifact = run_agy_once(agy, instruction, args.fallback_model, image_name, args.timeout, str(out_dir))
    if not artifact:
        raise RuntimeError(
            f"no generated image found in agy's brain folder ({BRAIN}) after the run(s). "
            "agy may have failed or saved elsewhere -- check its output above."
        )
    raw_path = out_dir / f"{args.name}-v{version}{artifact.suffix.lower()}"
    shutil.copy2(artifact, raw_path)
    print(f"SAVED: {raw_path}  (native {artifact.suffix.lstrip('.').upper()} from {artifact.name})")
    final = raw_path

    if args.strip:
        cut_path = out_dir / f"{args.name}-v{version}-cut.png"
        strip_py = Path(__file__).with_name("strip-bg.py")
        cmd = [sys.executable, str(strip_py), str(raw_path), str(cut_path),
               "--bg-hue", str(args.bg_hue), *args.strip_arg]
        print(f"Stripping background -> {cut_path}")
        if subprocess.run(cmd).returncode != 0:
            raise RuntimeError("strip-bg.py failed.")
        print(f"STRIPPED: {cut_path}")
        print("Verify on a dark background, then copy into UI/static/img under the display-name filename.")
        final = cut_path

    return final


# CreateProcess flag (Windows): give the child its OWN console (conhost) but with NO window. That
# console is still a real TTY, so agy runs instead of hanging the way it does under a headless/piped
# parent -- and nothing flashes on the desktop. (agy's tty check is on the console handle, not window
# visibility: CREATE_NO_WINDOW, CREATE_NEW_CONSOLE, and a SW_HIDE'd console all pass it.)
CREATE_NO_WINDOW = 0x08000000


def run_detached(args):
    """Re-spawn this script in a new console window (so agy gets a TTY) and poll for its result.

    Lets a headless caller (e.g. an agent's piped shell) drive generation. Returns 0 on success
    or an error string for sys.exit. The prompt rides along as a real argv element via subprocess'
    list form, sidestepping the cross-process quote-mangling that plagues Start-Process."""
    out_dir = Path(args.dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    # Absolute so the child writes to the same place regardless of its inherited cwd.
    status_file = (out_dir / f".{safe_image_name(args.name)}.detached.status").resolve()
    try:
        status_file.unlink()
    except FileNotFoundError:
        pass

    child = [sys.executable, str(Path(__file__).resolve()),
             "--prompt", args.prompt, "--name", args.name, "--dir", args.dir,
             "--bg-hue", str(args.bg_hue), "--timeout", str(args.timeout),
             "--model", args.model, "--fallback-model", args.fallback_model,
             "--_status-file", str(status_file)]
    if args.strip:
        child.append("--strip")
    for sa in args.strip_arg:
        child += ["--strip-arg", sa]
    if args.edit:
        child.append("--edit")
    for ip in args.input:
        child += ["--input", ip]
    if args.agy:
        child += ["--agy", args.agy]

    # The child may run agy twice (primary model, then fallback), so allow for both attempts.
    attempts = 2 if (args.fallback_model and args.fallback_model != args.model) else 1
    grace = args.timeout * attempts + 60
    print(f"Spawning agy in a hidden console (detached); waiting up to {grace}s ...")
    proc = subprocess.Popen(child, creationflags=CREATE_NO_WINDOW)

    deadline = time.time() + grace
    while time.time() < deadline:
        if status_file.exists():
            break
        if proc.poll() is not None:
            time.sleep(1)  # child exited; grace for the status file to flush
            break
        time.sleep(2)

    if not status_file.exists():
        return ("ERROR: detached run produced no status file. The agy window may still be open "
                "or closed early -- check it, or rerun without --detached in an interactive terminal.")
    result = status_file.read_text(encoding="utf-8").strip()
    try:
        status_file.unlink()
    except OSError:
        pass
    if result.startswith("OK "):
        print(f"SAVED (detached): {result[3:]}")
        return 0
    return f"ERROR (detached child): {result[5:] if result.startswith('FAIL ') else result}"


def main():
    ap = argparse.ArgumentParser(description="Generate a game icon via the Antigravity CLI (agy), on your subscription.")
    ap.add_argument("--prompt", required=True, help="Full composed prompt: '<SUBJECT>. <STYLE> <BACKGROUND>'.")
    ap.add_argument("--name", required=True, help="Stable kebab-case slug, e.g. beginner-sword.")
    ap.add_argument("--dir", default="generated-images", help="Output directory (default: generated-images).")
    ap.add_argument("--edit", action="store_true",
                    help="Edit the most recent <name>-v<N> image instead of generating fresh "
                         "(passes it as the base via the tool's ImagePaths).")
    ap.add_argument("--input", action="append", default=[], metavar="PATH",
                    help="Reference image to edit/combine (repeatable). With --edit, total images "
                         "must stay <= 3 (the generate_image tool's cap).")
    ap.add_argument("--strip", action="store_true", help="After generating, chroma-key the background with strip-bg.py.")
    ap.add_argument("--bg-hue", type=float, default=85.0, help="Backdrop hue for --strip (85=lime, 300=magenta).")
    ap.add_argument("--strip-arg", action="append", default=[],
                    help="Extra arg passed through to strip-bg.py (repeatable). Use =: --strip-arg=--trim-corners")
    ap.add_argument("--agy", help="Path to agy.exe (default: auto-detected under LOCALAPPDATA, then PATH).")
    ap.add_argument("--model", default=DEFAULT_MODEL,
                    help=f'agy session model driving the generate_image tool (default: "{DEFAULT_MODEL}").')
    ap.add_argument("--fallback-model", default=FALLBACK_MODEL,
                    help=f'Model to retry on if --model yields no image, e.g. its usage is exhausted '
                         f'(default: "{FALLBACK_MODEL}"). Pass "" to disable the fallback.')
    ap.add_argument("--timeout", type=int, default=600, help="Seconds to wait for agy per attempt (default: 600).")
    ap.add_argument("--detached", action="store_true",
                    help="Spawn agy in a hidden console (gives it a TTY, no window) and poll for the "
                         "result; lets a headless caller run this without an interactive terminal.")
    # Internal: the detached parent passes this so the child reports success/failure back via file.
    ap.add_argument("--_status-file", help=argparse.SUPPRESS)
    args = ap.parse_args()

    if args.detached:
        sys.exit(run_detached(args))

    status_file = Path(args._status_file) if args._status_file else None
    try:
        final = generate(args)
    except RuntimeError as e:
        if status_file:
            status_file.write_text(f"FAIL {e}", encoding="utf-8")
        sys.exit(f"ERROR: {e}")
    if status_file:
        status_file.write_text(f"OK {final}", encoding="utf-8")


if __name__ == "__main__":
    main()
