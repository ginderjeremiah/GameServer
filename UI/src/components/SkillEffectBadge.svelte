<!-- A small marker shown on a skill that carries one or more authored effects (timed buffs/debuffs).
     Purely presentational: the consumer decides when to render it (the skill's `effects` list is
     non-empty) and where to position it. The accent defaults to the themeable `--effect-accent`. -->
<span class="effect-badge" style:--badge-accent={accent} role="img" aria-label="Carries effects" title="Carries effects"
></span>

<script lang="ts">
type Props = {
	/** Overrides the marker accent (CSS colour). Defaults to the themeable effect accent. */
	accent?: string;
};

const { accent = 'var(--effect-accent)' }: Props = $props();
</script>

<style lang="scss">
.effect-badge {
	display: block;
	width: 7px;
	height: 7px;
	transform: rotate(45deg);
	background: var(--badge-accent);
	box-shadow: 0 0 5px color-mix(in srgb, var(--badge-accent) 75%, transparent);
	border-radius: 1px;
}
</style>
