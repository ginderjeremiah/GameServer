<div class="challenges-frame" data-testid="challenges-screen" bind:this={frame}>
	<!-- Header -->
	<div class="chal-header">
		<div>
			<div class="chal-eyebrow">
				<span class="diamond"></span>
				<span class="mono-label accent">Quests</span>
			</div>
			<div class="chal-title">Challenges</div>
		</div>
		<div class="chal-summary">
			<div class="summary-count">
				<span class="summary-done">{view.summary.done}</span>
				<span class="summary-total">/ {view.summary.total}</span>
			</div>
			<div class="mono-label sm">unlocked · {view.summary.pct}%</div>
		</div>
	</div>

	<!-- Body: type rail + detail/overview -->
	{#if view.error}
		<div class="chal-error" data-testid="challenges-error">
			<div class="error-title">Couldn’t load challenges</div>
			<p class="error-copy">Something went wrong fetching your challenge progress. Please try again later.</p>
		</div>
	{:else}
		<div class="chal-body">
			<TypeRail groups={view.groups} selected={view.selectedType} onSelect={(t) => view.select(t)} />

			<div class="chal-detail">
				{#if view.selectedType === 'all'}
					<OverviewPane
						summary={view.summary}
						nextUp={view.nextUp}
						groups={view.groups}
						onPick={(t) => view.select(t)}
					/>
				{:else if view.selectedGroup}
					<TypeHero group={view.selectedGroup} />
					<div class="detail-toolbar">
						<span class="mono-label sm">{view.selectedGroup.items.length} challenges</span>
						<SortControl value={view.sort} onChange={(s) => view.setSort(s)} />
					</div>
					<div class="detail-grid">
						{#each view.detail as challenge (challenge.id)}
							<ChallengeDetailCard c={challenge} />
						{/each}
					</div>
				{/if}
			</div>
		</div>
	{/if}

	{#if view.loading}
		<Loading delay={150} />
	{/if}

	<RewardTooltip bind:this={tooltip} reward={hoveredReward} />
</div>

<script lang="ts">
import { Loading } from '$components';
import { anchorPosition, playerChallenges, registerTooltipComponent, toastError, type TooltipComponent } from '$stores';
import { onMount } from 'svelte';
import { ChallengesView, type ResolvedReward } from './challenges-view.svelte';
import { setRewardTooltip, type RewardTooltipController } from './reward-tooltip-context';
import ChallengeDetailCard from './ChallengeDetailCard.svelte';
import OverviewPane from './OverviewPane.svelte';
import RewardTooltip from './RewardTooltip.svelte';
import SortControl from './SortControl.svelte';
import TypeHero from './TypeHero.svelte';
import TypeRail from './TypeRail.svelte';

const view = new ChallengesView();

let frame: HTMLDivElement;
let tooltip = $state<TooltipComponent>();
let hoveredReward = $state<ResolvedReward>();

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

// One reward tooltip for the whole screen, driven through context so nested
// reward affordances don't have to thread hover handlers down the tree.
const controller: RewardTooltipController = {
	show: (reward, anchor) => {
		hoveredReward = reward;
		setTooltipPosition(anchorPosition(anchor));
		showTooltip();
	},
	move: (anchor) => setTooltipPosition(anchorPosition(anchor)),
	hide: () => {
		hoveredReward = undefined;
		hideTooltip();
	}
};
setRewardTooltip(controller);

onMount(async () => {
	// Force a fresh fetch so progress reflects play since the store was last loaded (it is loaded
	// once at game boot to gate zone navigation). The shared store is the single source of truth.
	await playerChallenges.load(true);
	view.error = playerChallenges.error;
	if (playerChallenges.error) {
		// Don't conflate a failed load with a genuine no-progress result — a
		// dropped fetch would otherwise render every challenge as zero progress.
		toastError('Your challenge progress could not be loaded. Please try again later.');
	} else {
		view.playerChallenges = playerChallenges.all;
	}
	view.loading = false;
});
</script>

<style lang="scss">
.challenges-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	position: relative;
	color: var(--text-primary);
	font-family: Geist, Arial, Helvetica, sans-serif;
	overflow: hidden;
}

.chal-header {
	display: flex;
	align-items: flex-end;
	justify-content: space-between;
	gap: 24px;
	padding: 24px 30px 16px;
}

.chal-eyebrow {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 7px;
}

.diamond {
	width: 7px;
	height: 7px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
	flex-shrink: 0;
}

.chal-title {
	font-size: 28px;
	font-weight: 400;
	letter-spacing: -0.4px;
	line-height: 1;
}

.chal-summary {
	text-align: right;
}

.summary-count {
	display: flex;
	align-items: baseline;
	gap: 6px;
	justify-content: flex-end;
}

.summary-done {
	font-family: var(--mono);
	font-size: 22px;
	color: var(--success);
}

.summary-total {
	font-family: var(--mono);
	font-size: 13px;
	color: var(--text-muted);
}

.chal-body {
	flex: 1;
	min-height: 0;
	display: flex;
	padding: 0 30px 8px;
}

.chal-detail {
	flex: 1;
	min-width: 0;
	overflow-y: auto;
	padding: 2px 0 24px 22px;
}

.chal-error {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	text-align: center;
	gap: 10px;
	padding: 24px 30px;
}

.error-title {
	font-size: 18px;
	font-weight: 500;
	color: var(--text-primary);
}

.error-copy {
	margin: 0;
	max-width: 420px;
	font-size: 13px;
	line-height: 1.6;
	color: var(--text-tertiary);
}

.detail-toolbar {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-bottom: 13px;
}

.detail-grid {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 12px;
}
</style>
