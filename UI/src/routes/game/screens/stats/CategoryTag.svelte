<!-- A small pill tagging a statistic's category, tinted by the category accent. -->
<span class="tag" style:--cat-color={statCategoryColor(cat)}>{statCategoryLabel(cat)}</span>

<script lang="ts">
import type { StatCategory } from './statistics-view.svelte';
import { statCategoryColor, statCategoryLabel } from './statistics-display';

interface Props {
	cat: StatCategory;
}

let { cat }: Props = $props();
</script>

<style lang="scss">
.tag {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--cat-color);
	border: 1px solid color-mix(in srgb, var(--cat-color) 40%, transparent);
	border-radius: 2px;
	padding: 2px 6px;
	line-height: 1;
	white-space: nowrap;
}
</style>
