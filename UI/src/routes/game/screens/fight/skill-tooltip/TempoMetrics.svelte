<div class="tt-metrics">
	<div class="tt-metric">
		<div class="tt-metric-label">Cooldown</div>
		<div class="tt-metric-value">{formatNum(cooldown)}s</div>
	</div>
	<div class="tt-metric">
		<div class="tt-metric-label">DPS</div>
		<div class="tt-metric-value">{formatNum(dps)}</div>
	</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';

interface Props {
	/** Adjusted cooldown in seconds. */
	cooldown: number;
	/** Damage per second (total ÷ cooldown). */
	dps: number;
}

const { cooldown, dps }: Props = $props();
</script>

<style lang="scss">
.tt-metrics {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 10px;
}

.tt-metric {
	padding: 7px 10px;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 2px;
}

.tt-metric-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.3px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
	margin-bottom: 2px;
}

.tt-metric-value {
	font-family: var(--mono);
	font-size: 14px;
	color: var(--text-primary);
	letter-spacing: 0.3px;
}
</style>
