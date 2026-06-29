<!-- A small damage-type icon (the art in `static/img`, mapped by `damageTypeKeyIcon`). Decorative by
     default (`alt=""`) because every surface that shows it also renders the type's name or a damage
     number alongside; pass `alt` for a standalone use. Replaces the former inline-SVG `DamageTypeGlyph`
     (#1320 stopgap) with the same house-style art used for the amp/resist attribute icons. -->
<img class="dmg-icon" {src} {alt} style:width="{size}px" style:height="{size}px" />

<script lang="ts">
import type { EDamageTypeKey } from '$lib/api';
import { damageTypeKeyIcon } from '$lib/common';

interface Props {
	/** The damage-type key whose icon to render. */
	dmgKey: EDamageTypeKey;
	/** Rendered px size (square). */
	size?: number;
	/** Alt text; defaults to "" (decorative — a name/number label is shown alongside). */
	alt?: string;
}

const { dmgKey, size = 14, alt = '' }: Props = $props();

const src = $derived(damageTypeKeyIcon(dmgKey));
</script>

<style lang="scss">
.dmg-icon {
	display: inline-block;
	flex-shrink: 0;
	object-fit: contain;
	vertical-align: middle;
}
</style>
