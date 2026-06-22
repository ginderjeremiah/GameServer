<!-- A bordered rarity pill (boxed tier label tinted by rarity). Distinct from RarityTag's
     dot + label treatment; shared by the skill inspector and the codex skill dossier headers,
     where rarity reads as a secondary classifier alongside the kind/scaling eyebrow. -->
<span class="rarity-tag-box" style:--rarity={color}>{label}</span>

<script lang="ts">
interface Props {
	/** Themeable rarity hue, e.g. `var(--rarity-rare)`. */
	color: string;
	/** Tier label, e.g. `Rare`. */
	label: string;
}

const { color, label }: Props = $props();
</script>

<style lang="scss">
.rarity-tag-box {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--rarity);
	padding: 2px 7px;
	border: 1px solid color-mix(in srgb, var(--rarity) 45%, transparent);
	border-radius: 3px;
	background: color-mix(in srgb, var(--rarity) 12%, transparent);
}
</style>
