<div class="battler-card round-border">
	<div class="battler-info">
		<span>{battler?.name ?? ''}</span>
		<span>{`Level: ${battler?.level ?? 1}`}</span>
	</div>
	<div class="health-display">
		<label for={healthId}>Health: </label>
		<div class="health-meter round-border" style={`--health-perc: ${healthPerc}%;`}>
			<div class="health-layer health-remaining"></div>
			<div class="health-layer health-disappearing"></div>
			<span>{healthText}</span>
		</div>
	</div>
	<Skills {battler} />
</div>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { type Battler } from '$lib/battle';
import { formatNum } from '$lib/common';
import Skills from './Skills.svelte';

type Props = {
	battler: Battler | undefined;
};

const { battler }: Props = $props();

const currentHealth = $derived(battler?.currentHealth ?? 0);
const maxHealth = $derived(battler?.attributes.getValue(EAttribute.MaxHealth));
const healthText = $derived(`${formatNum(currentHealth)}/${maxHealth ?? 0}`);
const healthPerc = $derived(
	maxHealth ? formatNum(Math.max((currentHealth * 100) / maxHealth, 0)) : 100
);
const healthId = crypto.randomUUID();
</script>

<style lang="scss">
.battler-card {
	width: 30vw;
	background-color: var(--container-background-color);
	font-size: 1.25rem;
	padding: 0.5rem;
	border: var(--default-border);

	> :not(:last-child) {
		margin-bottom: 1rem;
	}

	.battler-info {
		user-select: text;
		display: flex;
		justify-content: space-between;
		font-size: 1.5rem;
	}

	.health-display {
		user-select: text;

		.health-meter {
			position: relative;
			border: var(--default-border);
			background-color: var(--health-missing-color);
			padding: 0.25rem;
			z-index: 1;
			overflow: hidden;

			span {
				position: relative;
				z-index: 10;
			}

			.health-layer {
				position: absolute;
				top: 0;
				left: 0;
				height: 100%;
				width: var(--health-perc);

				&.health-remaining {
					z-index: 3;
					background-color: var(--health-remaining-color);
					transition: width 0.1s ease-out;
				}

				&.health-disappearing {
					z-index: 2;
					background-color: var(--health-disappearing-color);
					transition: width 1s ease-out;
				}
			}
		}
	}
}
</style>
