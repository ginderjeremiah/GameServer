<span
	class="tag-pill"
	style:color={color.fg}
	style:border-color={color.bd}
	style:background={color.bg}
	style:box-shadow={isNew ? `inset 0 0 0 1px ${color.bd}` : 'none'}
>
	<span class="tdot" style:background={color.fg}></span>
	{tag.name}
	{#if onRemove}
		<span
			class="x"
			role="button"
			tabindex="0"
			title="Remove"
			onclick={(e) => (e.stopPropagation(), onRemove?.())}
			onkeydown={(e) => (e.key === 'Enter' || e.key === ' ') && (e.preventDefault(), onRemove?.())}
		>
			<WorkbenchIcon kind="x" size={10} />
		</span>
	{/if}
</span>

<script lang="ts">
import type { ITag } from '$lib/api';
import { reference } from '../reference.svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';

interface Props {
	tag: ITag;
	isNew?: boolean;
	onRemove?: () => void;
}

const { tag, isNew = false, onRemove }: Props = $props();
const color = $derived(reference.tagColor(tag.tagCategoryId));
</script>
