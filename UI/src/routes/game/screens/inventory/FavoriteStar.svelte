<!-- The shared favorite-star glyph used by the inventory grid slot and toolbar filter.
     Fills with the accessory accent when `filled`; the stroke colour is overridable. -->
<svg {width} {height} viewBox="0 0 16 16" style:fill={filled ? STAR_ACCENT : 'none'} style:stroke stroke-width="1.3">
	<path d="M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z" stroke-linejoin="round" />
</svg>

<script lang="ts">
const STAR_ACCENT = 'var(--category-accessory)';

interface Props {
	filled?: boolean;
	size?: number;
	/** Stroke colour; defaults to the accessory accent. */
	stroke?: string;
}

const { filled = false, size = 12, stroke = STAR_ACCENT }: Props = $props();
const width = $derived(size);
const height = $derived(size);
</script>
