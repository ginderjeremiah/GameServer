<!-- A single-segment progress bar (a fraction of full width), used in stat cards
     and dossier tiles. -->
<div class="track" style:height="{height}px" role="presentation">
	<div class="fill" style:width="{clamped * 100}%" style:background={color}></div>
</div>

<script lang="ts">
interface Props {
	/** Fill fraction, 0..1. */
	frac: number;
	color: string;
	height?: number;
}

let { frac, color, height = 6 }: Props = $props();

const clamped = $derived(Math.max(0, Math.min(1, frac)));
</script>

<style lang="scss">
.track {
	width: 100%;
	background: color-mix(in srgb, var(--white) 6%, transparent);
	border-radius: 2px;
	overflow: hidden;
}

.fill {
	height: 100%;
	border-radius: 2px;
	transition: width 200ms cubic-bezier(0.4, 0, 0.2, 1);
}
</style>
