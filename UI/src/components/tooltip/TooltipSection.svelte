<div class="tt-section" class:last>
	<div class="tt-section-header">
		<span>{label}</span>
		<div class="tt-section-line"></div>
	</div>
	{@render children?.()}
</div>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	label: string;
	last?: boolean;
	children?: Snippet;
}

const { label, last = false, children }: Props = $props();
</script>

<style lang="scss">
.tt-section {
	margin-bottom: 12px;

	&.last {
		margin-bottom: 0;
	}
}

.tt-section-header {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 7px;
	display: flex;
	align-items: center;
	gap: 8px;
}

.tt-section-line {
	flex: 1;
	height: 1px;
	background: color-mix(in srgb, var(--text-primary) 6%, transparent);
}
</style>
