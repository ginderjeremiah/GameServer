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
	<div class="detail-pane">
		<div class="detail-head">
			<div class="head-row">
				<span class="rec-id" class:is-new={record.id < 0}>{record.id < 0 ? 'new' : `#${record.id}`}</span>
				<h2 class="rec-name" class:blank={!record.name}>{record.name || entity.blankName}</h2>
				{#if badge}
					<span class="rare-tag" style:color={entity.badgeColor?.(record) ?? 'var(--text-secondary)'}>{badge}</span>
				{/if}
				{#if status === 'added'}<span class="spill added">new</span>{/if}
				{#if status === 'modified'}<span class="spill modified">edited</span>{/if}
				{#if status === 'deleted'}<span class="spill deleted">removed</span>{/if}
				{#if entity.retireable && store.isRetired(record)}<span class="spill retired">retired</span>{/if}
				<div class="spacer"></div>
				{#if status === 'deleted'}
					<button type="button" class="hdr-action accent" onclick={() => store.restoreItem(record.id)}>
						<WorkbenchIcon kind="check" size={11} sw={1.7} />Restore
					</button>
				{:else}
					{#if status === 'modified'}
						<button type="button" class="hdr-action reset" onclick={() => store.resetItem(record.id)}>Reset</button>
					{/if}
					{#if entity.retireable}
						{#if record.id < 0}
							<!-- Never-saved record: drop it locally (it was never persisted, so it's not "retired"). -->
							<button type="button" class="hdr-action danger" onclick={() => store.removeItem(record.id)}>
								<WorkbenchIcon kind="x" size={11} />Remove
							</button>
						{:else if store.isRetired(record)}
							<button type="button" class="hdr-action accent" onclick={() => store.setRetired(record.id, false)}>
								<WorkbenchIcon kind="check" size={11} sw={1.7} />Reinstate
							</button>
						{:else}
							<button type="button" class="hdr-action danger" onclick={() => onRetire(record)}>
								<WorkbenchIcon kind="archive" size={11} />Retire
							</button>
						{/if}
					{:else}
						<button type="button" class="hdr-action danger" onclick={() => store.removeItem(record.id)}>
							<WorkbenchIcon kind="x" size={11} />Delete
						</button>
					{/if}
				{/if}
			</div>

			{#if entity.headline}
				<div class="detail-headline">{entity.headline(record)}</div>
			{/if}

			<div class="tabs">
				{#each entity.sections as section (section.key)}
					{@const count = section.count?.(record) ?? null}
					{@const incomplete = sectionWarnings(section, record).length > 0}
					{@const dirty = sectionDirty(section)}
					<button type="button" class="tab" class:active={tab === section.key} onclick={() => onTab(section.key)}>
						{section.label}
						{#if count !== null}<span class="count-badge">{count}</span>{/if}
						{#if incomplete}<WarnTriangle size={12} />{/if}
						{#if dirty}<span class="tab-dot" aria-hidden="true"></span><span class="sr-only">unsaved changes</span>{/if}
					</button>
				{/each}
			</div>
		</div>

		<div class="detail-body">
			<div class="body-inner" class:locked={status === 'deleted'}>
				<div class="sec-title">
					{curSection.label}{#if curSection.desc}<span class="sub">— {curSection.desc}</span>{/if}<span class="ln"
					></span>
				</div>
				<SectionRenderer section={curSection} {record} {baseline} {store} />
			</div>
		</div>

		<div class="save-bar">
			<div class="save-summary">
				{#if store.saved}
					<span class="saved"><WorkbenchIcon kind="check" size={13} sw={1.7} />Changes saved</span>
				{:else if store.counts.total === 0}
					<span>No unsaved changes</span>
				{:else}
					<span class="pending">{store.counts.total} unsaved {store.counts.total === 1 ? 'change' : 'changes'}</span>
					<span class="pips">
						{#if store.counts.added > 0}<span class="pip added"
								><span class="dot"></span>{store.counts.added} added</span
							>{/if}
						{#if store.counts.modified > 0}<span class="pip modified"
								><span class="dot"></span>{store.counts.modified} edited</span
							>{/if}
						{#if store.counts.deleted > 0}<span class="pip deleted"
								><span class="dot"></span>{store.counts.deleted} removed</span
							>{/if}
					</span>
				{/if}
			</div>
			<div class="save-actions">
				<button
					type="button"
					class="btn"
					disabled={store.counts.total === 0 || store.saving}
					onclick={() => store.discard()}
				>
					Discard
				</button>
				<button
					type="button"
					class="btn primary"
					disabled={store.counts.total === 0 || store.saving}
					onclick={() => store.save()}
				>
					Save Changes
				</button>
			</div>
		</div>
	</div>
{/if}

<script lang="ts">
import { dangerModal, staticData } from '$stores';
import { fieldsOf, type EntityConfig, type Identified } from '../entities/types';
import type { EntityStore } from '../entity-store.svelte';
import { recordsEqual } from '../entity-store.svelte';
import { computeReferences, formatReferenceBody } from '../references';
import { sectionWarnings } from '../validation';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import SectionRenderer from './SectionRenderer.svelte';
import WarnTriangle from './WarnTriangle.svelte';

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
 */
const onRetire = async (rec: Identified) => {
	const groups = computeReferences(entity.key, rec.id, {
		enemies: staticData.enemies ?? [],
		zones: staticData.zones ?? [],
		challenges: staticData.challenges ?? []
	});
	if (groups.length > 0) {
		const confirmed = await dangerModal({
			title: `Retire ${entity.singular}?`,
			body: formatReferenceBody(entity.key, rec.name || entity.blankName, groups),
			confirmLabel: 'Retire anyway'
		});
		if (!confirmed) {
			return;
		}
	}
	store.setRetired(rec.id, true);
};

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
.detail-head {
	padding: 18px 32px 0;
}
.head-row {
	display: flex;
	align-items: center;
	gap: 12px;
}
.spacer {
	flex: 1;
}
.rec-id {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);

	&.is-new {
		color: var(--accent);
	}
}
.rec-name {
	margin: 0;
	font-size: 22px;
	font-weight: 500;
	color: var(--text-primary);

	&.blank {
		color: var(--text-muted);
	}
}
.detail-headline {
	margin-top: 8px;
	font-size: 14px;
	color: var(--text-secondary);
	letter-spacing: -0.1px;
}
.tabs {
	display: flex;
	gap: 4px;
	margin-top: 16px;
	border-bottom: 1px solid var(--border-subtle);
	flex-wrap: wrap;
}
.detail-body {
	flex: 1;
	overflow-y: auto;
	padding: 24px 32px;
}
.body-inner {
	max-width: 1020px;

	&.locked {
		opacity: 0.5;
		pointer-events: none;
	}
}
.pips {
	display: inline-flex;
	gap: 12px;
}
.sr-only {
	position: absolute;
	width: 1px;
	height: 1px;
	margin: -1px;
	padding: 0;
	overflow: hidden;
	clip: rect(0, 0, 0, 0);
	white-space: nowrap;
	border: 0;
}
</style>
