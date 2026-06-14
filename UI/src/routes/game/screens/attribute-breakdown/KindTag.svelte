<!-- A small pill tagging an attribute by its display taxonomy (Primary / Secondary / Status). -->
<span
	class="kind-tag"
	class:secondary={type === EAttributeType.Secondary}
	class:status={type === EAttributeType.Status}
>
	{label}
</span>

<script lang="ts">
import { EAttributeType } from '$lib/api';
import { ATTRIBUTE_TYPE_GROUPS } from './attribute-breakdown-view.svelte';

interface Props {
	type: EAttributeType;
}

let { type }: Props = $props();

const label = $derived(ATTRIBUTE_TYPE_GROUPS.find((g) => g.type === type)?.label ?? '');
</script>

<style lang="scss">
.kind-tag {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--accent);
	border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent);
	border-radius: 2px;
	padding: 2px 6px;
	line-height: 1;
	white-space: nowrap;

	&.secondary {
		color: var(--warning);
		border-color: color-mix(in srgb, var(--warning) 40%, transparent);
	}

	&.status {
		color: var(--text-muted);
		border-color: color-mix(in srgb, var(--text-muted) 40%, transparent);
	}
}
</style>
