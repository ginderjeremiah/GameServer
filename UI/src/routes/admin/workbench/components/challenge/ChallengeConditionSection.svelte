<!--
	Condition builder (compact form). The objective type is the only editable driver
	here: changing it re-derives the tracked statistic, entity dimension, and whether
	a target is even possible. The statistic/entity are surfaced read-only.
-->
<div class="ch-compact">
	<div class="ch-compact-grid">
		<div class="fld type-fld">
			<span class="lbl">Objective Type</span>
			<div class="type-select">
				<select
					class="sel"
					class:dirty={typeDirty}
					value={challenge.challengeTypeId}
					onchange={(e) =>
						store.patch(challenge.id, (d) =>
							deriveFromType(d as unknown as IChallenge, reference.challengeTypes, +e.currentTarget.value)
						)}
				>
					{#each reference.challengeTypes as type (type.id)}
						<option value={type.id}>{type.name}</option>
					{/each}
				</select>
				<SelectCaret />
				{#if typeDirty}<DirtyDot />{/if}
			</div>
		</div>
		<StatReadout {challenge} />
		<GoalField {challenge} baseline={base} {store} />
		<div class="grid-break"></div>
		<ScopeField {challenge} baseline={base} {store} />
	</div>
	<div class="ch-grid-cap">
		<WorkbenchIcon kind="bolt" size={11} stroke="var(--text-muted)" />
		Statistic &amp; entity scope are defined by the challenge type.
	</div>
	<SentenceBox {challenge} />
</div>

<script lang="ts">
import type { IChallenge } from '$lib/api';
import { reference } from '../../reference.svelte';
import type { EntityStore } from '../../entity-store.svelte';
import type { Identified } from '../../entities/types';
import { deriveFromType } from '../../entities/challenge-helpers';
import SelectCaret from '../SelectCaret.svelte';
import DirtyDot from '../DirtyDot.svelte';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';
import StatReadout from './StatReadout.svelte';
import GoalField from './GoalField.svelte';
import ScopeField from './ScopeField.svelte';
import SentenceBox from './SentenceBox.svelte';

interface Props {
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { record, baseline, store }: Props = $props();

const challenge = $derived(record as unknown as IChallenge);
const base = $derived(baseline as unknown as IChallenge | undefined);
const typeDirty = $derived(base ? challenge.challengeTypeId !== base.challengeTypeId : false);
</script>

<style lang="scss">
.type-fld {
	width: 220px;
}
.type-select {
	position: relative;
}
.grid-break {
	flex-basis: 100%;
	height: 0;
}
</style>
