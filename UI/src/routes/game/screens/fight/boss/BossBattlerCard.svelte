<!-- The heightened gold "boss card" that replaces the normal enemy BattlerCard while
     a dedicated boss is engaged: a Zone-Boss ribbon, gold chrome, the phase-pip HP
     bar, and the boss's full skill loadout (glowing in the boss accent). -->
<div class="boss-card" data-testid="boss-card">
	<CombatFloaters side="enemy" testId="boss-floaters" />

	<div class="ribbon">
		<BossKicker>Zone Boss</BossKicker>
		<BossDiamond size={8} />
	</div>

	<div class="identity">
		<span class="boss-name">{battler.name}</span>
		<span class="boss-level">LV · {battler.level}</span>
	</div>

	<BossHpBar currentHealth={battler.currentHealth} {maxHealth} />

	<div class="skills">
		<Skills {battler} side="enemy" accent="var(--boss-accent)" />
	</div>

	<!-- Active timed effects float below the card (absolutely positioned) so effects coming and
	     going never change the card's height — which would shift the vertically-centred combatants row. -->
	<div class="effect-chips-slot">
		<ActiveEffectChips {battler} reversed />
	</div>
</div>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { type Battler } from '$lib/battle';
import ActiveEffectChips from '../ActiveEffectChips.svelte';
import CombatFloaters from '../CombatFloaters.svelte';
import Skills from '../Skills.svelte';
import BossKicker from './BossKicker.svelte';
import BossDiamond from './BossDiamond.svelte';
import BossHpBar from './BossHpBar.svelte';

type Props = {
	battler: Battler;
};

const { battler }: Props = $props();

const maxHealth = $derived(battler.attributes.getValue(EAttribute.MaxHealth));
</script>

<style lang="scss">
.boss-card {
	position: relative;
	width: 392px;
	min-width: 200px;
	flex-shrink: 1;
	color: var(--text-primary);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--boss-accent) 6%, transparent),
		color-mix(in srgb, var(--white) 2%, transparent) 40%
	);
	border: 1px solid color-mix(in srgb, var(--boss-accent) 40%, transparent);
	border-right: 3px solid var(--boss-accent);
	border-radius: 3px;
	padding: 16px 20px 18px;
	box-shadow:
		0 0 0 1px color-mix(in srgb, var(--black) 45%, transparent),
		0 0 34px color-mix(in srgb, var(--boss-accent) 15%, transparent);
}

.ribbon {
	display: flex;
	align-items: center;
	justify-content: flex-end;
	gap: 9px;
	margin-bottom: 12px;
}

.identity {
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	gap: 2px;
	margin-bottom: 14px;
}

.boss-name {
	font-size: 21px;
	font-weight: 600;
	letter-spacing: -0.2px;
}

.boss-level {
	font-family: var(--mono);
	font-size: 10px;
	color: color-mix(in srgb, var(--text-primary) 55%, transparent);
	letter-spacing: 0.6px;
}

.skills {
	margin-top: 16px;
}

// Anchored to the card's bottom edge and inset to the content padding, so the effect tiles line up
// under the skill row without occupying card height.
.effect-chips-slot {
	position: absolute;
	top: 100%;
	left: 20px;
	right: 20px;
	padding-top: 12px;
}
</style>
