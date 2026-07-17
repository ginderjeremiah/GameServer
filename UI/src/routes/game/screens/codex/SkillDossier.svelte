<!-- The skill dossier (right panel): the header carries a rarity-tinted accent border + tier tag
     alongside the intellect "Skill" kind mark (mirroring items: rarity on the border, kind on the
     label), base damage / cooldown meta, how to obtain the skill (item grants, or the enemy-only /
     not-obtainable wording), the attributes the skill scales with, its authored effects (wording
     from the shared skill-effect-display helper), and the enemies that use it. The "used by" pills
     cross-link into the enemy dossier. A reference card only — no equip/loadout, no DPS. -->
{#if view.skillsTab.selectedSkill}
	{@const skill = view.skillsTab.selectedSkill}
	{@const rarity = view.skillsTab.selectedSkillRarity}
	<DossierShell
		selectedItem={skill}
		testid="codex-skill-dossier"
		accent="var(--attr-intellect)"
		borderAccent={rarity?.color}
		kind="Skill"
		name={skill.name}
		description={skill.description}
	>
		{#snippet headExtra()}
			{#if rarity}
				<RarityTagBox color={rarity.color} label={rarity.label} />
			{/if}
		{/snippet}

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
				<div class="section-label">How to obtain</div>
				{#if view.skillsTab.skillProvenance.sources.length > 0}
					<div class="sources">
						{#each view.skillsTab.skillProvenance.sources as source (source.kind + '-' + source.id)}
							<div
								class="source"
								style:--src={source.accent}
								data-testid="codex-skill-source-{source.kind}-{source.id}"
							>
								<span class="src-kind">{source.label}</span>
								<span class="src-name">{source.name}</span>
							</div>
						{/each}
					</div>
				{:else}
					<div class="empty">{view.skillsTab.skillProvenance.emptyLabel}</div>
				{/if}
			</div>

			<div class="section">
				<div class="section-label">Scales with</div>
				{#if view.skillsTab.skillScaling.length > 0}
					<div class="chips">
						{#each view.skillsTab.skillScaling as scale (scale.attributeId)}
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

			{#if view.skillsTab.skillEffects.length > 0}
				<div class="section">
					<div class="section-label">Effects</div>
					<div class="effects">
						{#each view.skillsTab.skillEffects as effect (effect.id)}
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
				<div class="section-label">Used by · {view.skillsTab.skillUsedBy.length}</div>
				{#if view.skillsTab.skillUsedBy.length > 0}
					<div class="pills">
						{#each view.skillsTab.skillUsedBy as user (user.enemyId)}
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
					stats={view.skillsTab.skillStatistics}
					loading={view.statsLoading}
					error={view.statsError}
					emptyMessage="No statistics recorded for this skill yet."
					testid="codex-skill-stats"
				/>
			</div>
		</div>
	</DossierShell>
{/if}

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { formatBaseDamage, formatCooldown } from './codex-display';
import DossierShell from './DossierShell.svelte';
import StatisticsPanel from './StatisticsPanel.svelte';
import RarityTagBox from '$components/RarityTagBox.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
@use '$styles/codex-dossier' as dossier;

.body {
	@include dossier.stacked-body;
}

.meta {
	@include dossier.meta-row;
}

.meta-card {
	@include dossier.meta-card;
}

.meta-label {
	@include dossier.meta-label;
}

.meta-val {
	@include dossier.meta-val;
}

.section-label {
	@include dossier.section-label;
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

.sources {
	display: flex;
	flex-direction: column;
	gap: 8px;
}

.source {
	display: flex;
	align-items: baseline;
	gap: 9px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-left: 2px solid var(--src);
	border-radius: 5px;
	padding: 8px 11px;
}

.src-kind {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
	flex: none;
}

.src-name {
	font-size: 12.5px;
	color: var(--text-primary);
	flex: 1;
	min-width: 0;
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
	@include dossier.empty-note;
}
</style>
