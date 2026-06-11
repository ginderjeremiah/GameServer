<!--
	Reward section: grant an item, a mod, and/or a skill, with hard exclusivity across
	every challenge in the catalogue (a reward unlocked elsewhere can't be re-assigned).
	The current sibling challenges are read live from the store so exclusivity
	reflects unsaved edits too.
-->
<div>
	<div class="ch-reward-intro">
		<WorkbenchIcon kind="gift" size={13} stroke="var(--text-tertiary)" />
		<span>
			Grant an item, a mod, and/or a skill. Each unlock belongs to exactly one challenge — already-claimed rewards are
			disabled below.
		</span>
	</div>

	<div class="ch-reward-grid">
		<RewardSlot
			kind="item"
			label="Item Reward"
			valueId={challenge.rewardItemId}
			name={challenge.rewardItemId != null ? reference.itemRecName(challenge.rewardItemId) : undefined}
			sub={challenge.rewardItemId != null
				? reference.rarityName(reference.itemRarityId(challenge.rewardItemId) ?? ERarity.Common)
				: ''}
			color={challenge.rewardItemId != null
				? reference.rarityColor(reference.itemRarityId(challenge.rewardItemId) ?? ERarity.Common)
				: null}
			retired={challenge.rewardItemId != null && reference.itemRetired(challenge.rewardItemId)}
			dirty={itemDirty}
			open={open === 'item'}
			onClear={() => setItem(undefined)}
			onOpen={() => (open = open === 'item' ? null : 'item')}
		/>
		<RewardSlot
			kind="mod"
			label="Item Mod Reward"
			valueId={challenge.rewardItemModId}
			name={challenge.rewardItemModId != null ? reference.itemModName(challenge.rewardItemModId) : undefined}
			sub={challenge.rewardItemModId != null ? (reference.itemModTypeName(challenge.rewardItemModId) ?? '') : ''}
			color={challenge.rewardItemModId != null ? 'var(--accent)' : null}
			retired={challenge.rewardItemModId != null && reference.itemModRetired(challenge.rewardItemModId)}
			dirty={modDirty}
			open={open === 'mod'}
			onClear={() => setMod(undefined)}
			onOpen={() => (open = open === 'mod' ? null : 'mod')}
		/>
		<RewardSlot
			kind="skill"
			label="Skill Reward"
			valueId={challenge.rewardSkillId}
			name={challenge.rewardSkillId != null ? reference.skillName(challenge.rewardSkillId) : undefined}
			sub={challenge.rewardSkillId != null ? `${reference.skillBaseDamage(challenge.rewardSkillId) ?? 0} dmg` : ''}
			color={challenge.rewardSkillId != null ? 'var(--accent)' : null}
			retired={challenge.rewardSkillId != null && reference.skillRetired(challenge.rewardSkillId)}
			dirty={skillDirty}
			open={open === 'skill'}
			onClear={() => setSkill(undefined)}
			onOpen={() => (open = open === 'skill' ? null : 'skill')}
		/>
	</div>

	{#if open === 'item'}
		<RewardPicker
			kind="item"
			records={itemPickRecords}
			currentId={challenge.rewardItemId}
			claimed={claimedItems}
			onPick={setItem}
			onClose={() => (open = null)}
		/>
	{:else if open === 'mod'}
		<RewardPicker
			kind="mod"
			records={modPickRecords}
			currentId={challenge.rewardItemModId}
			claimed={claimedMods}
			onPick={setMod}
			onClose={() => (open = null)}
		/>
	{:else if open === 'skill'}
		<RewardPicker
			kind="skill"
			records={skillPickRecords}
			currentId={challenge.rewardSkillId}
			claimed={claimedSkills}
			onPick={setSkill}
			onClose={() => (open = null)}
		/>
	{/if}

	{#if none}
		<div class="ch-reward-warn">
			<WorkbenchIcon kind="warn" size={12} sw={1.4} stroke="var(--warning)" />
			This challenge unlocks nothing — players get no reward for completing it.
		</div>
	{/if}
</div>

<script lang="ts">
import { ERarity, type IChallenge } from '$lib/api';
import { reference } from '../../reference.svelte';
import type { EntityStore } from '../../entity-store.svelte';
import type { Identified } from '../../entities/types';
import { claimedItemMap, claimedModMap, claimedSkillMap } from '../../entities/challenge-helpers';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';
import RewardSlot from './RewardSlot.svelte';
import RewardPicker from './RewardPicker.svelte';

interface Props {
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { record, baseline, store }: Props = $props();

const challenge = $derived(record as unknown as IChallenge);
const base = $derived(baseline as unknown as IChallenge | undefined);

let open = $state<'item' | 'mod' | 'skill' | null>(null);
// Collapse any open picker when switching to a different record.
let openForId = $state<number>();
$effect(() => {
	if (openForId !== challenge.id) {
		openForId = challenge.id;
		open = null;
	}
});

const liveChallenges = $derived(store.items.filter((it) => store.status(it) !== 'deleted') as unknown as IChallenge[]);
const claimedItems = $derived(claimedItemMap(liveChallenges, challenge.id));
const claimedMods = $derived(claimedModMap(liveChallenges, challenge.id));
const claimedSkills = $derived(claimedSkillMap(liveChallenges, challenge.id));

const itemDirty = $derived(base ? challenge.rewardItemId !== base.rewardItemId : false);
const modDirty = $derived(base ? challenge.rewardItemModId !== base.rewardItemModId : false);
const skillDirty = $derived(base ? challenge.rewardSkillId !== base.rewardSkillId : false);
const none = $derived(
	challenge.rewardItemId == null && challenge.rewardItemModId == null && challenge.rewardSkillId == null
);

// Retired records drop out of the pick list (can't be newly granted) unless they're the
// challenge's current reward, which stays visible (marked retired) so it isn't silently lost.
const keepActive = <T extends { id: number; retiredAt?: string | null }>(records: T[], currentId: number | undefined) =>
	records.filter((r) => !r.retiredAt || r.id === currentId);

const itemPickRecords = $derived(
	keepActive(reference.itemRecords(), challenge.rewardItemId).map((i) => ({
		id: i.id,
		name: i.name,
		color: reference.rarityColor(i.rarityId),
		tag: reference.rarityName(i.rarityId),
		retired: !!i.retiredAt
	}))
);
const modPickRecords = $derived(
	keepActive(reference.itemModRecords(), challenge.rewardItemModId).map((m) => ({
		id: m.id,
		name: m.name,
		color: 'var(--accent)',
		tag: reference.modTypeName(m.itemModTypeId),
		retired: !!m.retiredAt
	}))
);
const skillPickRecords = $derived(
	keepActive(reference.skillRecords(), challenge.rewardSkillId).map((s) => ({
		id: s.id,
		name: s.name,
		color: 'var(--accent)',
		tag: `${s.baseDamage} dmg`,
		retired: !!s.retiredAt
	}))
);

const setItem = (id: number | undefined) => {
	store.patch(challenge.id, (d) => ((d as unknown as IChallenge).rewardItemId = id));
	open = null;
};
const setMod = (id: number | undefined) => {
	store.patch(challenge.id, (d) => ((d as unknown as IChallenge).rewardItemModId = id));
	open = null;
};
const setSkill = (id: number | undefined) => {
	store.patch(challenge.id, (d) => ((d as unknown as IChallenge).rewardSkillId = id));
	open = null;
};
</script>

<style lang="scss">
.ch-reward-warn {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	margin-top: 16px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--warning);
	border: 1px solid color-mix(in srgb, var(--warning) 35%, transparent);
	background: color-mix(in srgb, var(--warning) 8%, transparent);
	border-radius: 3px;
	padding: 7px 11px;
}
</style>
