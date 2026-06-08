<!-- The "available" boss affordance: a boxed, centered, single-row banner naming the
     zone boss with an Auto-fight toggle and the Challenge action. Switches to
     "Re-challenge" + a Cleared seal once the zone's boss has been defeated. -->
<div class="trigger-wrap">
	<div class="trigger" data-testid="boss-trigger">
		<BossDiamond size={8} />
		<div class="identity">
			<BossKicker>Zone Boss</BossKicker>
			<span class="boss-name">{bossName}</span>
			<span class="boss-level">LV · {bossLevel}</span>
		</div>
		{#if cleared}
			<ClearedSeal compact />
		{/if}
		<div class="divider" aria-hidden="true"></div>
		<AutoFightToggle on={autoFight} onChange={onToggleAutoFight} compact />
		<ChallengeButton onClick={onChallenge}>{cleared ? 'Re-challenge' : 'Challenge'}</ChallengeButton>
	</div>
</div>

<script lang="ts">
import BossDiamond from './BossDiamond.svelte';
import BossKicker from './BossKicker.svelte';
import ClearedSeal from './ClearedSeal.svelte';
import AutoFightToggle from './AutoFightToggle.svelte';
import ChallengeButton from './ChallengeButton.svelte';

type Props = {
	bossName: string;
	bossLevel: number;
	cleared: boolean;
	autoFight: boolean;
	onChallenge: () => void;
	onToggleAutoFight: (on: boolean) => void;
};

const { bossName, bossLevel, cleared, autoFight, onChallenge, onToggleAutoFight }: Props = $props();
</script>

<style lang="scss">
.trigger-wrap {
	display: flex;
	justify-content: center;
	width: 100%;
}

.trigger {
	display: inline-flex;
	align-items: center;
	gap: 14px;
	background: linear-gradient(
		90deg,
		color-mix(in srgb, var(--boss-accent) 9%, transparent),
		color-mix(in srgb, var(--white) 2%, transparent) 72%
	);
	border: 1px solid color-mix(in srgb, var(--boss-accent) 27%, transparent);
	border-left: 3px solid var(--boss-accent);
	border-radius: 3px;
	padding: 9px 12px 9px 14px;
}

.identity {
	display: flex;
	align-items: baseline;
	gap: 10px;
}

.boss-name {
	font-size: 15.5px;
	font-weight: 500;
	color: var(--text-primary);
	letter-spacing: -0.1px;
}

.boss-level {
	font-family: var(--mono);
	font-size: 10px;
	color: color-mix(in srgb, var(--text-primary) 55%, transparent);
	letter-spacing: 0.5px;
}

.divider {
	width: 1px;
	align-self: stretch;
	background: color-mix(in srgb, var(--white) 10%, transparent);
	margin: 0 2px;
}
</style>
