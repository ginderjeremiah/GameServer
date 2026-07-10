{#if tier}
	<DetailHeader
		idLabel={tier.id < 0 ? 'new' : `#${tier.id}`}
		isNew={tier.id < 0}
		name={tier.name}
		blank="Unnamed tier"
		status={store.profStatus(tier)}
		retired={store.isRetired(tier)}
		headline={`${store.selectedPath?.name ?? ''} · Tier ${tier.pathOrdinal} · max level ${tier.maxLevel}`}
		tabs={[
			{ key: 'identity', label: 'Identity · Conlang', warn: identityWarn },
			{ key: 'xp', label: 'XP Curve', warn: xpWarn },
			{ key: 'milestones', label: 'Milestones', count: payoutCount, dirty: milestonesDirty, warn: milestoneWarn },
			{
				key: 'gateways',
				label: 'Gateways',
				count: tier.prerequisiteIds.length,
				disabled: !isRoot && tier.prerequisiteIds.length === 0
			}
		]}
		activeTab={store.tierTab}
		onTab={(k) => store.setTierTab(k as TierTab)}
		onReset={store.profStatus(tier) === 'modified' ? () => store.resetProf(tier.id) : undefined}
		onRetire={() => onRetire(tier)}
		onReinstate={() => store.retireProf(tier.id, false)}
		onRemove={() => store.removeTier(tier.id)}
	>
		{#snippet crumb()}
			<div class="crumb">
				<button type="button" class="crumb-link" onclick={() => store.back()}
					>{store.selectedPath?.name ?? 'Path'}</button
				>
				<span class="crumb-sep">›</span> Tier {tier?.pathOrdinal}
			</div>
		{/snippet}
	</DetailHeader>

	<div class="detail-body" class:locked={store.isRetired(tier) || store.saving}>
		<div class="body-inner">
			{#if store.tierTab === 'identity'}
				<div class="sec-title">
					Identity · Conlang<span class="sub">— name and words of power</span><span class="ln"></span>
				</div>
				<ConlangIdentity {store} {tier} />
			{:else if store.tierTab === 'xp'}
				<div class="sec-title">
					XP Curve<span class="sub">— max level and the cost curve</span><span class="ln"></span>
				</div>
				<XpCurve {store} {tier} />
			{:else if store.tierTab === 'milestones'}
				<div class="sec-title">
					Milestones<span class="sub">— click a level to author its payout · only payout levels are stored</span><span
						class="ln"
					></span>
				</div>
				<MilestonesEditor {store} {tier} />
			{:else if store.tierTab === 'gateways'}
				<div class="sec-title">
					Gateways<span class="sub">— how this path is unlocked</span><span class="ln"></span>
				</div>
				<GatewaysEditor {store} {tier} />
			{/if}
		</div>
	</div>
{/if}

<script lang="ts">
import { childChanged } from '../save-helpers';
import { referenceSourcesFromStatic, retireWithConfirm } from '../retire-confirm';
import type { ProgressionStore, TierTab } from './progression-store.svelte';
import { payoutLevels, proficiencyWarnings } from './progression-helpers';
import DetailHeader from './DetailHeader.svelte';
import ConlangIdentity from './ConlangIdentity.svelte';
import XpCurve from './XpCurve.svelte';
import MilestonesEditor from './MilestonesEditor.svelte';
import GatewaysEditor from './GatewaysEditor.svelte';
import type { WorkbenchProficiency } from './types';

interface Props {
	store: ProgressionStore;
}

const { store }: Props = $props();

const tier = $derived(store.drilledTier);

/**
 * Retire a tier, first surfacing what references it — a gear gate (item `requiredProficiencyId`), a
 * synthesis-recipe condition, or another tier's cross-path prerequisite. The progression editor isn't
 * a generic `EntityConfig`, so it can't go through `WorkbenchDetail`'s onRetire; this mirrors it.
 */
const onRetire = (rec: WorkbenchProficiency) =>
	retireWithConfirm({
		entityKey: 'proficiencies',
		id: rec.id,
		name: rec.name || 'Unnamed tier',
		title: 'Retire tier?',
		sources: referenceSourcesFromStatic(),
		onConfirmed: () => store.retireProf(rec.id, true)
	});
const baseline = $derived(tier ? store.profBaseline(tier.id) : undefined);
const isRoot = $derived(tier?.pathOrdinal === 0);
const payoutCount = $derived(tier ? payoutLevels(tier).length : 0);

const identityWarn = $derived(
	!!tier &&
		(!tier.name.trim() ||
			!tier.word.trim() ||
			!tier.pronunciation.trim() ||
			!tier.translation.trim() ||
			!tier.iconPath.trim())
);
const xpWarn = $derived(!!tier && (tier.maxLevel < 1 || !(tier.baseXp > 0) || !(tier.xpGrowth > 0)));
const milestoneWarn = $derived(
	!!tier && proficiencyWarnings(tier).some((w) => w.includes('level') && w.includes('out of range'))
);
const milestonesDirty = $derived(
	!!baseline &&
		(childChanged(tier?.levelModifiers, baseline.levelModifiers) ||
			childChanged(tier?.levelRewards, baseline.levelRewards))
);
</script>

<style lang="scss">
.crumb {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);
	margin-bottom: 10px;
}
.crumb-link {
	background: none;
	border: none;
	color: var(--accent);
	cursor: pointer;
	font: inherit;
	padding: 0;
}
.crumb-sep {
	color: var(--text-muted);
}
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
</style>
