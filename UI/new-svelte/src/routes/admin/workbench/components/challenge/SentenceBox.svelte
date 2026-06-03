<!-- Plain-language preview of the assembled objective, plus the unusual "at most" flag. -->
<div class="ch-sentence">
	<span class="ch-sentence-eye">Players see</span>
	<span class="ch-sentence-text">{sentence}</span>
	{#if atMost}
		<span class="ch-flag info"><WorkbenchIcon kind="gauge" size={11} sw={1.5} />completes at or under goal</span>
	{/if}
</div>

<script lang="ts">
import { EChallengeGoalComparison, type IChallenge } from '$lib/api';
import { reference } from '../../reference.svelte';
import { challengeSentence, goalComparisonOf } from '../../entities/challenge-helpers';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';

interface Props {
	challenge: IChallenge;
}

const { challenge }: Props = $props();

const sentence = $derived(challengeSentence(challenge, reference.entityName));
const atMost = $derived(
	goalComparisonOf(reference.challengeTypes, challenge.challengeTypeId) === EChallengeGoalComparison.AtMost
);
</script>
