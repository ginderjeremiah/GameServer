<!-- A monospace attribute pill (icon + code) tinted by the attribute's colour. Opens the shared
     attribute tooltip on hover whenever a screen provides a controller via `setAttributeTooltip`;
     otherwise it is purely presentational. -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<span
	class="achip"
	class:wide
	style:--ac={attributeColor(attributeId)}
	onmouseenter={(e) => attrTip?.show(attributeId, e)}
	onmousemove={(e) => attrTip?.move(e)}
	onmouseleave={() => attrTip?.hide()}
>
	<AttributeIcon id={attributeId} size={iconSize} />
	{attributeCode(attributeId, staticData.attributes)}
</span>

<script lang="ts">
import type { EAttribute } from '$lib/api';
import { attributeCode, attributeColor } from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import { getAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';

interface Props {
	attributeId: EAttribute;
	iconSize?: number;
	/** Centred, min-width variant for chips that align in a column (the skill damage breakdown). */
	wide?: boolean;
}

const { attributeId, iconSize = 12, wide = false }: Props = $props();

const attrTip = getAttributeTooltip();
</script>

<style lang="scss">
.achip {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	padding: 2px 8px;
	border: 1px solid color-mix(in srgb, var(--ac) 38%, transparent);
	border-radius: 3px;
	background: color-mix(in srgb, var(--ac) 12%, transparent);
	color: var(--ac);
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	white-space: nowrap;

	&.wide {
		justify-content: center;
		min-width: 46px;
		text-align: center;
	}
}
</style>
