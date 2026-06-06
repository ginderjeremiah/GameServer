<!-- A tiny hint describing how a statistic is read: lower-is-better, or whether
     the headline is a peak / best / running total. -->
<span class="hint">{text}</span>

<script lang="ts">
import type { StatType } from './statistics-view.svelte';

interface Props {
	stat: StatType;
}

let { stat }: Props = $props();

const text = $derived(
	stat.comp === 'AtMost' ? 'lower is better' : stat.agg === 'max' ? 'peak' : stat.agg === 'min' ? 'best' : 'total'
);
</script>

<style lang="scss">
.hint {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	color: var(--text-muted);
}
</style>
