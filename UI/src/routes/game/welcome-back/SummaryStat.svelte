<!-- A single labelled figure in the welcome-back summary grid (e.g. "Battles won · 142"). Presentational. -->
<div class="summary-stat" class:muted>
	<span class="value" style:color={accent ?? null}>{value}</span>
	<span class="label">{label}</span>
</div>

<script lang="ts">
interface Props {
	label: string;
	value: string | number;
	/** Optional themeable accent for the value (e.g. the gold XP/level hue). */
	accent?: string;
	/** Dims a zero/neutral figure so the meaningful gains read first. */
	muted?: boolean;
}

let { label, value, accent, muted = false }: Props = $props();
</script>

<style lang="scss">
.summary-stat {
	display: flex;
	flex-direction: column;
	gap: 2px;
	padding: 10px 12px;
	background: var(--panel-2);
	border: var(--default-border);
	border-radius: var(--border-radius);

	&.muted .value {
		color: var(--text-muted);
	}
}

.value {
	font-size: 1.35rem;
	font-weight: 600;
	color: var(--text-primary);
	font-variant-numeric: tabular-nums;
}

.label {
	font-family: var(--mono);
	font-size: 0.7rem;
	text-transform: uppercase;
	letter-spacing: 0.08em;
	color: var(--text-tertiary);
}
</style>
