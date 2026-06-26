<!-- The enemy dossier (right panel): a section-accented header (kind + name), a row of sub-tabs, and
     the active sub-tab's body. Sub-tab content lives in its own small panel component. -->
{#if view.selectedEnemy}
	<div class="dossier" data-testid="codex-dossier" style:--dossier-accent={view.dossierAccent}>
		<div class="head">
			<div class="kind-line">
				<span class="kind-dot"></span>
				<span class="kind">{view.dossierKind}</span>
			</div>
			<div class="name">{view.selectedEnemy.name}</div>
		</div>

		<div class="sub-tabs" role="tablist" aria-label="Enemy detail">
			{#each view.subTabs as tab (tab.key)}
				<button
					type="button"
					id="codex-subtab-{tab.key}"
					role="tab"
					aria-selected={view.sub === tab.key}
					aria-controls="codex-subpanel"
					class="sub-tab"
					class:active={view.sub === tab.key}
					data-testid="codex-subtab-{tab.key}"
					onclick={() => view.selectSub(tab.key)}
				>
					{tab.label}
					<span class="underline"></span>
				</button>
			{/each}
		</div>

		<div class="body" id="codex-subpanel" role="tabpanel" aria-labelledby="codex-subtab-{view.sub}">
			{#if view.sub === 'attributes'}
				<AttributesPanel {view} />
			{:else if view.sub === 'statistics'}
				<StatisticsPanel
					stats={view.statistics}
					loading={view.statsLoading}
					error={view.statsError}
					emptyMessage="No battles recorded against this enemy yet."
					testid="codex-enemy-stats"
				/>
			{:else if view.sub === 'skills'}
				<EnemySkillsPanel {view} />
			{:else if view.sub === 'spawns'}
				<EnemySpawnsPanel {view} />
			{:else if view.sub === 'challenges'}
				<EnemyChallengesPanel {view} />
			{/if}
		</div>
	</div>
{/if}

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
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
.dossier {
	width: 380px;
	flex: none;
	background: var(--surface);
	border-left: 1px solid var(--border-subtle);
	display: flex;
	flex-direction: column;
}

.head {
	border-left: 3px solid var(--dossier-accent);
	padding: 18px 20px 14px;
	flex: none;
}

.kind-line {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 6px;
}

.kind-dot {
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--dossier-accent);
	flex: none;
}

.kind {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--dossier-accent);
}

.name {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
	line-height: 1.05;
}

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
