<!-- The boss accent motif: a small rotated gold square, optionally glowing. Reused
     across the trigger, boss bar, and victory overlay. -->
<span class="boss-diamond" class:glow style:--diamond-size="{size}px" aria-hidden="true"></span>

<script lang="ts">
type Props = {
	/** Edge length in px. */
	size?: number;
	/** Whether to render the accent glow. */
	glow?: boolean;
};

const { size = 8, glow = true }: Props = $props();
</script>

<style lang="scss">
.boss-diamond {
	display: inline-block;
	width: var(--diamond-size);
	height: var(--diamond-size);
	transform: rotate(45deg);
	background: var(--boss-accent);
	flex-shrink: 0;

	&.glow {
		box-shadow: 0 0 8px color-mix(in srgb, var(--boss-accent) 80%, transparent);
	}
}
</style>
