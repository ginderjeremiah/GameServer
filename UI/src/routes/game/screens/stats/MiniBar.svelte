<!-- A single-segment progress bar (a fraction of full width), used in stat cards and dossier tiles.
     Composes the shared Bar primitive presentationally — the value is shown elsewhere on the card. -->
<Bar
	value={frac}
	max={1}
	presentational
	--bar-height="{height}px"
	--bar-radius="2px"
	--bar-fill={color}
	--bar-transition="width 200ms cubic-bezier(0.4, 0, 0.2, 1)"
/>

<script lang="ts">
import Bar from '$components/Bar.svelte';

interface Props {
	/** Fill fraction, 0..1. */
	frac: number;
	color: string;
	height?: number;
}

let { frac, color, height = 6 }: Props = $props();
</script>
