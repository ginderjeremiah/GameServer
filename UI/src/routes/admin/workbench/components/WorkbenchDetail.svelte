{#if !record}
	<div class="detail-pane">
		<div class="empty-state empty-fill">
			<div class="glyph"><WorkbenchIcon kind={entity.glyph} size={22} /></div>
			<div class="et">No {entity.label.toLowerCase()} left</div>
			<button type="button" class="btn primary sm" onclick={onNew}>
				<WorkbenchIcon kind="plus" size={12} />New {entity.singular}
			</button>
		</div>
	</div>
{:else}
	{@const status = store.status(record)}
	{@const badge = entity.listBadge?.(record)}
	{@const curSection = entity.sections.find((s) => s.key === tab) ?? entity.sections[0]}
	{@const title = entity.title?.(record) || record.name}
	<div class="detail-pane">
		<DetailHeader
			idLabel={record.id < 0 ? 'new' : `#${record.id}`}
			isNew={record.id < 0}
			name={title ?? ''}
			blank={entity.blankName}
			{badge}
			badgeColor={entity.badgeColor?.(record)}
			{status}
			retireable={entity.retireable ?? false}
			retired={(entity.retireable ?? false) && store.isRetired(record)}
			headline={entity.headline?.(record)}
			tabs={entity.sections.map((section) => ({
				key: section.key,
				label: section.label,
				count: section.count?.(record) ?? null,
				dirty: sectionDirty(section),
				warn: sectionWarnings(section, record).length > 0
			}))}
			activeTab={tab}
			onTab={(key) => onTab(key)}
			onReset={() => store.resetItem(record.id)}
			onRetire={() => onRetire(record)}
			onReinstate={() => store.setRetired(record.id, false)}
			onRemove={() => store.removeItem(record.id)}
			onDelete={() => onDelete(record)}
			onRestore={() => store.restoreItem(record.id)}
		/>

		<div class="detail-body">
			<div class="body-inner" class:locked={status === 'deleted' || store.saving}>
				<div class="sec-title">
					{curSection.label}{#if curSection.desc}<span class="sub">— {curSection.desc}</span>{/if}<span class="ln"
					></span>
				</div>
				<SectionRenderer section={curSection} {record} {baseline} {store} />
			</div>
		</div>

		<SaveBar
			saved={store.saved}
			total={store.counts.total}
			saving={store.saving}
			added={store.counts.added}
			modified={store.counts.modified}
			deleted={store.counts.deleted}
			onDiscard={() => store.discard()}
			onSave={() => store.save()}
		/>
	</div>
{/if}

<script lang="ts">
import { fieldsOf, type EntityConfig, type Identified } from '../entities/types';
import type { EntityStore } from '../entity-store.svelte';
import { recordsEqual } from '../entity-store.svelte';
import {
	deleteWithConfirm,
	ownCatalogueOverride,
	referenceSourcesFromStatic,
	retireWithConfirm
} from '../retire-confirm';
import { sectionWarnings } from '../validation';
import SaveBar from '../SaveBar.svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import DetailHeader from './DetailHeader.svelte';
import SectionRenderer from './SectionRenderer.svelte';

interface Props {
	entity: EntityConfig<Identified>;
	store: EntityStore<Identified>;
	record: Identified | undefined;
	baseline: Identified | undefined;
	tab: string;
	onTab: (key: string) => void;
	onNew: () => void;
}

const { entity, store, record, baseline, tab, onTab, onNew }: Props = $props();

/**
 * Retire a saved reference record, first surfacing what currently references it. The referenced-by
 * surface is computed from the cached reference sets (no backend round-trip); an unreferenced record
 * retires without an extra prompt. Advisory only — confirming proceeds, and the record stays
 * resolvable by id either way (see references.ts).
 *
 * Every catalogue but this pane's own comes from `staticData` (last-saved): each of their reference
 * fns reads a *sibling* catalogue (an enemy is referenced by zones/challenges, an item by
 * challenges/classes, …) this pane holds no live copy of — closing that gap generically would mean
 * holding every reference-carrying entity's store alive across a tool switch (see
 * `ownCatalogueOverride`'s comment), tracked as the remaining part of #1976 rather than attempted
 * here. This pane's own catalogue is overridden with its live `store.items` for the two entity types
 * whose reference lookup reads it (enemies' own spawns, a recipe named via the skills catalogue
 * being retired from) — density-safe, since `store.items` isn't dense-by-id once a record's been
 * added this session (`EntityStore.addItem` prepends negative ids).
 */
const onRetire = (rec: Identified) =>
	retireWithConfirm({
		entityKey: entity.key,
		id: rec.id,
		name: entity.title?.(rec) || rec.name || entity.blankName,
		title: `Retire ${entity.singular}?`,
		sources: referenceSourcesFromStatic(ownCatalogueOverride(entity.key, store.items)),
		onConfirmed: () => store.setRetired(rec.id, true)
	});

/**
 * Hard-delete confirm for the one non-retireable entity (tags): surfaces what currently applies
 * the tag before the cascade strips it from every one of them (see `references.ts`'s tag case).
 */
const onDelete = (rec: Identified) =>
	deleteWithConfirm({
		entityKey: entity.key,
		id: rec.id,
		name: entity.title?.(rec) || rec.name || entity.blankName,
		title: `Delete ${entity.singular}?`,
		sources: referenceSourcesFromStatic(),
		onConfirmed: () => store.removeItem(rec.id)
	});

const sectionDirty = (section: EntityConfig<Identified>['sections'][number]): boolean => {
	if (!record || !baseline) {
		return false;
	}
	const current = fieldsOf(record);
	const base = fieldsOf(baseline);
	if (section.kind === 'fields') {
		return section.fields.some((f) => !recordsEqual(current[f.key as string], base[f.key as string]));
	}
	if ('itemsKey' in section) {
		const key = section.itemsKey as string;
		return !recordsEqual(current[key], base[key]);
	}
	if (section.dirtyKeys) {
		return section.dirtyKeys.some((key) => !recordsEqual(current[key as string], base[key as string]));
	}
	return false;
};
</script>

<style lang="scss">
.detail-pane {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
}
.empty-fill {
	height: 100%;
}
</style>
