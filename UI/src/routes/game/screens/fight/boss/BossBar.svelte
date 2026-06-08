<!-- The "engaged" boss affordance, shown in place of the trigger during a boss fight:
     a heightened bar naming the boss, with the Auto-fight toggle and a Retreat action. -->
<div class="boss-bar" data-testid="boss-bar">
	<BossDiamond size={9} />
	<div class="identity">
		<BossKicker>Boss Fight</BossKicker>
		<span class="boss-name">{bossName}</span>
	</div>
	<AutoFightToggle on={autoFight} onChange={onToggleAutoFight} compact />
	<RetreatButton onClick={onRetreat} />
</div>

<script lang="ts">
import BossDiamond from './BossDiamond.svelte';
import BossKicker from './BossKicker.svelte';
import AutoFightToggle from './AutoFightToggle.svelte';
import RetreatButton from './RetreatButton.svelte';

type Props = {
	bossName: string;
	autoFight: boolean;
	onToggleAutoFight: (on: boolean) => void;
	onRetreat: () => void;
};

const { bossName, autoFight, onToggleAutoFight, onRetreat }: Props = $props();
</script>

<style lang="scss">
.boss-bar {
	display: flex;
	align-items: center;
	gap: 14px;
	background: linear-gradient(
		90deg,
		color-mix(in srgb, var(--boss-accent) 11%, transparent),
		color-mix(in srgb, var(--white) 2%, transparent) 55%
	);
	border: 1px solid color-mix(in srgb, var(--boss-accent) 33%, transparent);
	border-left: 3px solid var(--boss-accent);
	border-radius: 3px;
	padding: 9px 13px;
}

.identity {
	display: flex;
	align-items: baseline;
	gap: 10px;
	flex: 1;
	min-width: 0;
}

.boss-name {
	font-size: 15px;
	font-weight: 500;
	color: var(--text-primary);
}
</style>
