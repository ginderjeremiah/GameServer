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
		<button
			type="button"
			class="x"
			title="Remove"
			aria-label="Remove tag {tag.name}"
			onclick={stopPropagation(() => onRemove?.())}
		>
			<WorkbenchIcon kind="x" size={10} />
		</button>
	{/if}
</span>

<script lang="ts">
import type { ITag } from '$lib/api';
import { reference } from '../reference.svelte';
import { stopPropagation } from '$lib/common/event-wrappers';
import WorkbenchIcon from '../WorkbenchIcon.svelte';

interface Props {
	tag: ITag;
	isNew?: boolean;
	onRemove?: () => void;
}

const { tag, isNew = false, onRemove }: Props = $props();
const color = $derived(reference.tagColor(tag.tagCategoryId));
</script>
