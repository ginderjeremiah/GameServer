<!-- A small attribute icon (the art in `static/img`, mapped by `attributeIcon`). Decorative by
     default (`alt=""`) because every surface that shows it also renders the attribute's name or
     code as text alongside; pass `alt` for a standalone use. Renders nothing for an attribute
     without art (the obsolete `DropBonus` and the typed DoT accumulators), so callers don't need
     their own guard. -->
{#if src}
	<img class="attr-icon" {src} {alt} style:width="{size}px" style:height="{size}px" />
{/if}

<script lang="ts">
import type { EAttribute } from '$lib/api';
import { attributeIcon } from '$lib/common';

interface Props {
	id: EAttribute;
	/** Rendered px size (square). */
	size?: number;
	/** Alt text; defaults to "" (decorative — a name/code label is shown alongside). */
	alt?: string;
}

const { id, size = 16, alt = '' }: Props = $props();

const src = $derived(attributeIcon(id));
</script>

<style lang="scss">
.attr-icon {
	display: inline-block;
	flex-shrink: 0;
	object-fit: contain;
	vertical-align: middle;
}
</style>
