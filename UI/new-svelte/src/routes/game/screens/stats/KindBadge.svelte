<!-- An entity-kind badge: glyph + kind label, for statistics that break down by
     an entity. Total-only ("none") statistics use a plain "Overall" badge. -->
{#if kind === 'none'}
	<span class="badge overall">Overall</span>
{:else}
	<span class="badge">
		<StatGlyph {kind} size={11} />
		<span class="label" style:--kind-color={statKindColor(kind)}>{statKindLabel(kind)}</span>
	</span>
{/if}

<script lang="ts">
import StatGlyph from './StatGlyph.svelte';
import type { StatKind } from './statistics-view.svelte';
import { statKindColor, statKindLabel } from './statistics-display';

interface Props {
	kind: StatKind;
}

let { kind }: Props = $props();
</script>

<style lang="scss">
.badge {
	display: inline-flex;
	align-items: center;
	gap: 5px;
}

.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--kind-color) 80%, transparent);
}

.overall {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
}
</style>
