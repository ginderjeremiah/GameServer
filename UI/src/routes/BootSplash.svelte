<!-- Lightweight splash shown by the root layout while a session is being restored on boot (a
	Login/Status + a reference-version check, sub-second when the cache is warm). It deliberately
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

// Accessible label for screen readers without showing visible text.
.sr-only {
	position: absolute;
	width: 1px;
	height: 1px;
	margin: -1px;
	padding: 0;
	overflow: hidden;
	clip: rect(0, 0, 0, 0);
	white-space: nowrap;
	border: 0;
}
</style>
