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
		onRetire={() => onRetire(path)}
		onReinstate={() => store.retirePath(path.id, false)}
		onRemove={() => store.removePath(path.id)}
	/>

	<div class="detail-body" class:locked={store.isRetired(path) || store.saving}>
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
				<div class="mt16">
					<ProgInput
						label="Designer notes"
						value={path.designerNotes}
						textarea
						fullWidth
						onChange={(v) => store.patchPath(path.id, (d) => (d.designerNotes = v))}
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
import { denseByLiveId, referenceSourcesFromStatic, retireWithConfirm } from '../retire-confirm';
import type { ProgressionStore, PathTab } from './progression-store.svelte';
import { activityKeyGroups, hasTierCollision, pathWarnings } from './progression-helpers';
import type { WorkbenchPath } from './types';
import DetailHeader from './DetailHeader.svelte';
import ProgInput from './ProgInput.svelte';
import ProgSelect from './ProgSelect.svelte';
import TiersSpine from './TiersSpine.svelte';

interface Props {
	store: ProgressionStore;
}

const { store }: Props = $props();

/**
 * Retire a path, first surfacing any live gateway one of its tiers would soft-lock — the same
 * check the backend guard (`AdminPaths.FindRetiredPathGatingLiveGateway`) rejects the save on.
 * Previously bypassed the confirm entirely (#1863), unlike every other retire path.
 *
 * Overrides both `proficiencies` and `paths` with this session's live (unsaved-edits-included)
 * copies — `pathReferences` reads `store.paths`' own `retiredAt` to decide whether a gating path
 * is still live (#2099), not just `store.profs` for the tiers themselves. `paths` needs the
 * dense-by-id rebuild since `addPath` prepends unsaved paths with negative ids, unlike `profs`
 * which `pathReferences`/`proficiencyReferences` only ever `.filter()`, never index by id.
 */
const onRetire = (rec: WorkbenchPath) =>
	retireWithConfirm({
		entityKey: 'paths',
		id: rec.id,
		name: rec.name || 'Unnamed path',
		title: 'Retire path?',
		sources: referenceSourcesFromStatic({ proficiencies: store.profs, paths: denseByLiveId(store.paths) }),
		onConfirmed: () => store.retirePath(rec.id, true)
	});

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
