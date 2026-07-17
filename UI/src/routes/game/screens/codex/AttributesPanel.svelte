<!-- Attributes sub-tab: the enemy's attribute distribution scaled to a chosen level. A boss is a
     fixed-level encounter; a normal enemy gets a live level slider. "Show scaling" reveals the
     base + per-level breakdown. Primary bars come straight from the distribution; the derived
     Max Health / Toughness cards are computed through the real battle attribute composition. -->
<div class="attributes">
	<div class="section-head">
		<span class="label">Attribute distribution</span>
		<button
			type="button"
			class="scaling-toggle"
			class:on={view.enemiesTab.scaling}
			aria-pressed={view.enemiesTab.scaling}
			data-testid="codex-scaling-toggle"
			onclick={() => view.enemiesTab.toggleScaling()}
		>
			Show scaling
		</button>
	</div>

	{#if view.enemiesTab.range}
		{#if view.enemiesTab.range.fixed}
			<div class="fixed-level">
				<span>FIXED ENCOUNTER</span>
				<span class="rule"></span>
				<span class="fixed-val">LVL {view.enemiesTab.level}</span>
			</div>
		{:else}
			<div class="level-control">
				<span class="lvl-label">LVL</span>
				<input
					type="range"
					class="level-range"
					min={view.enemiesTab.range.min}
					max={view.enemiesTab.range.max}
					value={view.enemiesTab.level}
					aria-label="Level"
					data-testid="codex-level-slider"
					oninput={(e) => view.enemiesTab.setLevel(+e.currentTarget.value)}
				/>
				<span class="lvl-val">{view.enemiesTab.level}</span>
			</div>
		{/if}
	{/if}

	<div class="primary">
		{#each view.enemiesTab.attributes.primary as stat (stat.attributeId)}
			<div class="stat">
				<div class="stat-row">
					<span class="code">{attributeCode(stat.attributeId, staticData.attributes)}</span>
					<span class="bar-wrap" style:--bar-fill={attributeColor(stat.attributeId)}>
						<Bar value={stat.value} max={view.enemiesTab.maxPrimary} presentational />
					</span>
					<span class="val">{stat.value}</span>
				</div>
				{#if view.enemiesTab.scaling}
					<div class="scale-line">{stat.base} base + {stat.perLevel}/lvl × {view.enemiesTab.level}</div>
				{/if}
			</div>
		{/each}
	</div>

	<div class="secondary">
		{#each view.enemiesTab.attributes.secondary as stat (stat.attributeId)}
			<div class="card">
				<div class="card-label">{attributeName(stat.attributeId, staticData.attributes)}</div>
				<div class="card-val" style:color={secondaryColor(stat.attributeId)}>{stat.value}</div>
				{#if view.enemiesTab.scaling}
					<div class="card-scale">+{roundDelta(stat.perLevel)}/lvl</div>
				{/if}
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import { Bar } from '$components';
import { EAttribute } from '$lib/api';
import { attributeCode, attributeColor, attributeName } from '$lib/common';
import { staticData } from '$stores';
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();

// Derived secondaries have no themed `--attr-*` hue, so map the two surfaced ones to semantic tokens
// (health → success/green, toughness → accent/blue), matching the design's intent.
const secondaryColor = (id: EAttribute): string => (id === EAttribute.MaxHealth ? 'var(--success)' : 'var(--accent)');

// A derived stat's per-level slope can carry float noise; round it for the scaling readout.
const roundDelta = (value: number): number => Math.round(value * 10) / 10;
</script>

<style lang="scss">
.section-head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-bottom: 11px;
}

.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.scaling-toggle {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	background: transparent;
	border: 1px solid var(--border-light);
	border-radius: 10px;
	padding: 2px 9px;
	color: var(--text-muted);
	cursor: pointer;

	&.on {
		background: color-mix(in srgb, var(--accent) 16%, transparent);
		border-color: color-mix(in srgb, var(--accent) 50%, transparent);
		color: var(--accent);
	}
}

.fixed-level {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 14px;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-muted);
}

.rule {
	height: 1px;
	flex: 1;
	background: var(--border-subtle);
}

.fixed-val {
	color: var(--boss-accent);
	font-size: 11px;
}

.level-control {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 14px;
}

.lvl-label {
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-muted);
}

.lvl-val {
	font-family: var(--mono);
	font-size: 13px;
	color: var(--enemy-accent);
	font-weight: 500;
	width: 26px;
	text-align: right;
}

.level-range {
	flex: 1;
	-webkit-appearance: none;
	appearance: none;
	height: 3px;
	border-radius: 3px;
	background: var(--border-light);
	outline: none;

	&::-webkit-slider-thumb {
		-webkit-appearance: none;
		appearance: none;
		width: 14px;
		height: 14px;
		border-radius: 50%;
		background: var(--enemy-accent);
		cursor: pointer;
		box-shadow: 0 0 8px color-mix(in srgb, var(--enemy-accent) 60%, transparent);
		border: 2px solid var(--surface);
	}

	&::-moz-range-thumb {
		width: 14px;
		height: 14px;
		border-radius: 50%;
		background: var(--enemy-accent);
		cursor: pointer;
		border: 2px solid var(--surface);
	}
}

.stat {
	margin-bottom: 9px;
}

.stat-row {
	display: flex;
	align-items: center;
	gap: 9px;
}

.code {
	font-family: var(--mono);
	font-size: 9.5px;
	width: 30px;
	color: var(--text-tertiary);
}

.bar-wrap {
	flex: 1;
	--bar-height: 8px;
	--bar-radius: 3px;
	--bar-track-bg: color-mix(in srgb, var(--white) 6%, transparent);
}

.val {
	font-family: var(--mono);
	font-size: 11.5px;
	width: 34px;
	text-align: right;
	color: var(--text-primary);
	font-weight: 500;
}

.scale-line {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
	padding-left: 39px;
	margin-top: 2px;
}

.secondary {
	display: flex;
	gap: 10px;
	margin-top: 14px;
	padding-top: 13px;
	border-top: 1px solid var(--border-subtle);
}

.card {
	flex: 1;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 9px 11px;
}

.card-label {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.card-val {
	font-family: var(--mono);
	font-size: 17px;
	font-weight: 500;
	margin-top: 2px;
}

.card-scale {
	font-family: var(--mono);
	font-size: 8px;
	color: var(--text-muted);
	margin-top: 2px;
}
</style>
