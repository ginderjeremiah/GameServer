<!--
	Scope: a statistic-bucketed challenge is either global or scoped to a specific
	entity of its statistic's entity type. Stat-less / global statistics have no
	target, so the field collapses to an explanatory read-out.
-->
{#if challenge.entityType === EEntityType.None}
	<div class="fld scope-global-fld">
		<span class="lbl">Scope</span>
		<div class="ch-scope-global">
			<WorkbenchIcon kind="map" size={13} stroke="var(--text-tertiary)" />
			<span>
				{kind === 'level'
					? 'Tracked from player level — no target entity'
					: 'Tracked globally — this statistic has no target entity'}
			</span>
		</div>
	</div>
{:else}
	<div class="fld scope-fld">
		<span class="lbl" class:warn={dirty}>Scope · {entityTypeName(challenge.entityType)}</span>
		<div class="scope-row">
			<div class="eswitch">
				<button type="button" class:active={!specific} onclick={setGlobal}>Global</button>
				<button type="button" class:active={specific} onclick={setSpecific}>Specific</button>
			</div>
			{#if specific}
				<div class="fld scope-select">
					<select
						class="sel"
						class:dirty
						value={challenge.targetEntityId}
						onchange={(e) =>
							store.patch(challenge.id, (d) => ((d as unknown as IChallenge).targetEntityId = +e.currentTarget.value))}
					>
						{#each options as option (option.value)}
							<option value={option.value}>{option.text}</option>
						{/each}
					</select>
					<SelectCaret />
				</div>
			{/if}
		</div>
	</div>
{/if}

<script lang="ts">
import { EEntityType, type IChallenge } from '$lib/api';
import { reference } from '../../reference.svelte';
import type { EntityStore } from '../../entity-store.svelte';
import type { Identified } from '../../entities/types';
import { entityTypeName, trackedKind } from '../../entities/challenge-helpers';
import SelectCaret from '../SelectCaret.svelte';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';

interface Props {
	challenge: IChallenge;
	baseline: IChallenge | undefined;
	store: EntityStore<Identified>;
}

const { challenge, baseline, store }: Props = $props();

const kind = $derived(trackedKind(challenge));
const specific = $derived(challenge.targetEntityId != null);
const options = $derived(reference.entityOptions(challenge.entityType));
const dirty = $derived(baseline ? challenge.targetEntityId !== baseline.targetEntityId : false);

const setGlobal = () => store.patch(challenge.id, (d) => ((d as unknown as IChallenge).targetEntityId = undefined));
const setSpecific = () =>
	store.patch(challenge.id, (d) => ((d as unknown as IChallenge).targetEntityId = options[0]?.value));
</script>

<style lang="scss">
.scope-global-fld {
	min-width: 220px;
	flex: 1 1 220px;
}
.scope-fld {
	min-width: 260px;
	flex: 1 1 260px;
}
.scope-row {
	display: flex;
	gap: 10px;
	align-items: center;
	flex-wrap: wrap;
}
.scope-select {
	position: relative;
	min-width: 200px;
	flex: 1;
}
</style>
