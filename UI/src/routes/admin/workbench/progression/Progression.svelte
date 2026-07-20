<div class="prog workbench" data-testid="progression-editor">
	<div class="prog-head">
		<div class="eyebrow">Admin Console · Progression</div>
		<div class="title-row">
			<h1 class="title">Paths</h1>
			<div class="summary">
				<span>{store.paths.length} {store.paths.length === 1 ? 'path' : 'paths'}</span>
				{#if store.totalChanges > 0}
					<span class="unsaved">{store.totalChanges} unsaved</span>
				{/if}
			</div>
			<div class="spacer"></div>
			<div class="view-toggle" role="group" aria-label="Progression view">
				<button
					type="button"
					class="seg"
					class:active={view === 'list'}
					aria-pressed={view === 'list'}
					onclick={() => (view = 'list')}
				>
					List
				</button>
				<button
					type="button"
					class="seg"
					class:active={view === 'map'}
					aria-pressed={view === 'map'}
					data-testid="progression-map-toggle"
					onclick={() => (view = 'map')}
				>
					Map
				</button>
			</div>
		</div>
	</div>

	{#if store.error}
		<AdminLoadError testid="progression-error" message={store.error} onRetry={() => store.load()} />
	{:else if !store.loaded}
		<div class="prog-loading"><Loading loading={true} delay={120} /></div>
	{:else}
		{#if view === 'map'}
			<ProgressionMap {store} onNavigate={() => (view = 'list')} />
		{:else}
			<div class="prog-body">
				<ProgressionList {store} />

				<div class="detail-col">
					{#if store.drilledTier}
						<TierDetail {store} />
					{:else if store.selectedPath}
						<PathDetail {store} />
					{:else}
						<div class="empty-detail" data-testid="progression-empty">
							<div class="glyph"><WorkbenchIcon kind="rune" size={22} /></div>
							<div class="et">No path selected</div>
							<button type="button" class="btn primary sm" onclick={() => store.addPath()}>
								<WorkbenchIcon kind="plus" size={12} />New path
							</button>
						</div>
					{/if}
				</div>
			</div>
		{/if}

		<SaveBar
			saved={store.saved}
			total={store.totalChanges}
			saving={store.saving}
			added={store.counts.added}
			modified={store.counts.modified}
			blocked={store.hasBlockingWarnings}
			onDiscard={() => store.discard()}
			onSave={() => store.save()}
			saveTestId="progression-save"
		/>
	{/if}
</div>

<script lang="ts">
import { onDestroy, onMount } from 'svelte';
import Loading from '$components/Loading.svelte';
import { workbenchDirty } from '../dirty.svelte';
import SaveBar from '../SaveBar.svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import '../workbench.scss';
import AdminLoadError from '../../AdminLoadError.svelte';
import { ProgressionStore } from './progression-store.svelte';
import ProgressionList from './ProgressionList.svelte';
import ProgressionMap from './ProgressionMap.svelte';
import PathDetail from './PathDetail.svelte';
import TierDetail from './TierDetail.svelte';

const store = new ProgressionStore();

let view = $state<'list' | 'map'>('list');

onMount(() => {
	void store.load();
});

onDestroy(() => {
	store.dispose();
	workbenchDirty.set(0);
});

// Surface this store's pending-change count to the admin shell's tool-switch/unload guard.
$effect(() => {
	workbenchDirty.set(store.totalChanges);
});
</script>

<style lang="scss">
.prog-head {
	padding: 20px 32px 18px;
	border-bottom: 1px solid var(--border-subtle);
	flex-shrink: 0;
}
.eyebrow {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--eyebrow);
	margin-bottom: 6px;
}
.title-row {
	display: flex;
	align-items: baseline;
	gap: 14px;
}
.view-toggle {
	align-self: center;
	display: flex;
	border: 1px solid var(--border-light);
	border-radius: 5px;
	overflow: hidden;
}
.seg {
	background: transparent;
	border: none;
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 5px 14px;
	cursor: pointer;
	transition: all 0.14s ease;

	&:hover:not(.active) {
		color: var(--text-secondary);
	}
	&.active {
		background: var(--accent);
		color: var(--text-on-accent);
	}
}
.title {
	margin: 0;
	font-size: 22px;
	font-weight: 500;
	letter-spacing: -0.2px;
}
.summary {
	display: inline-flex;
	align-items: center;
	gap: 14px;
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-tertiary);

	.unsaved {
		color: var(--text-secondary);
	}
}
.prog-loading {
	flex: 1;
	min-height: 0;
}
.prog-body {
	display: flex;
	flex: 1;
	min-height: 0;
}
.detail-col {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
}
.empty-detail {
	flex: 1;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 12px;
	color: var(--text-muted);

	.glyph {
		width: 56px;
		height: 56px;
		border-radius: 10px;
		border: 1px dashed var(--border-light);
		display: flex;
		align-items: center;
		justify-content: center;
		color: var(--text-tertiary);
	}
	.et {
		font-size: 14px;
		color: var(--text-tertiary);
	}
}
</style>
