<div class="timeline">
	<div class="track"></div>
	<div class="nodes">
		{#each levelNodes as node (node.level)}
			<button
				type="button"
				class="lvl"
				class:selected={node.level === sel}
				onclick={() => store.selectLevel(node.level)}
				aria-label={`Level ${node.level}`}
			>
				<span class="dot {node.kind}" class:max={node.level === maxLevel}></span>
				<span class="num" class:on={node.level === sel}>{node.level}</span>
			</button>
		{/each}
	</div>
</div>
<div class="legend">
	<span><span class="lg empty"></span>empty</span>
	<span><span class="lg bonus"></span>per-level bonus</span>
	<span class="ac"><span class="lg milestone"></span>milestone (grants a skill)</span>
</div>

<div class="payout">
	<div class="payout-head">
		<span class="pl">Level {sel}</span>
		{#if reward}<span class="ms-badge">Milestone</span>{/if}
		<div class="spacer"></div>
		<span class="threshold">threshold ~ {threshold.toLocaleString()} xp</span>
	</div>

	{#if hasPayout}
		<div class="field-label">Attribute modifiers</div>
		{#each modRows as mr (mr.index)}
			<div class="mod-row">
				<div class="m-attr">
					<AttributePicker
						ariaLabel="Attribute"
						value={mr.modifier.attributeId}
						options={attrOptionsFor(mr.modifier.attributeId)}
						onChange={(v) => store.updateModifier(tier.id, mr.index, { attributeId: v })}
					/>
				</div>
				<div class="m-type">
					<ProgSelect
						ariaLabel="Modifier type"
						value={mr.modifier.modifierTypeId}
						options={modTypeOptions}
						onChange={(v) => store.updateModifier(tier.id, mr.index, { modifierTypeId: v })}
					/>
				</div>
				<div class="m-val">
					<ProgNumber
						ariaLabel="Amount"
						value={mr.modifier.amount}
						allowNegative
						onChange={(v) => store.updateModifier(tier.id, mr.index, { amount: v })}
					/>
				</div>
				<button
					type="button"
					class="rm"
					aria-label="Remove modifier"
					onclick={() => store.removeModifier(tier.id, mr.index)}><WorkbenchIcon kind="x" size={12} /></button
				>
			</div>
		{/each}
		<button type="button" class="link-add" onclick={() => store.addModifier(tier.id, sel)}>+ Add modifier</button>

		<div class="field-label mt">
			Reward skill <span class="muted">(optional — granted on reaching this level)</span>
		</div>
		<div class="reward">
			<ProgSelect
				ariaLabel="Reward skill"
				value={reward?.rewardSkillId ?? NO_SKILL}
				options={rewardOptions}
				onChange={(v) => store.setReward(tier.id, sel, v)}
			/>
		</div>

		<div class="payout-foot">
			<button type="button" class="link-danger" onclick={() => store.removePayout(tier.id, sel)}
				>✕ Remove this payout level</button
			>
		</div>
	{:else}
		<div class="empty-payout">
			<div class="ep-text">No payout at level {sel} — players just gain the level.</div>
			<button
				type="button"
				class="btn-add"
				data-testid="progression-add-payout"
				onclick={() => store.addPayout(tier.id, sel)}>+ Add a payout here</button
			>
		</div>
	{/if}
</div>

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import { reference } from '../reference.svelte';
import type { ProgressionStore } from './progression-store.svelte';
import { cumulativeXp, modifiersAtLevel, rewardAtLevel } from './progression-helpers';
import { NO_SKILL, type WorkbenchProficiency } from './types';
import ProgSelect from './ProgSelect.svelte';
import ProgNumber from './ProgNumber.svelte';
import AttributePicker from '../components/AttributePicker.svelte';

interface Props {
	store: ProgressionStore;
	tier: WorkbenchProficiency;
}

const { store, tier }: Props = $props();

const maxLevel = $derived(Math.max(1, Math.floor(tier.maxLevel) || 1));
const sel = $derived(Math.min(Math.max(1, store.selectedLevel), maxLevel));
const reward = $derived(rewardAtLevel(tier, sel));
const selMods = $derived(modifiersAtLevel(tier, sel));
const hasPayout = $derived(selMods.length > 0 || reward !== undefined);
const threshold = $derived(cumulativeXp(tier.baseXp, tier.xpGrowth, sel));

const modTypeOptions = reference.modifierTypeOptions();
const rewardOptions = $derived(reference.playerSkillOptions(reward?.rewardSkillId));

// Absolute indices into levelModifiers so a mid-list edit/remove targets the right row.
const modRows = $derived(
	tier.levelModifiers.map((modifier, index) => ({ modifier, index })).filter((row) => row.modifier.level === sel)
);

// Forbid two modifiers for the same attribute at one level (the backend keys on (level, attribute)).
const usedAttrs = $derived(new Set(selMods.map((m) => m.attributeId)));
const attrOptionsFor = (current: number) =>
	reference.attributeOptions().filter((o) => o.value === current || !usedAttrs.has(o.value));

const levelNodes = $derived.by(() => {
	const nodes: { level: number; kind: 'empty' | 'bonus' | 'milestone' }[] = [];
	for (let level = 1; level <= maxLevel; level++) {
		const hasReward = rewardAtLevel(tier, level) !== undefined;
		const hasMod = modifiersAtLevel(tier, level).length > 0;
		nodes.push({ level, kind: hasReward ? 'milestone' : hasMod ? 'bonus' : 'empty' });
	}
	return nodes;
});
</script>

<style lang="scss">
.timeline {
	position: relative;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	padding: 22px 22px 16px;
	margin-bottom: 14px;
}
.track {
	position: absolute;
	left: 32px;
	right: 32px;
	top: 33px;
	height: 2px;
	background: var(--border-subtle);
}
.nodes {
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	position: relative;
}
.lvl {
	flex: 1;
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 10px;
	background: none;
	border: none;
	cursor: pointer;
	padding: 0;
}
.dot {
	width: 13px;
	height: 13px;
	border-radius: 50%;

	&.empty {
		border: 1px solid var(--border-light);
		background: var(--page);
	}
	&.bonus {
		width: 17px;
		height: 17px;
		background: color-mix(in srgb, var(--white) 30%, transparent);
	}
	&.milestone {
		width: 22px;
		height: 22px;
		background: var(--accent);
	}
	&.milestone.max {
		border: 2px solid var(--accent-light);
	}
}
.lvl.selected .dot {
	box-shadow: 0 0 0 4px color-mix(in srgb, var(--accent) 28%, transparent);
}
.num {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-tertiary);

	&.on {
		color: var(--accent-light);
	}
}
.legend {
	display: flex;
	gap: 18px;
	margin-bottom: 18px;
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);

	span {
		display: inline-flex;
		align-items: center;
		gap: 6px;
	}
	.ac {
		color: var(--accent-light);
	}
}
.lg {
	width: 12px;
	height: 12px;
	border-radius: 50%;

	&.empty {
		border: 1px solid var(--border-light);
	}
	&.bonus {
		background: color-mix(in srgb, var(--white) 30%, transparent);
	}
	&.milestone {
		background: var(--accent);
	}
}
.payout {
	background: var(--panel);
	border: 1px solid var(--accent);
	border-radius: 5px;
	padding: 18px 20px;
	box-shadow: 0 0 0 1px color-mix(in srgb, var(--accent) 18%, transparent);
}
.payout-head {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 16px;
}
.pl {
	font-size: 15px;
	font-weight: 500;
	color: var(--text-primary);
}
.ms-badge {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--accent);
	border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent);
	background: color-mix(in srgb, var(--accent) 10%, transparent);
	border-radius: 3px;
	padding: 2px 8px;
}
.spacer {
	flex: 1;
}
.threshold {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
}
.field-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 9px;

	&.mt {
		margin-top: 18px;
	}
	.muted {
		text-transform: none;
		letter-spacing: 0;
	}
}
.mod-row {
	display: flex;
	gap: 8px;
	align-items: flex-end;
	margin-bottom: 8px;
}
.m-attr {
	flex: 1;
}
.m-type {
	width: 150px;
}
.m-val {
	width: 96px;
}
.rm {
	width: 30px;
	height: 36px;
	background: transparent;
	border: none;
	color: var(--text-muted);
	cursor: pointer;

	&:hover {
		color: var(--change-removed);
	}
}
.link-add {
	background: none;
	border: none;
	color: var(--accent);
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 2px 0;
	cursor: pointer;
}
.reward {
	max-width: 340px;
}
.payout-foot {
	margin-top: 14px;
	border-top: 1px solid var(--border-subtle);
	padding-top: 12px;
}
.link-danger {
	background: none;
	border: none;
	color: var(--text-muted);
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	cursor: pointer;
	padding: 0;

	&:hover {
		color: var(--change-removed);
	}
}
.empty-payout {
	text-align: center;
	padding: 14px 0 6px;
}
.ep-text {
	font-size: 13px;
	color: var(--text-tertiary);
	margin-bottom: 12px;
}
.btn-add {
	background: color-mix(in srgb, var(--accent) 12%, transparent);
	border: 1px solid var(--accent);
	color: var(--accent-light);
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 8px 16px;
	border-radius: 3px;
	cursor: pointer;
}
</style>
