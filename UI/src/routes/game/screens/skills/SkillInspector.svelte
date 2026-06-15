<div class="detail">
	{#if metrics}
		<div class="d-top">
			<div class="d-ico-wrap">
				<SkillIcon skill={metrics.skill} locked={!metrics.unlocked} size={78} />
				{#if equipped}
					<span class="slotbadge">{view.slotOf(metrics.skill.id)}</span>
				{/if}
			</div>
			<div class="d-headings">
				<div class="eyebrow">{scalesEyebrow}</div>
				<div class="d-name">{metrics.skill.name}</div>
				<div class="d-desc">{metrics.skill.description}</div>
			</div>
			<div class="d-cta">
				{#if !metrics.unlocked}
					<button type="button" class="btn dim" disabled>🔒 Locked</button>
					<div class="d-hint">Unlock by completing<br /><b>{metrics.source?.name ?? 'a challenge'}</b></div>
				{:else if equipped}
					<button type="button" class="btn danger" onclick={() => view.toggle(metrics.skill.id)}
						>✓ In loadout · Remove</button
					>
					<div class="d-hint">priority slot {view.slotOf(metrics.skill.id)} · drag below to reorder</div>
				{:else if pending}
					<button type="button" class="btn ghost" onclick={() => view.cancelSwap()}>Cancel swap</button>
					<div class="d-hint">pick an equipped card below to replace</div>
				{:else if full}
					<button type="button" class="btn ghost" onclick={() => view.toggle(metrics.skill.id)}
						>Loadout full · Swap…</button
					>
					<div class="d-hint">choose which slot to replace</div>
				{:else}
					<button type="button" class="btn" onclick={() => view.toggle(metrics.skill.id)}>Equip ▸</button>
					<div class="d-hint">{view.cap - view.equipped.length} slot(s) free</div>
				{/if}
			</div>
		</div>

		<div class="bignums">
			<div class="stat">
				<span class="v accent">{fmt(view.effective(metrics.skill.id))}</span><span class="k">effective dmg</span>
			</div>
			<div class="stat"><span class="v">{metrics.cooldown.toFixed(1)}s</span><span class="k">cooldown</span></div>
			<div class="stat">
				<span class="v accent">{fmt(view.effectiveDps(metrics.skill.id))}</span><span class="k">effective dps</span>
			</div>
			<div class="stat"><span class="v">{castsPer10s}</span><span class="k">casts / 10s</span></div>
		</div>
		<div class="rawnote">{rawNote}</div>

		<div class="d-sec">
			<div class="label">Damage breakdown</div>
			{#if metrics.contributions.length}
				{#each metrics.contributions as contribution (contribution.attributeId)}
					<div class="scale" style:--ac={attributeColor(contribution.attributeId)}>
						<span class="achip">
							<AttributeIcon id={contribution.attributeId} size={12} />
							{attributeCode(contribution.attributeId, staticData.attributes)}
						</span>
						<div class="bar"><i style:width="{Math.round((contribution.value / maxContribution) * 100)}%"></i></div>
						<span class="contrib"
							>{attributeName(contribution.attributeId, staticData.attributes)} ×{contribution.multiplier} = +{fmt(
								contribution.value
							)}</span
						>
					</div>
				{/each}
			{:else}
				<div class="d-desc">No attribute scaling.</div>
			{/if}
			<div class="brk-line def">
				<span class="brk-k">Enemy defense</span><span class="brk-v">−{fmt(view.appliedDefense(metrics.skill.id))}</span>
			</div>
			<div class="brk-line total">
				<span class="brk-k">Effective hit</span><span class="brk-v">{fmt(view.effective(metrics.skill.id))}</span>
			</div>
		</div>

		{#if effects.length}
			<div class="d-sec">
				<div class="label">Effects</div>
				{#each effects as effect (effect.id)}
					<div class="effect-row">
						<span class="emag" style:color={effectDirectionColor(effect.direction)}>{effect.magnitude}</span>
						<span class="eattr">{effect.attributeName}</span>
						<span class="emeta">{effect.targetLabel} · {effect.duration}</span>
					</div>
				{/each}
			</div>
		{/if}

		<div class="d-foot">
			<div class="foot-item">
				<div class="fl">Scaling</div>
				<div class="fv">{scalingSummary}</div>
			</div>
			<div class="foot-item">
				<div class="fl">Source</div>
				<div class="fv">{sourceSummary}</div>
			</div>
			<div class="foot-item">
				<div class="fl">Status</div>
				<div class="fv">{statusSummary}</div>
			</div>
		</div>
	{/if}
</div>

<script lang="ts">
import {
	attributeCode,
	attributeColor,
	attributeIsHarmful,
	attributeName,
	describeEffect,
	effectDirectionColor,
	formatNum
} from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import SkillIcon from './SkillIcon.svelte';
import type { SkillMetrics, SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
};

const { view }: Props = $props();

const metrics = $derived<SkillMetrics | undefined>(view.selected);
const equipped = $derived(metrics ? view.isEquipped(metrics.skill.id) : false);
const pending = $derived(metrics ? view.pendingSwap === metrics.skill.id : false);
const full = $derived(view.equipped.length >= view.cap);

const fmt = (n: number) => formatNum(Math.round(n));

// One display description per authored effect, reusing the shared helper so wording/direction
// match the battle tooltip's "On hit" lines; `id` is kept for a stable each-key.
const effects = $derived(
	(metrics?.skill.effects ?? []).map((effect) => ({
		id: effect.id,
		...describeEffect(
			effect,
			attributeName(effect.attributeId, staticData.attributes),
			attributeIsHarmful(effect.attributeId, staticData.attributes)
		)
	}))
);

const scalesEyebrow = $derived(
	metrics?.skill.damageMultipliers.length
		? 'Scales ' +
				metrics.skill.damageMultipliers.map((m) => attributeCode(m.attributeId, staticData.attributes)).join(' · ')
		: 'Active skill'
);
const scalingSummary = $derived(
	metrics?.skill.damageMultipliers.length
		? metrics.skill.damageMultipliers.map((m) => attributeCode(m.attributeId, staticData.attributes)).join(' · ')
		: '—'
);
const sourceSummary = $derived(
	metrics
		? metrics.unlocked
			? '✓ ' + (metrics.source?.name ?? 'Starting skill')
			: '🔒 ' + (metrics.source?.name ?? 'Locked')
		: ''
);
const statusSummary = $derived(
	metrics
		? equipped
			? 'Equipped · slot ' + view.slotOf(metrics.skill.id)
			: metrics.unlocked
				? 'Available'
				: 'Locked'
		: ''
);

const castsPer10s = $derived(metrics && metrics.cooldown > 0 ? (10 / metrics.cooldown).toFixed(1) : '—');
const maxContribution = $derived(
	metrics ? Math.max(metrics.skill.baseDamage, ...metrics.contributions.map((c) => c.value), 1) : 1
);
const rawNote = $derived.by(() => {
	if (!metrics) {
		return '';
	}
	const raw = fmt(view.rawDamage(metrics.skill.id));
	if (view.defense <= 0) {
		return `${raw} raw damage · target has no defense`;
	}
	const applied = fmt(view.appliedDefense(metrics.skill.id));
	if (view.effective(metrics.skill.id) === 0) {
		return `${raw} raw − ${applied} def · fully blocked`;
	}
	return `${raw} raw − ${applied} def`;
});
</script>

<style lang="scss">
.detail {
	display: flex;
	flex-direction: column;
	padding: 18px 22px;
	background: color-mix(in srgb, var(--white) 2.5%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	overflow-y: auto;
}

.d-top {
	display: flex;
	gap: 15px;
	align-items: flex-start;
}

.d-ico-wrap {
	position: relative;

	.slotbadge {
		position: absolute;
		top: -6px;
		left: -6px;
		display: flex;
		align-items: center;
		justify-content: center;
		width: 17px;
		height: 17px;
		border-radius: 3px;
		font-family: var(--mono);
		font-size: 9px;
		color: var(--text-on-accent);
		background: var(--accent);
	}
}

.d-headings {
	flex: 1;
	min-width: 0;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.d-name {
	margin-top: 2px;
	font-size: 25px;
	font-weight: 500;
	letter-spacing: -0.4px;
	line-height: 1.05;
}

.d-desc {
	margin-top: 3px;
	font-size: 13.5px;
	color: var(--text-tertiary);
}

.d-cta {
	display: flex;
	flex-direction: column;
	gap: 6px;
	align-items: flex-end;
	flex-shrink: 0;
}

.d-hint {
	max-width: 170px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.4px;
	line-height: 1.5;
	text-align: right;
	color: var(--text-muted);

	b {
		color: var(--text-secondary);
	}
}

.btn {
	padding: 8px 16px;
	border: 1px solid var(--accent);
	border-radius: 3px;
	background: var(--accent);
	color: var(--text-on-accent);
	font-family: var(--sans);
	font-size: 13px;
	font-weight: 500;
	white-space: nowrap;
	cursor: pointer;

	&.ghost {
		background: transparent;
		color: var(--text-primary);
		border-color: var(--border-medium);
	}

	&.danger {
		background: color-mix(in srgb, var(--enemy-accent) 16%, transparent);
		color: var(--enemy-accent);
		border-color: color-mix(in srgb, var(--enemy-accent) 55%, transparent);
	}

	&.dim {
		background: transparent;
		color: var(--text-muted);
		border-color: var(--border-light);
		cursor: not-allowed;
	}
}

.bignums {
	display: flex;
	gap: 30px;
	margin-top: 16px;
	padding-top: 14px;
	border-top: 1px solid var(--border-subtle);
}

.stat {
	display: flex;
	flex-direction: column;
	gap: 2px;

	.v {
		font-size: 25px;
		font-weight: 500;
		line-height: 1;

		&.accent {
			color: var(--accent);
		}
	}

	.k {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 1.2px;
		text-transform: uppercase;
		color: var(--text-muted);
	}
}

.rawnote {
	margin-top: 8px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.3px;
	color: var(--text-muted);
}

.d-sec {
	margin-top: 16px;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.scale {
	display: flex;
	align-items: center;
	gap: 11px;
	margin-top: 8px;
}

.achip {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	gap: 4px;
	min-width: 46px;
	padding: 2px 8px;
	border: 1px solid color-mix(in srgb, var(--ac) 38%, transparent);
	border-radius: 3px;
	background: color-mix(in srgb, var(--ac) 12%, transparent);
	color: var(--ac);
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	text-align: center;
	white-space: nowrap;
}

.bar {
	flex: 1;
	height: 6px;
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 7%, transparent);
	overflow: hidden;

	i {
		display: block;
		height: 100%;
		border-radius: 4px;
		background: var(--ac);
	}
}

.contrib {
	min-width: 128px;
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-tertiary);
	white-space: nowrap;
}

.brk-line {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	padding-top: 7px;
	font-size: 12px;

	.brk-k {
		color: var(--text-secondary);
	}

	.brk-v {
		font-family: var(--mono);
		font-size: 11.5px;
	}

	&.def {
		.brk-k {
			color: var(--enemy-accent);
		}

		.brk-v {
			color: var(--error);
		}
	}

	&.total {
		margin-top: 6px;
		padding-top: 8px;
		border-top: 1px solid color-mix(in srgb, var(--text-primary) 10%, transparent);
		font-weight: 600;

		.brk-v {
			color: var(--accent);
			font-size: 13px;
		}
	}
}

.effect-row {
	display: flex;
	align-items: baseline;
	gap: 11px;
	margin-top: 8px;
	font-size: 12px;
}

.emag {
	min-width: 46px;
	font-family: var(--mono);
	font-size: 11px;
	font-weight: 600;
	text-align: right;
}

.eattr {
	color: var(--text-secondary);
}

.emeta {
	margin-left: auto;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.4px;
	color: var(--text-muted);
	white-space: nowrap;
}

.d-foot {
	display: flex;
	gap: 30px;
	margin-top: 16px;
	padding-top: 13px;
	border-top: 1px solid var(--border-subtle);
}

.foot-item {
	.fl {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 1.4px;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.fv {
		margin-top: 3px;
		font-size: 13px;
		color: var(--text-secondary);
	}
}
</style>
