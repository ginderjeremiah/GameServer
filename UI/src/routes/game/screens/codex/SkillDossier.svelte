<!-- The skill dossier (right panel): the header carries a rarity-tinted accent border + tier tag
     alongside the intellect "Skill" kind mark (mirroring items: rarity on the border, kind on the
     label), base damage / cooldown meta, the attributes the skill scales with, its authored effects
     (wording from the shared skill-effect-display helper), and the enemies that use it. The "used by"
     pills cross-link into the enemy dossier. A reference card only — no equip/loadout, no DPS. -->
{#if view.selectedSkill}
	{@const skill = view.selectedSkill}
	{@const rarity = view.selectedSkillRarity}
	<div class="dossier" data-testid="codex-skill-dossier">
		<div class="head" style:border-left-color={rarity?.color}>
			<div class="kind-line">
				<span class="kind-dot"></span>
				<span class="kind">Skill</span>
				{#if rarity}
					<span class="rarity-tag" style:--rarity={rarity.color}>{rarity.label}</span>
				{/if}
			</div>
			<div class="name">{skill.name}</div>
			{#if skill.description}
				<div class="desc">{skill.description}</div>
			{/if}
		</div>

		<div class="body">
			<div class="meta">
				<div class="meta-card">
					<div class="meta-label">Base Damage</div>
					<div class="meta-val">{formatBaseDamage(skill.baseDamage)}</div>
				</div>
				<div class="meta-card">
					<div class="meta-label">Cooldown</div>
					<div class="meta-val">{formatCooldown(skill.cooldownMs)}</div>
				</div>
			</div>

			<div class="section">
				<div class="section-label">Scales with</div>
				{#if view.skillScaling.length > 0}
					<div class="chips">
						{#each view.skillScaling as scale (scale.attributeId)}
							<span class="scale-chip" style:--chip={scale.color}>
								<span class="mult">{scale.multiplierLabel}</span>
								<span class="attr">{scale.name}</span>
							</span>
						{/each}
					</div>
				{:else}
					<div class="empty">No attribute scaling — flat base damage</div>
				{/if}
			</div>

			{#if view.skillEffects.length > 0}
				<div class="section">
					<div class="section-label">Effects</div>
					<div class="effects">
						{#each view.skillEffects as effect (effect.id)}
							<div class="effect-row">
								<span class="emag" style:color={effect.color}>{effect.magnitude}</span>
								<span class="eattr">{effect.attributeName}</span>
								<span class="emeta">{effect.targetLabel} · {effect.duration}</span>
							</div>
						{/each}
					</div>
				</div>
			{/if}

			<div class="section">
				<div class="section-label">Used by · {view.skillUsedBy.length}</div>
				{#if view.skillUsedBy.length > 0}
					<div class="pills">
						{#each view.skillUsedBy as user (user.enemyId)}
							<button
								type="button"
								class="pill"
								style:--pill={user.accent}
								data-testid="codex-skill-usedby-{user.enemyId}"
								onclick={() => view.openEnemy(user.enemyId)}
							>
								<span class="pill-mark" class:boss={user.isBoss}></span>
								<span class="pill-name">{user.name}</span>
							</button>
						{/each}
					</div>
				{:else}
					<div class="empty">Player skill — no enemies use it</div>
				{/if}
			</div>

			<div class="section">
				<StatisticsPanel
					stats={view.skillStatistics}
					loading={view.statsLoading}
					error={view.statsError}
					emptyMessage="No statistics recorded for this skill yet."
					testid="codex-skill-stats"
				/>
			</div>
		</div>
	</div>
{/if}

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { formatBaseDamage, formatCooldown } from './codex-display';
import StatisticsPanel from './StatisticsPanel.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.dossier {
	width: 380px;
	flex: none;
	background: var(--surface);
	border-left: 1px solid var(--border-subtle);
	display: flex;
	flex-direction: column;
}

.head {
	border-left: 3px solid var(--attr-intellect);
	padding: 18px 20px 14px;
	flex: none;
}

.kind-line {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 6px;
}

.kind-dot {
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--attr-intellect);
	flex: none;
}

.kind {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--attr-intellect);
}

.rarity-tag {
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

.name {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
	line-height: 1.05;
}

.desc {
	margin-top: 7px;
	font-size: 12px;
	line-height: 1.5;
	color: var(--text-tertiary);
}

.body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px 20px 20px;
	display: flex;
	flex-direction: column;
	gap: 18px;
}

.meta {
	display: flex;
	gap: 10px;
}

.meta-card {
	flex: 1;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 9px 11px;
}

.meta-label {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.meta-val {
	font-family: var(--mono);
	font-size: 15px;
	font-weight: 500;
	margin-top: 3px;
	color: var(--text-primary);
}

.section-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 10px;
}

.chips {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}

.scale-chip {
	display: inline-flex;
	align-items: baseline;
	gap: 6px;
	background: color-mix(in srgb, var(--chip) 12%, transparent);
	border: 1px solid color-mix(in srgb, var(--chip) 40%, transparent);
	border-radius: 4px;
	padding: 4px 9px;
}

.mult {
	font-family: var(--mono);
	font-size: 11px;
	font-weight: 600;
	color: var(--chip);
}

.attr {
	font-size: 12px;
	color: var(--text-secondary);
}

.effects {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.effect-row {
	display: flex;
	align-items: baseline;
	gap: 9px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	padding: 8px 11px;
}

.emag {
	font-family: var(--mono);
	font-size: 12px;
	font-weight: 600;
	flex: none;
}

.eattr {
	font-size: 12.5px;
	color: var(--text-primary);
	flex: 1;
	min-width: 0;
}

.emeta {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
	white-space: nowrap;
	flex: none;
}

.pills {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}

.pill {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-left: 2px solid var(--pill);
	border-radius: 5px;
	padding: 6px 11px;
	cursor: pointer;
	font-family: var(--sans);

	&:hover {
		background: color-mix(in srgb, var(--pill) 10%, var(--panel));
	}
}

.pill-mark {
	width: 7px;
	height: 7px;
	flex: none;
	border-radius: 50%;
	background: var(--pill);

	// A boss reads as a diamond, mirroring the enemy table's mark language.
	&.boss {
		border-radius: 0;
		transform: rotate(45deg);
		box-shadow: 0 0 6px color-mix(in srgb, var(--pill) 50%, transparent);
	}
}

.pill-name {
	font-size: 12.5px;
	color: var(--text-primary);
}

.empty {
	font-size: 12px;
	color: var(--text-muted);
	font-style: italic;
}
</style>
