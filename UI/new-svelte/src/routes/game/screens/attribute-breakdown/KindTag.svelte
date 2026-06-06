<!-- A small pill tagging an attribute as Core or Derived. -->
<span class="kind-tag" class:derived={group === 'derived'}>{label}</span>

<script lang="ts">
import type { AttributeGroup } from './attribute-breakdown-view.svelte';

interface Props {
	group: AttributeGroup;
}

let { group }: Props = $props();

const label = $derived(group === 'core' ? 'Core' : 'Derived');
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

	&.derived {
		color: var(--warning);
		border-color: color-mix(in srgb, var(--warning) 40%, transparent);
	}
}
</style>
