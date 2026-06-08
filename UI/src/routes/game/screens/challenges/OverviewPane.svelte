<div class="overview">
	<div class="overview-top">
		<div class="summary-card">
			<div class="mono-label">Overall progress</div>
			<div class="summary-count">
				<span class="summary-done">{summary.done}</span>
				<span class="summary-total">/ {summary.total} rewards unlocked</span>
			</div>
			<ProgressBar percent={summary.pct} accent="var(--accent)" height={6} />
			<div class="summary-foot">
				<span class="mono-label sm">{summary.active} in progress</span>
				<span class="mono-label sm">{summary.pct}% complete</span>
			</div>
		</div>

		{#if nextUp && nextUp.reward}
			<div
				class="next-card"
				style:background="linear-gradient(135deg, {tintColor(nextUp.reward.accent, 0.12)}, transparent 70%)"
				style:border="1px solid {tintColor(nextUp.reward.accent, 0.3)}"
			>
				<div class="next-head">
					<span class="next-dot" style:background={nextUp.reward.accent} style:--acc={nextUp.reward.accent}></span>
					<span class="mono-label" style:color={tintColor(nextUp.reward.accent, 0.85)}>Next up · closest to unlock</span
					>
				</div>
				<div class="next-name">{nextUp.name}</div>
				<div class="next-progress"><ProgressReadout c={nextUp} /></div>
				<RewardAffordance reward={nextUp.reward} variant="tile" />
			</div>
		{/if}
	</div>

	<div>
		<div class="all-types-label"><span class="mono-label">All challenge types</span></div>
		<div class="types-grid">
			{#each groups as group (group.typeId)}
				{@const stats = typeStats(group.items)}
				{@const pct = (stats.done / Math.max(1, stats.total)) * 100}
				{@const complete = stats.done === stats.total}
				{@const next2 = sortChallenges(
					group.items.filter((c) => c.state !== 'done'),
					'progress'
				)[0]}
				<button
					class="type-card"
					style:border-left="2px solid {tintColor(group.accent, 0.6)}"
					onclick={() => onPick(group.typeId)}
				>
					<div class="type-card-head">
						<RingMeter {pct} accent={group.accent} size={34} stroke={3} done={complete}>
							<TypeGlyph typeId={group.typeId} color={complete ? 'var(--success)' : group.accent} size={15} />
						</RingMeter>
						<div class="type-card-text">
							<div class="type-card-name">{group.label}</div>
							<div class="mono-label sm" class:done={complete}>{stats.done}/{stats.total} unlocked</div>
						</div>
					</div>
					<div class="type-card-reward">
						{#if next2}
							<RewardAffordance reward={next2.reward} variant="chip" />
						{:else}
							<span class="all-unlocked">All unlocked ✓</span>
						{/if}
					</div>
				</button>
			{/each}
		</div>
	</div>
</div>

<script lang="ts">
import type { EChallengeType } from '$lib/api';
import { tintColor } from '$lib/common';
import {
	sortChallenges,
	typeStats,
	type ChallengeVM,
	type OverallSummary,
	type TypeGroup
} from './challenges-view.svelte';
import ProgressBar from './ProgressBar.svelte';
import ProgressReadout from './ProgressReadout.svelte';
import RewardAffordance from './RewardAffordance.svelte';
import RingMeter from './RingMeter.svelte';
import TypeGlyph from './TypeGlyph.svelte';

interface Props {
	summary: OverallSummary;
	nextUp: ChallengeVM | null;
	groups: TypeGroup[];
	onPick: (type: EChallengeType) => void;
}

const { summary, nextUp, groups, onPick }: Props = $props();
</script>

<style lang="scss">
.overview {
	display: flex;
	flex-direction: column;
	gap: 18px;
}

.overview-top {
	display: flex;
	gap: 14px;
}

.mono-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);

	&.sm {
		font-size: 8.5px;
	}

	&.done {
		color: var(--success);
	}
}

.summary-card {
	flex: 1;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 17px 19px;
}

.summary-count {
	display: flex;
	align-items: baseline;
	gap: 8px;
	margin: 9px 0 11px;
}

.summary-done {
	font-family: var(--mono);
	font-size: 32px;
	color: var(--success);
	line-height: 1;
}

.summary-total {
	font-family: var(--mono);
	font-size: 14px;
	color: var(--text-muted);
}

.summary-foot {
	display: flex;
	justify-content: space-between;
	margin-top: 8px;
}

.next-card {
	width: 300px;
	flex-shrink: 0;
	position: relative;
	overflow: hidden;
	border-radius: 4px;
	padding: 17px 19px;
}

.next-head {
	display: flex;
	align-items: center;
	gap: 7px;
	margin-bottom: 9px;
}

.next-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
}

.next-name {
	font-size: 16px;
	color: var(--text-primary);
	margin-bottom: 5px;
	letter-spacing: -0.2px;
}

.next-progress {
	margin-bottom: 12px;
}

.all-types-label {
	margin-bottom: 11px;
}

.types-grid {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 12px;
}

.type-card {
	text-align: left;
	cursor: pointer;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 13px 15px;
}

.type-card-head {
	display: flex;
	align-items: center;
	gap: 11px;
}

.type-card-text {
	flex: 1;
	min-width: 0;
}

.type-card-name {
	font-size: 14px;
	color: var(--text-primary);
	letter-spacing: -0.1px;
}

.type-card-reward {
	margin-top: 11px;
}

.all-unlocked {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--success);
}

@media (prefers-reduced-motion: no-preference) {
	.next-dot {
		animation: next-pulse 1.8s ease-in-out infinite;
	}

	@keyframes next-pulse {
		0%,
		100% {
			box-shadow: 0 0 0 0 var(--acc);
			opacity: 1;
		}
		70% {
			box-shadow: 0 0 0 5px transparent;
			opacity: 0.7;
		}
	}
}
</style>
