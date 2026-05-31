<div
	class="skill-tooltip"
	bind:this={container}
	style={skill ? '' : 'display: none;'}
	style:border-left="3px solid var(--accent)"
>
	{#if skill}
		<!-- Title section -->
		<div class="tt-title-section">
			<div class="tt-category-row">
				<div class="tt-category-diamond"></div>
				<span class="tt-category-label">Skill</span>
				<div class="tt-cooldown-pill">
					<div class="tt-cooldown-fill" style:width="{cooldownProgress}%" class:ready={isReady}></div>
					<span class="tt-cooldown-text" class:ready={isReady}>
						{isReady ? 'READY' : `${remainingCdFormatted}s`}
					</span>
				</div>
			</div>
			<div class="tt-skill-name">{skill.name}</div>
		</div>

		<div class="tt-body">
			<!-- Damage breakdown -->
			<div class="tt-section">
				<div class="tt-section-header">
					<span>Damage breakdown</span>
					<div class="tt-section-line"></div>
				</div>
				<div class="tt-dmg-list">
					<div class="tt-dmg-row">
						<span class="tt-dmg-label">Base</span>
						<span class="tt-dmg-value">{baseDamage}</span>
					</div>
					{#each multipliers as mult (mult.attributeId)}
						<div class="tt-dmg-row">
							<span class="tt-dmg-label">
								{attributeName(mult.attributeId)}
								<span class="tt-mult-dim">x{mult.multiplier}</span>
							</span>
							<span class="tt-dmg-value positive">+{formatNum(getMultiplier(mult))}</span>
						</div>
					{/each}
					{#if opponent}
						<div class="tt-dmg-row">
							<span class="tt-dmg-label">Enemy defense</span>
							<span class="tt-dmg-value negative">-{formatNum(enemyDefense)}</span>
						</div>
					{/if}
					<div class="tt-dmg-total">
						<span>Total</span>
						<span class="tt-total-value">{formatNum(total)}</span>
					</div>
				</div>
			</div>

			<!-- Tempo -->
			<div class="tt-section">
				<div class="tt-section-header">
					<span>Tempo</span>
					<div class="tt-section-line"></div>
				</div>
				<div class="tt-metrics">
					<div class="tt-metric">
						<div class="tt-metric-label">Cooldown</div>
						<div class="tt-metric-value">{formatNum(adjustedCd)}s</div>
					</div>
					<div class="tt-metric">
						<div class="tt-metric-label">DPS</div>
						<div class="tt-metric-value">{formatNum(total / adjustedCd)}</div>
					</div>
				</div>
			</div>
		</div>
	{/if}
</div>

<script lang="ts">
import { EAttribute, type IAttributeMultiplier } from '$lib/api';
import { type Skill } from '$lib/battle';
import { formatNum } from '$lib/common';
import { battleEngine } from '$lib/engine';
import { staticData } from '$stores';

export const getBaseNode = () => container;

type Props = {
	skill: Skill | undefined;
};

const { skill }: Props = $props();

let container: HTMLDivElement;

const opponent = $derived(skill?.owner ? battleEngine.getOpponent(skill.owner) : undefined);

const baseDamage = $derived(skill?.baseDamage ?? 0);
const multipliers = $derived(skill?.damageMultipliers ?? []);
const totalDamage = $derived(baseDamage + multipliers.reduce((a, b) => a + getMultiplier(b), 0));
const enemyDefense = $derived(opponent?.attributes.getValue(EAttribute.Defense) ?? 0);
const total = $derived(Math.max(totalDamage - enemyDefense, 0));
const cdMultiplier = $derived(skill?.owner.cdMultiplier ?? 1);
const adjustedCd = $derived((skill?.cooldownMs ?? 0) / 1000 / cdMultiplier);
const remainingCd = $derived(
	Math.abs(adjustedCd - (cdMultiplier + (skill?.renderChargeTime ?? 0)) / 1000 / cdMultiplier)
);
const isReady = $derived(remainingCd <= 0.01);
const cooldownProgress = $derived(isReady ? 100 : Math.max(0, ((adjustedCd - remainingCd) / adjustedCd) * 100));
const remainingCdFormatted = $derived(formatNum(remainingCd));

const getMultiplier = (mult: IAttributeMultiplier) => {
	return (skill?.owner.attributes.getValue(mult.attributeId) ?? 0) * mult.multiplier;
};

const attributeName = (attrId: number) => {
	return staticData.attributes?.[attrId]?.name ?? EAttribute[attrId] ?? 'Unknown';
};
</script>

<style lang="scss">
.skill-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px rgba(161, 194, 247, 0.13);
}

.tt-title-section {
	padding: 14px 16px 12px;
	border-bottom: 1px solid rgba(240, 240, 240, 0.08);
}

.tt-category-row {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}

.tt-category-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 6px rgba(161, 194, 247, 0.68);
}

.tt-category-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--accent);
}

.tt-cooldown-pill {
	margin-left: auto;
	position: relative;
	width: 60px;
	height: 18px;
	background: rgba(240, 240, 240, 0.06);
	border: 1px solid rgba(255, 255, 255, 0.14);
	border-radius: 2px;
	overflow: hidden;
}

.tt-cooldown-fill {
	position: absolute;
	inset: 0;
	background: linear-gradient(90deg, rgba(161, 194, 247, 0.5), rgba(161, 194, 247, 0.18));

	&.ready {
		background: linear-gradient(90deg, rgba(189, 224, 180, 0.55), rgba(189, 224, 180, 0.2));
	}
}

.tt-cooldown-text {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	color: #c0d8ff;
	letter-spacing: 0.6px;

	&.ready {
		color: #bde0b4;
	}
}

.tt-skill-name {
	font-size: 18px;
	font-weight: 400;
	color: #f0f0f0;
	letter-spacing: -0.2px;
}

.tt-body {
	padding: 12px 16px 14px;
}

.tt-section {
	margin-bottom: 12px;

	&:last-child {
		margin-bottom: 0;
	}
}

.tt-section-header {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);
	margin-bottom: 7px;
	display: flex;
	align-items: center;
	gap: 8px;
}

.tt-section-line {
	flex: 1;
	height: 1px;
	background: rgba(240, 240, 240, 0.06);
}

.tt-dmg-list {
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.tt-dmg-row {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	font-size: 11.5px;
}

.tt-dmg-label {
	color: rgba(240, 240, 240, 0.78);
}

.tt-mult-dim {
	color: rgba(240, 240, 240, 0.45);
}

.tt-dmg-value {
	font-family: 'Geist Mono', monospace;
	font-size: 11.5px;
	color: #f0f0f0;
	letter-spacing: 0.3px;

	&.positive {
		color: #bde0b4;
	}
	&.negative {
		color: #f0a094;
	}
}

.tt-dmg-total {
	margin-top: 4px;
	padding-top: 6px;
	border-top: 1px solid rgba(240, 240, 240, 0.1);
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	font-size: 13px;
	font-weight: 600;
	color: #f0f0f0;
}

.tt-total-value {
	font-family: 'Geist Mono', monospace;
	color: var(--accent);
	letter-spacing: 0.3px;
}

.tt-metrics {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 10px;
}

.tt-metric {
	padding: 7px 10px;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 2px;
}

.tt-metric-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 1.3px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.5);
	margin-bottom: 2px;
}

.tt-metric-value {
	font-family: 'Geist Mono', monospace;
	font-size: 14px;
	color: #f0f0f0;
	letter-spacing: 0.3px;
}
</style>
