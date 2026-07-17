<!-- The enemy dossier (right panel): a section-accented header (kind + name), a row of sub-tabs, and
     the active sub-tab's body. Sub-tab content lives in its own small panel component. -->
<DossierShell
	selectedItem={view.enemiesTab.selectedEnemy}
	testid="codex-dossier"
	accent={view.enemiesTab.dossierAccent}
	kind={view.enemiesTab.dossierKind}
	name={view.enemiesTab.selectedEnemy?.name ?? ''}
>
	<div class="sub-tabs" role="tablist" aria-label="Enemy detail">
		{#each view.enemiesTab.subTabs as tab (tab.key)}
			<button
				type="button"
				id="codex-subtab-{tab.key}"
				role="tab"
				aria-selected={view.enemiesTab.sub === tab.key}
				aria-controls="codex-subpanel"
				class="sub-tab"
				class:active={view.enemiesTab.sub === tab.key}
				data-testid="codex-subtab-{tab.key}"
				onclick={() => view.enemiesTab.selectSub(tab.key)}
			>
				{tab.label}
				<span class="underline"></span>
			</button>
		{/each}
	</div>

	<div class="body" id="codex-subpanel" role="tabpanel" aria-labelledby="codex-subtab-{view.enemiesTab.sub}">
		{#if view.enemiesTab.sub === 'attributes'}
			<AttributesPanel {view} />
		{:else if view.enemiesTab.sub === 'statistics'}
			<StatisticsPanel
				stats={view.enemiesTab.statistics}
				loading={view.statsLoading}
				error={view.statsError}
				emptyMessage="No battles recorded against this enemy yet."
				testid="codex-enemy-stats"
			/>
		{:else if view.enemiesTab.sub === 'skills'}
			<EnemySkillsPanel {view} />
		{:else if view.enemiesTab.sub === 'spawns'}
			<EnemySpawnsPanel {view} />
		{:else if view.enemiesTab.sub === 'challenges'}
			<EnemyChallengesPanel {view} />
		{/if}
	</div>
</DossierShell>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import DossierShell from './DossierShell.svelte';
import AttributesPanel from './AttributesPanel.svelte';
import StatisticsPanel from './StatisticsPanel.svelte';
import EnemySkillsPanel from './EnemySkillsPanel.svelte';
import EnemySpawnsPanel from './EnemySpawnsPanel.svelte';
import EnemyChallengesPanel from './EnemyChallengesPanel.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.sub-tabs {
	display: flex;
	gap: 2px;
	padding: 0 18px;
	border-bottom: 1px solid var(--border-subtle);
	flex: none;
}

.sub-tab {
	position: relative;
	background: transparent;
	border: none;
	cursor: pointer;
	padding: 3px 7px 10px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);

	&.active {
		color: var(--text-primary);

		.underline {
			background: var(--dossier-accent);
		}
	}
}

.underline {
	position: absolute;
	left: 0;
	right: 0;
	bottom: -1px;
	height: 2px;
	background: transparent;
}

.body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px 20px 20px;
}
</style>
