<TooltipShell {accent} hidden={!challenge} bind:base={container}>
	{#snippet header()}
		<TooltipTitle label={typeName} name={challenge?.name ?? ''} diamondColor={accent} labelColor={accent} />
	{/snippet}

	{#if challenge?.description}
		<TooltipSection label="Requirement" last={!hasUnlocks}>
			<p class="ct-desc">{challenge.description}</p>
		</TooltipSection>
	{/if}

	{#if hasUnlocks}
		<TooltipSection label="Unlocks" last>
			{#each unlockedZones as zone (zone.id)}
				<div class="ct-row">
					<span class="ct-kind" style:color={tintColor(accent, 0.85)}>Zone</span>
					<span class="ct-name">{zone.name}</span>
				</div>
			{/each}
			{#if reward}
				<div class="ct-row">
					<span class="ct-kind" style:color={tintColor(reward.accent, 0.85)}>{reward.sub}</span>
					<span class="ct-name" class:sealed={!completed} style:color={reward.accent}>
						{completed ? reward.name : '???'}
					</span>
				</div>
			{/if}
		</TooltipSection>
	{/if}
</TooltipShell>

<script lang="ts">
import { EChallengeType } from '$lib/api';
import { challengeTypeColor, normalizeText, resolveUnlockReward, tintColor, zonesUnlockedBy } from '$lib/common';
import { playerChallenges, staticData } from '$stores';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';

export const getBaseNode = () => container;

interface Props {
	/** The challenge whose requirement + unlocks to show, or undefined for the empty/hidden panel. */
	challengeId: number | undefined;
}

const { challengeId }: Props = $props();

// Bound to the shell's root element and relocated into the global tooltip container by
// getBaseNode(); reactive so the relocation runs once the shell has mounted.
let container = $state<HTMLDivElement>();

const challenge = $derived(challengeId != null ? staticData.challenges?.[challengeId] : undefined);
const completed = $derived(challenge != null && playerChallenges.isChallengeCompleted(challenge.id));
const accent = $derived(challenge ? challengeTypeColor(challenge.challengeTypeId) : 'var(--accent)');
const typeName = $derived(
	challenge
		? (staticData.challengeTypes?.find((t) => t.id === challenge.challengeTypeId)?.name ??
				normalizeText(EChallengeType[challenge.challengeTypeId] ?? ''))
		: ''
);
const unlockedZones = $derived(challenge ? zonesUnlockedBy(challenge.id, staticData.zones ?? []) : []);
const reward = $derived(
	challenge
		? resolveUnlockReward(challenge, {
				items: staticData.items,
				itemMods: staticData.itemMods,
				skills: staticData.skills
			})
		: null
);
const hasUnlocks = $derived(unlockedZones.length > 0 || reward != null);
</script>

<style lang="scss">
.ct-desc {
	margin: 0;
	font-size: 12px;
	line-height: 1.5;
	color: var(--text-tertiary);
}

.ct-row {
	display: flex;
	align-items: baseline;
	gap: 8px;

	& + & {
		margin-top: 6px;
	}
}

.ct-kind {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	white-space: nowrap;
	flex-shrink: 0;
}

.ct-name {
	font-size: 13px;
	color: var(--text-primary);
	letter-spacing: -0.1px;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;

	&.sealed {
		letter-spacing: 1px;
		color: color-mix(in srgb, var(--text-primary) 45%, transparent);
	}
}
</style>
