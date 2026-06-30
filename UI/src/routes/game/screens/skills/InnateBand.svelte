<!-- Read-only band of skills granted by equipped gear (innate, always-active). Distinct from the
     editable EquippedBand: these can't be selected, removed, or reordered — they follow the gear. A grant
     that duplicates a selected skill (or another grant) is surfaced as "already in your loadout" so the
     de-duplicated copy isn't invisibly dropped. -->
{#if view.innateSkills.length}
	<div class="innate-head">
		<span class="label">Innate — granted by equipped gear · always active, outside the loadout cap</span>
	</div>

	<div class="band" style:--cap={view.cap}>
		{#each view.innateSkills as innate, index (`${innate.skill.id}-${index}`)}
			{@const metrics = view.metric(innate.skill.id)}
			{@const dormant = !innate.duplicate && view.dormant(innate.skill)}
			<div class="eqcard" class:dupe={innate.duplicate} class:dormant data-testid="innate-card">
				<span class="src" title="Granted by {innate.sourceItemName}">⟡ {innate.sourceItemName}</span>
				<span class="en">{innate.skill.name}</span>
				{#if innate.duplicate}
					<span class="dupe-note">already in your loadout</span>
				{:else if dormant}
					<!-- A granted weapon-typed skill is gated like a selected one (#1342). -->
					<span
						class="dormant-note"
						title="Off-weapon — dormant until you equip a {requiredWeapon(innate.skill)} weapon"
					>
						⊘ dormant · needs {requiredWeapon(innate.skill)}
					</span>
				{:else if metrics}
					<SkillCardStats {view} {metrics} />
				{/if}
			</div>
		{/each}
	</div>
{/if}

<script lang="ts">
import SkillCardStats from './SkillCardStats.svelte';
import type { ISkill } from '$lib/api';
import { primaryDamageType } from '$lib/battle';
import { damageTypeName } from '$lib/common';
import type { SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
};

const { view }: Props = $props();

/** The weapon type a dormant (off-weapon) granted skill needs equipped to field — its primary leaf type. */
const requiredWeapon = (skill: ISkill): string => damageTypeName(primaryDamageType(skill.damagePortions));
</script>

<style lang="scss">
.innate-head {
	display: flex;
	align-items: center;
	gap: 12px;
	margin: 14px 0 9px;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.band {
	display: grid;
	grid-template-columns: repeat(var(--cap), 1fr);
	gap: 11px;
}

.eqcard {
	position: relative;
	display: flex;
	flex-direction: column;
	gap: 7px;
	min-height: 104px;
	padding: 10px 11px;
	// Dashed border + muted tint signals "read-only / not part of the editable loadout".
	border: 1px dashed var(--border-medium);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 2%, transparent);
	text-align: left;
	color: var(--text-primary);

	&.dupe {
		opacity: 0.7;
	}

	// Off-weapon (dormant): a granted weapon-typed skill the held weapon doesn't field.
	&.dormant {
		opacity: 0.55;
	}
}

.dormant-note {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	// Neutral muted tone (matching the dupe-note); --warning is reserved for validation.
	color: var(--text-tertiary);
}

.src {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.en {
	font-size: 13px;
	font-weight: 500;
	line-height: 1.1;
}

.dupe-note {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}
</style>
