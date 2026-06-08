<!-- Goal amount with the statistic-derived unit and (for Time Trial) an "at most" hint. -->
<div class="fld goal-fld">
	<span class="lbl">Goal {isMs ? '(time)' : 'amount'}{atMost ? ' · at most' : ''}</span>
	<div class="num-unit">
		<NumInput
			class="inp num{dirty ? ' dirty' : ''}"
			style="padding-right: {pad}px"
			value={challenge.progressGoal}
			onChange={(v) => store.patch(challenge.id, (d) => ((d as unknown as IChallenge).progressGoal = v))}
		/>
		<span class="suffix">{unit}</span>
	</div>
	{#if dirty}<span class="dirty-dot"></span>{/if}
</div>

<script lang="ts">
import { EChallengeGoalComparison, type IChallenge } from '$lib/api';
import { reference } from '../../reference.svelte';
import type { EntityStore } from '../../entity-store.svelte';
import type { Identified } from '../../entities/types';
import { goalComparisonOf, goalUnit } from '../../entities/challenge-helpers';
import NumInput from '../NumInput.svelte';

interface Props {
	challenge: IChallenge;
	baseline: IChallenge | undefined;
	store: EntityStore<Identified>;
}

const { challenge, baseline, store }: Props = $props();

const unit = $derived(goalUnit(challenge));
const isMs = $derived(unit === 'ms');
const pad = $derived(Math.round(unit.length * 6.6 + 20));
const atMost = $derived(
	goalComparisonOf(reference.challengeTypes, challenge.challengeTypeId) === EChallengeGoalComparison.AtMost
);
const dirty = $derived(baseline ? challenge.progressGoal !== baseline.progressGoal : false);
</script>

<style lang="scss">
.goal-fld {
	width: 210px;
}
</style>
