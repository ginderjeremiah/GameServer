<!--
	Read-only tracked-value readout. What a challenge tracks is defined entirely by
	its type (a statistic bucketed by an entity dimension, or the player's level for
	Level Reached) and is never edited here.
-->
<div class="fld stat-readout-fld">
	<span class="lbl">Tracked Value</span>
	<div class="ch-stat-readout" class:none={kind === 'none'}>
		<WorkbenchIcon
			kind={kind === 'level' ? 'bars' : kind === 'none' ? 'target' : 'gauge'}
			size={13}
			stroke={kind === 'none' ? 'var(--text-tertiary)' : 'var(--accent)'}
		/>
		<span class="nm" class:muted={kind === 'none'}>{label}</span>
		{#if kind === 'stat'}
			<span class="ch-stat-ent"
				>{challenge.entityType !== EEntityType.None ? entityTypeName(challenge.entityType) : 'global'}</span
			>
		{:else if kind === 'level'}
			<span class="ch-stat-ent">player</span>
		{/if}
	</div>
</div>

<script lang="ts">
import { EEntityType, type IChallenge } from '$lib/api';
import { reference } from '../../reference.svelte';
import { entityTypeName, trackedKind, trackedLabel } from '../../entities/challenge-helpers';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';

interface Props {
	challenge: IChallenge;
}

const { challenge }: Props = $props();

const kind = $derived(trackedKind(challenge));
const label = $derived(trackedLabel(challenge, reference.challengeTypes));
</script>

<style lang="scss">
.stat-readout-fld {
	min-width: 248px;
	flex: 1 1 248px;
}
</style>
