<!-- The boss's HP bar: the shared HpBar in its tall variant, overlaid with phase pips
     at 25 / 50 / 75% to telegraph boss phases. -->
<HpBar {currentHealth} {maxHealth} ariaLabel="Boss health" tall phasePips={PHASE_PIPS} testId="boss-hp-bar" />

<script lang="ts">
import { HpBar } from '$components';

type Props = {
	currentHealth: number;
	maxHealth: number;
};

const { currentHealth, maxHealth }: Props = $props();

const PHASE_PIPS = [25, 50, 75];
</script>
