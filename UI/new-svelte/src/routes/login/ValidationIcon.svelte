<svg width={size} height={size} viewBox="0 0 16 16" fill="none">
	{#if type === 'ok'}
		<path d="M3 8.5l3 3 7-7" stroke={resolvedColor} stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
	{:else}
		<path d="M4 4l8 8M12 4l-8 8" stroke={resolvedColor} stroke-width="2" stroke-linecap="round" />
	{/if}
</svg>

<script lang="ts">
interface Props {
	/** Whether to render the success check or the error cross. */
	type: 'ok' | 'err';
	/** Square size in pixels. */
	size?: number;
	/** Stroke colour; defaults to the themed success/error colour for the type. */
	color?: string;
}

let { type, size = 16, color }: Props = $props();

const resolvedColor = $derived(color ?? (type === 'ok' ? 'var(--success)' : 'var(--error)'));
</script>
