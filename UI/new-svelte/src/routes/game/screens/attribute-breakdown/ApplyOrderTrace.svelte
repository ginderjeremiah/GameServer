<!-- The literal apply-order trace: starting at 0, each additive contribution is
     added in turn, then (if any) the additive subtotal is multiplied, ending at
     the final value — the same order the engine's AttributeCollection resolves. -->
<div class="trace-wrap">
	<span class="section-label">Apply order</span>
	<div class="trace">
		<div class="line faint">
			<span class="swatch-slot"></span>
			<span class="label">Start</span>
			<span class="op"></span>
			<span class="value">{fmtNum(0, dec)}</span>
		</div>

		{#each adds as line, i (i)}
			<div class="line">
				<span class="swatch-slot"><span class="swatch" style:background={sourceColor(line.source)}></span></span>
				<span class="label">{traceLabel(line)}</span>
				<span class="op">{fmtSigned(line.applied)}</span>
				<span class="value">{fmtNum(line.running, dec)}</span>
			</div>
		{/each}

		{#if mults.length > 0}
			<div class="divider dashed"></div>
			<div class="line strong">
				<span class="swatch-slot"></span>
				<span class="label">Additive subtotal</span>
				<span class="op"></span>
				<span class="value">{fmtNum(computed.additiveSubtotal, dec)}</span>
			</div>
			{#each mults as line, i (i)}
				<div class="line">
					<span class="swatch-slot"><span class="swatch" style:background={sourceColor(line.source)}></span></span>
					<span class="label">{traceLabel(line)} (mult)</span>
					<span class="op">×{line.factor}</span>
					<span class="value">{fmtNum(line.running, dec)}</span>
				</div>
			{/each}
		{/if}

		<div class="divider"></div>
		<div class="line strong final">
			<span class="swatch-slot"></span>
			<span class="label">Final</span>
			<span class="op"></span>
			<span class="value">{fmtNum(computed.total, dec)}</span>
		</div>
	</div>
</div>

<script lang="ts">
import { sourceColor } from './source-display';
import { fmtNum, fmtSigned, traceLabel, type LabeledModifier } from './attribute-breakdown-view.svelte';
import type { ComputedAttribute } from '$lib/battle';

interface Props {
	computed: ComputedAttribute<LabeledModifier>;
	dec: number;
}

let { computed, dec }: Props = $props();

const adds = $derived(computed.lines.filter((l) => !l.multiplied));
const mults = $derived(computed.lines.filter((l) => l.multiplied));
</script>

<style lang="scss">
.section-label {
	display: block;
	margin-bottom: 10px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.trace {
	background: color-mix(in srgb, var(--black) 25%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
	border-radius: 5px;
	padding: 12px 14px;
}

.line {
	display: grid;
	grid-template-columns: 9px 1fr auto auto;
	align-items: center;
	gap: 9px;
	padding: 3px 0;

	&.faint .label {
		color: var(--text-muted);
	}

	&.strong .label {
		color: var(--text-primary);
	}
}

.swatch-slot {
	width: 9px;
	display: flex;
}

.swatch {
	width: 8px;
	height: 8px;
	border-radius: 1px;
}

.label {
	font-size: 11.5px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.op {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
	min-width: 46px;
	text-align: right;
}

.value {
	font-family: var(--mono);
	font-size: 11.5px;
	min-width: 60px;
	text-align: right;
	color: var(--text-tertiary);

	.strong & {
		color: var(--text-primary);
		font-weight: 600;
	}
}

.final .value {
	color: var(--accent);
}

.divider {
	height: 1px;
	background: color-mix(in srgb, var(--white) 12%, transparent);
	margin: 7px 0 6px;

	&.dashed {
		background: none;
		border-top: 1px dashed color-mix(in srgb, var(--white) 14%, transparent);
	}
}
</style>
