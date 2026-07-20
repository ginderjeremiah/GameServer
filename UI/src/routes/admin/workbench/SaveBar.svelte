<div class="save-bar">
	<div class="save-summary">
		{#if saved}
			<span class="saved"><WorkbenchIcon kind="check" size={13} sw={1.7} />Changes saved</span>
		{:else if total === 0}
			<span>No unsaved changes</span>
		{:else}
			<span class="pending">{total} unsaved {total === 1 ? 'change' : 'changes'}</span>
			<span class="pips">
				{#if added > 0}<span class="pip added"><span class="dot"></span>{added} added</span>{/if}
				{#if modified > 0}<span class="pip modified"><span class="dot"></span>{modified} edited</span>{/if}
				{#if deleted}<span class="pip deleted"><span class="dot"></span>{deleted} removed</span>{/if}
			</span>
			{#if blocked}
				<span class="blocked-note"><WarnTriangle size={13} />Resolve blocking warnings before saving</span>
			{/if}
		{/if}
	</div>
	<div class="save-actions">
		<button type="button" class="btn" disabled={total === 0 || saving} onclick={onDiscard}>Discard</button>
		<button
			type="button"
			class="btn primary"
			data-testid={saveTestId}
			disabled={total === 0 || saving || blocked}
			onclick={onSave}
		>
			Save Changes
		</button>
	</div>
</div>

<script lang="ts">
import WarnTriangle from './components/WarnTriangle.svelte';
import WorkbenchIcon from './WorkbenchIcon.svelte';

interface Props {
	saved: boolean;
	total: number;
	saving: boolean;
	added: number;
	modified: number;
	deleted?: number;
	/** True when a pending record carries a save-blocking warning — disables Save alongside the
	 *  existing total/saving gates and surfaces why. Defaults to false for callers with no concept
	 *  of blocking warnings (e.g. the progression editor's own advisory-only warnings). */
	blocked?: boolean;
	onDiscard: () => void;
	onSave: () => void;
	saveTestId?: string;
}

const {
	saved,
	total,
	saving,
	added,
	modified,
	deleted = 0,
	blocked = false,
	onDiscard,
	onSave,
	saveTestId
}: Props = $props();
</script>

<style lang="scss">
.pips {
	display: inline-flex;
	gap: 12px;
}
.blocked-note {
	display: inline-flex;
	align-items: center;
	gap: 5px;
	margin-left: 12px;
	color: var(--warning);
}
</style>
