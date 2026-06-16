<!-- A rarity pip + label (e.g. "● Common"), tinted by the item's rarity tier. Shared by the
     inventory equip slots and the item drawer header; callers position it via passthrough props
     (e.g. style="margin-left: auto"). -->
<span class="rarity-tag" {...rest}>
	<span class="rarity-dot" style:background={color} style:box-shadow="0 0 6px {rarityTint(rarityId, 0.65)}"></span>
	<span class="rarity-label" style:color>{rarityLabel(rarityId)}</span>
</span>

<script lang="ts">
import type { HTMLAttributes } from 'svelte/elements';
import { rarityColor, rarityLabel, rarityTint } from '$lib/common';

interface Props extends HTMLAttributes<HTMLSpanElement> {
	rarityId: number;
}

const { rarityId, ...rest }: Props = $props();

const color = $derived(rarityColor(rarityId));
</script>

<style lang="scss">
.rarity-tag {
	display: inline-flex;
	align-items: center;
	gap: 6px;
}

.rarity-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	flex-shrink: 0;
}

.rarity-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
}
</style>
