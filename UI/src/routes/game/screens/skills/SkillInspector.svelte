<div class="detail">
	{#if metrics}
		<div class="d-top">
			<div class="d-ico-wrap">
				<SkillIcon skill={metrics.skill} size={78} />
				{#if equipped}
					<span class="slotbadge">{view.slotOf(metrics.skill.id)}</span>
				{/if}
			</div>
			<div class="d-headings">
				<div class="eyebrow-row">
					<span class="eyebrow">{scalesEyebrow}</span>
					<RarityTagBox color={rarityAccent} label={rarityName} />
				</div>
				<div class="d-name">{metrics.skill.name}</div>
				<div class="d-desc">{metrics.skill.description}</div>
			</div>
			<SkillCta {view} {metrics} {equipped} {pending} {full} />
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

		<SkillDamageBreakdown {view} {metrics} />

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
				<div class="fl">Status</div>
				<div class="fv">{statusSummary}</div>
			</div>
		</div>
	{/if}
</div>

<script lang="ts">
import {
	attributeCode,
	attributeIsHarmful,
	attributeName,
	describeEffect,
	effectDirectionColor,
	formatNum,
	rarityColor,
	rarityLabel
} from '$lib/common';
import { staticData } from '$stores';
import SkillIcon from './SkillIcon.svelte';
import SkillCta from './SkillCta.svelte';
import SkillDamageBreakdown from './SkillDamageBreakdown.svelte';
import RarityTagBox from '$components/RarityTagBox.svelte';
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

// The skill's rarity tier, surfaced as a tinted tag beside the eyebrow.
const rarityAccent = $derived(metrics ? rarityColor(metrics.skill.rarityId) : 'var(--rarity-common)');
const rarityName = $derived(metrics ? rarityLabel(metrics.skill.rarityId) : '');

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
const statusSummary = $derived(
	metrics ? (equipped ? 'Equipped · slot ' + view.slotOf(metrics.skill.id) : 'Available') : ''
);

const castsPer10s = $derived(metrics && metrics.cooldown > 0 ? (10 / metrics.cooldown).toFixed(1) : '—');
const rawNote = $derived.by(() => {
	if (!metrics) {
		return '';
	}
	const id = metrics.skill.id;
	const raw = fmt(view.rawDamage(id));
	// Surface the expected crit contribution inline so the note reconciles with the breakdown's
	// Critical row and the crit-inclusive effective hit; omitted when no crit can occur.
	const critBonus = view.critBonus(id);
	const critPart = critBonus > 0 ? ` · +${fmt(critBonus)} crit` : '';
	if (view.toughness <= 0) {
		return `${raw} raw damage${critPart} · target has no toughness`;
	}
	const mitigated = fmt(view.mitigatedAmount(id));
	return `${raw} raw${critPart} − ${mitigated} mitigated`;
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

.eyebrow-row {
	display: flex;
	align-items: center;
	gap: 9px;
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
