<!-- The welcome-back gate (#1043): a full-screen summary of the progress the player's idle loop made
	while they were away, shown between the loading screen and the game shell. Dismissing it (Enter)
	starts the live idle loop. Presentational — the offline check, state re-sync, and engine start live in
	the WelcomeBackView; this only renders the summary and reports the dismissal. -->
<div class="welcome-screen" data-testid="welcome-back-gate">
	<div class="welcome-card">
		<header class="welcome-header">
			<DiamondMark pulsing marginBottom={20} />
			<h1>Welcome back</h1>
			<p class="away">
				Away for <strong>{awayLabel}</strong> · {modeLabel}{#if zoneName}
					· {zoneName}{/if}
			</p>
		</header>

		<div class="stat-grid">
			<SummaryStat label="Battles won" value={summary.battlesWon} muted={summary.battlesWon === 0} />
			<SummaryStat label="Battles lost" value={summary.battlesLost} muted={summary.battlesLost === 0} />
			<SummaryStat label="Battles drawn" value={summary.battlesDrawn} muted={summary.battlesDrawn === 0} />
			<SummaryStat
				label="Exp earned"
				value={formatNum(summary.totalExp)}
				accent="var(--gold)"
				muted={summary.totalExp === 0}
			/>
			<SummaryStat
				label="Levels gained"
				value={summary.levelsGained}
				accent="var(--gold)"
				muted={summary.levelsGained === 0}
			/>
			<SummaryStat label="Stat points" value={summary.statPointsGained} muted={summary.statPointsGained === 0} />
		</div>

		{#if completedChallenges.length > 0}
			<section class="challenges">
				<h2>Challenges completed</h2>
				<ul class="unlock-list">
					{#each completedChallenges as completed (completed.challengeId)}
						<ChallengeUnlockRow {completed} />
					{/each}
				</ul>
			</section>
		{/if}

		<button class="enter-btn" type="button" onclick={onEnter} data-testid="welcome-back-enter">
			Enter the realm
		</button>
	</div>
</div>

<script lang="ts">
import type { IOfflineProgressModel } from '$lib/api';
import { formatNum } from '$lib/common';
import { staticData } from '$stores';
import DiamondMark from '$components/DiamondMark.svelte';
import SummaryStat from './SummaryStat.svelte';
import ChallengeUnlockRow from './ChallengeUnlockRow.svelte';
import { formatAwayDuration, resolveCompletedChallenges } from './offline-summary';

interface Props {
	summary: IOfflineProgressModel;
	onEnter: () => void;
}

let { summary, onEnter }: Props = $props();

const awayLabel = $derived(formatAwayDuration(summary.awayMs));
const modeLabel = $derived(summary.autoChallengeBoss ? 'Boss farming' : 'Idle farming');
const zoneName = $derived(staticData.zones?.[summary.zoneId]?.name ?? '');
const completedChallenges = $derived(
	resolveCompletedChallenges(summary.completedChallenges, {
		challenges: staticData.challenges,
		items: staticData.items,
		itemMods: staticData.itemMods,
		skills: staticData.skills
	})
);
</script>

<style lang="scss">
.welcome-screen {
	width: 100%;
	height: 100%;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 40px;
	overflow: auto;
}

.welcome-card {
	width: 440px;
	max-width: 100%;
	display: flex;
	flex-direction: column;
	gap: 22px;
}

.welcome-header {
	display: flex;
	flex-direction: column;
	align-items: center;
	text-align: center;

	h1 {
		margin: 0;
		font-size: 1.7rem;
		color: var(--title-color);
		letter-spacing: 0.02em;
	}

	.away {
		margin: 6px 0 0;
		font-size: 0.85rem;
		color: var(--text-tertiary);

		strong {
			color: var(--text-secondary);
			font-weight: 600;
		}
	}
}

.stat-grid {
	display: grid;
	grid-template-columns: repeat(3, 1fr);
	gap: 8px;
}

.challenges {
	display: flex;
	flex-direction: column;
	gap: 8px;

	h2 {
		margin: 0;
		font-family: var(--mono);
		font-size: 0.72rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: var(--eyebrow);
	}
}

.unlock-list {
	list-style: none;
	margin: 0;
	padding: 0 12px;
	background: var(--panel-2);
	border: var(--default-border);
	border-radius: var(--border-radius);
	max-height: 240px;
	overflow-y: auto;
}

.enter-btn {
	margin-top: 4px;
	padding: 12px;
	font-size: 0.95rem;
	font-weight: 600;
	color: var(--text-on-accent);
	background: var(--btn-background);
	border: none;
	border-radius: var(--border-radius);
	cursor: pointer;
	transition: filter 120ms ease;

	&:hover {
		filter: brightness(1.08);
	}
}
</style>
