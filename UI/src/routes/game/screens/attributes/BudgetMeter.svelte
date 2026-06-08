<div class="budget">
	<div class="row">
		<span class="label">Points to spend</span>
		<span class="value" class:depleted={remaining <= 0}>
			{remaining}<span class="budget-total">&nbsp;/&nbsp;{budget}</span>
		</span>
	</div>
	<div class="track">
		<div class="fill" style="width: {pct}%"></div>
	</div>
</div>

<script lang="ts">
interface Props {
	remaining: number;
	budget: number;
}

const { remaining, budget }: Props = $props();

// The bar is clamped to [0, 100]: refunding committed points can push `remaining`
// above the baseline `budget`, but a full bar reads correctly in that case.
const pct = $derived(budget > 0 ? Math.max(0, Math.min(100, (remaining / budget) * 100)) : 0);
</script>

<style lang="scss">
.budget {
	display: flex;
	flex-direction: column;
	gap: 7px;
	min-width: 210px;
}

.row {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 12px;
	white-space: nowrap;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}

.value {
	font-family: var(--sans);
	font-size: 18px;
	font-weight: 600;
	letter-spacing: -0.3px;
	white-space: nowrap;
	color: var(--accent);
	text-shadow: 0 0 12px color-mix(in srgb, var(--accent) 40%, transparent);

	&.depleted {
		color: var(--text-muted);
		text-shadow: none;
	}
}

.budget-total {
	font-size: 11px;
	font-weight: 400;
	color: var(--text-muted);
}

.track {
	height: 4px;
	border-radius: 2px;
	background: color-mix(in srgb, var(--white) 7%, transparent);
	overflow: hidden;
}

.fill {
	height: 100%;
	background: var(--accent);
	box-shadow: 0 0 8px var(--accent);
	transition: width 220ms cubic-bezier(0.4, 0, 0.2, 1);
}
</style>
