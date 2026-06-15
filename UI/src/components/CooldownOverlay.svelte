<!-- A radial (conic-gradient) cooldown/duration overlay for a square tile: a dark wedge covering the
     tile from `sweep` degrees onward, leaving the rest transparent so the tile's icon shows through.
     Shared by the skill slots (sweep grows as the skill charges toward ready) and the combat effect
     chips (sweep = remaining-duration fraction, so the wedge grows as the effect expires and clears
     when it is re-applied). Purely presentational; the consumer computes `sweep` and positions this
     inside a `position: relative` tile. -->
<div class="cooldown-overlay" style:--sweep="{sweep}deg" aria-hidden="true"></div>

<script lang="ts">
interface Props {
	/** The transparent (revealed) arc in degrees, 0–360. The dark wedge covers the remainder. */
	sweep: number;
}

const { sweep }: Props = $props();
</script>

<style lang="scss">
.cooldown-overlay {
	position: absolute;
	inset: 0;
	background: conic-gradient(transparent var(--sweep), color-mix(in srgb, var(--black) 65%, transparent) var(--sweep));
	pointer-events: none;
}
</style>
