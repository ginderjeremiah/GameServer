<div class="empty-state">
	<div class="glyph"><WorkbenchIcon kind={icon} size={20} /></div>
	<div>
		<div class="et">{title}</div>
		<div class="es">{sub}</div>
	</div>
	{#if onAdd && addLabel}
		<button
			type="button"
			class="btn sm"
			onclick={onAdd}
			disabled={noFree}
			title={noFree ? 'Every option is already assigned' : undefined}
		>
			<WorkbenchIcon kind="plus" size={12} />{addLabel}
		</button>
	{/if}
</div>

<script lang="ts">
import WorkbenchIcon, { type WorkbenchIconKind } from '../WorkbenchIcon.svelte';

interface Props {
	icon: WorkbenchIconKind;
	title: string;
	sub: string;
	addLabel?: string;
	onAdd?: () => void;
	noFree?: boolean;
}

const { icon, title, sub, addLabel, onAdd, noFree = false }: Props = $props();
</script>
