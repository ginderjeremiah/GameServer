{#if path}
	<DetailHeader
		idLabel={path.id < 0 ? 'new' : `#${path.id}`}
		isNew={path.id < 0}
		name={path.name}
		blank="Unnamed path"
		status={store.pathStatus(path)}
		retired={store.isRetired(path)}
		headline={path.description}
		tabs={[
			{ key: 'identity', label: 'Identity', warn: identityWarn },
			{ key: 'tiers', label: 'Tiers', count: tiers.length, dirty: tiersDirty, warn: collision }
		]}
		activeTab={store.pathTab}
		onTab={(k) => store.setPathTab(k as PathTab)}
		onReset={store.pathStatus(path) === 'modified' ? () => store.resetPath(path.id) : undefined}
		onRetire={() => store.retirePath(path.id, true)}
		onReinstate={() => store.retirePath(path.id, false)}
		onRemove={() => store.removePath(path.id)}
	/>

	<div class="detail-body" class:locked={store.isRetired(path)}>
		<div class="body-inner">
			{#if store.pathTab === 'identity'}
				<div class="sec-title">Identity<span class="sub">— the path-level record</span><span class="ln"></span></div>
				<div class="row gap16">
					<ProgInput
						label="Path name"
						value={path.name}
						grow
						warn={!path.name.trim()}
						onChange={(v) => store.patchPath(path.id, (d) => (d.name = v))}
					/>
					<ProgSelect
						label="Activity key"
						width={240}
						value={path.activityKey}
						onChange={(v) => store.patchPath(path.id, (d) => (d.activityKey = v))}
						groups={activityKeyGroups}
					/>
				</div>
				<div class="mt16">
					<ProgInput
						label="Description"
						value={path.description}
						textarea
						fullWidth
						onChange={(v) => store.patchPath(path.id, (d) => (d.description = v))}
					/>
				</div>
			{:else if store.pathTab === 'tiers'}
				<div class="sec-title">
					Tiers<span class="sub">— ordered proficiencies · reorder with ↑ ↓ · open ›</span><span class="ln"></span>
				</div>
				<TiersSpine {store} pathId={path.id} />
			{/if}
		</div>
	</div>
{/if}

<script lang="ts">
import type { ProgressionStore, PathTab } from './progression-store.svelte';
import { activityKeyGroups, hasTierCollision, pathWarnings } from './progression-helpers';
import DetailHeader from './DetailHeader.svelte';
import ProgInput from './ProgInput.svelte';
import ProgSelect from './ProgSelect.svelte';
import TiersSpine from './TiersSpine.svelte';

interface Props {
	store: ProgressionStore;
}

const { store }: Props = $props();

const path = $derived(store.selectedPath);
const baseline = $derived(path ? store.pathBaseline(path.id) : undefined);
const tiers = $derived(store.currentTiers);

const identityWarn = $derived(!!path && pathWarnings(path).length > 0);
const collision = $derived(hasTierCollision(tiers));
const tiersDirty = $derived(!!baseline && tiers.some((t) => store.profStatus(t) !== 'clean'));
</script>

<style lang="scss">
.detail-body {
	flex: 1;
	overflow-y: auto;
	padding: 24px 32px;

	&.locked {
		opacity: 0.55;
		pointer-events: none;
	}
}
.body-inner {
	max-width: 1020px;
}
.sec-title {
	display: flex;
	align-items: center;
	gap: 10px;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin: 0 0 18px;

	.sub {
		text-transform: none;
		letter-spacing: 0;
		font-family: var(--sans);
		font-size: 12px;
		white-space: nowrap;
	}
	.ln {
		flex: 1;
		height: 1px;
		background: var(--border-subtle);
	}
}
.row {
	display: flex;
}
.gap16 {
	gap: 16px;
}
.mt16 {
	margin-top: 18px;
}
</style>
