<div class="field-label">
	Prerequisite proficiencies <span class="muted">— must be maxed to open this path (cross-path gateways)</span>
</div>

{#if tier.prerequisiteIds.length === 0}
	<div class="starter">
		Starter tier — opens from the world or by acquiring a contributing skill. Add a prerequisite to gate it behind other
		paths.
	</div>
{:else}
	<div class="gated">
		<div class="chips">
			{#each tier.prerequisiteIds as prereqId (prereqId)}
				<span class="chip">
					{reference.proficiencyName(prereqId) ?? `#${prereqId}`}
					<span class="star">✦</span>
					<button
						type="button"
						class="chip-x"
						aria-label="Remove prerequisite"
						onclick={() => store.removePrerequisite(tier.id, prereqId)}
					>
						<WorkbenchIcon kind="x" size={10} />
					</button>
				</span>
			{/each}
		</div>
		<span class="opens-arrow">┄►</span>
		<div class="opens">
			<div class="opens-cap">opens</div>
			<div class="opens-name">{tier.name || 'This tier'} · Tier {tier.pathOrdinal}</div>
		</div>
	</div>
{/if}

<div class="add-prereq">
	<ProgSelect label="Add prerequisite" width={340} value={addPick} options={addOptions} onChange={onAddPrereq} />
</div>

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import { reference } from '../reference.svelte';
import type { ProgressionStore } from './progression-store.svelte';
import type { WorkbenchProficiency } from './types';
import ProgSelect from './ProgSelect.svelte';

interface Props {
	store: ProgressionStore;
	tier: WorkbenchProficiency;
}

const { store, tier }: Props = $props();

const ADD_PLACEHOLDER = -1;
let addPick = $state(ADD_PLACEHOLDER);

const addOptions = $derived([
	{ value: ADD_PLACEHOLDER, text: '+ Add prerequisite…' },
	...reference.proficiencyOptions().filter((o) => o.value !== tier.id && !tier.prerequisiteIds.includes(o.value))
]);

const onAddPrereq = (value: number) => {
	if (value !== ADD_PLACEHOLDER) {
		store.addPrerequisite(tier.id, value);
	}
	addPick = ADD_PLACEHOLDER;
};
</script>

<style lang="scss">
.field-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 9px;

	.muted {
		text-transform: none;
		letter-spacing: 0;
	}
}
.starter {
	background: var(--panel);
	border: 1px dashed var(--border-light);
	border-radius: 5px;
	padding: 16px;
	font-size: 13px;
	color: var(--text-tertiary);
}
.gated {
	display: flex;
	gap: 24px;
	align-items: center;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	padding: 18px;
}
.chips {
	display: flex;
	flex-direction: column;
	gap: 8px;
}
.chip {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	border: 1px solid var(--accent);
	background: color-mix(in srgb, var(--accent) 8%, transparent);
	border-radius: 14px;
	padding: 5px 12px;
	font-size: 12.5px;
	color: var(--text-secondary);
}
.star {
	color: var(--accent);
}
.chip-x {
	background: none;
	border: none;
	color: var(--text-muted);
	cursor: pointer;
	display: flex;
	padding: 0;

	&:hover {
		color: var(--change-removed);
	}
}
.opens-arrow {
	font-family: var(--mono);
	font-size: 18px;
	color: var(--accent);
}
.opens {
	border: 1px dashed var(--accent);
	border-radius: 6px;
	padding: 12px 16px;
	text-align: center;
	background: color-mix(in srgb, var(--accent) 6%, transparent);
}
.opens-cap {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--accent);
}
.opens-name {
	font-size: 14px;
	font-weight: 500;
	color: var(--text-primary);
}
.add-prereq {
	margin-top: 14px;
}
</style>
