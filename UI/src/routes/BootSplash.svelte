<!-- Lightweight splash shown by the root layout while a session is being restored on boot (a
	Auth/Status + a reference-version check, sub-second when the cache is warm). It deliberately
	carries no manifest or "Enter" affordance — the point of the resume path is that the player drops
	straight back into the game without the loading screen. The pulsing diamond mirrors the
	login/loading chrome; `pulse-glow` is the shared keyframe in styles/common.scss (and is collapsed
	by the global reduced-motion rule there). -->
<div class="boot-splash" data-testid="boot-splash" role="status" aria-live="polite">
	<DiamondMark pulsing />
	<span class="sr-only">Restoring your session…</span>
</div>

<script lang="ts">
import DiamondMark from '$components/DiamondMark.svelte';
</script>

<style lang="scss">
.boot-splash {
	width: 100%;
	height: 100%;
	display: flex;
	align-items: center;
	justify-content: center;
}
</style>
