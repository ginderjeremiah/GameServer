<!-- The affordance slot between the zone nav and the arena: the in-fight boss bar
     while engaged, otherwise the always-available Challenge trigger. -->
{#if view.engaged}
	<BossBar
		bossName={view.bossName}
		autoFight={view.autoFight}
		onToggleAutoFight={(on) => view.toggleAutoFight(on)}
		onRetreat={() => view.retreat()}
	/>
{:else}
	<BossTrigger
		bossName={view.bossName}
		bossLevel={view.bossLevel}
		cleared={view.cleared}
		autoFight={view.autoFight}
		onChallenge={() => view.challenge()}
		onToggleAutoFight={(on) => view.toggleAutoFight(on)}
	/>
{/if}

<script lang="ts">
import type { BossView } from './boss-view.svelte';
import BossTrigger from './BossTrigger.svelte';
import BossBar from './BossBar.svelte';

type Props = {
	view: BossView;
};

const { view }: Props = $props();
</script>
