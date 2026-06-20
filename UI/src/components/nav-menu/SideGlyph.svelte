<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke={color} stroke-width="1.4">
	{#if kind === 'fight'}
		<path d="M11 1.5l3.5 3.5-7.5 7.5-3.5-3.5z M3 12l1.7 1.7" stroke-linejoin="round" stroke-linecap="round" />
	{:else if kind === 'cardGame'}
		<rect x="2.5" y="3" width="7" height="10" rx="1" />
		<rect x="6.5" y="2" width="7" height="10" rx="1" />
	{:else if kind === 'challenges'}
		<path d="M8 1.5l1.8 3.7 4.1.6-3 2.9.7 4.1L8 11l-3.6 1.8.7-4.1-3-2.9 4.1-.6z" stroke-linejoin="round" />
	{:else if kind === 'inventory'}
		<rect x="2" y="4" width="12" height="9.5" rx="0.5" />
		<path d="M5.5 4V2.5h5V4" stroke-linecap="round" />
	{:else if kind === 'skills'}
		<path d="M8.7 1.3L2 9.3H8l-.7 5.4L14 6.7H8z" stroke-linejoin="round" stroke-linecap="round" />
	{:else if kind === 'attributes'}
		<path d="M8 1.5l4.7 2v4.7c0 2.9-2.3 4.7-4.7 5.8-2.4-1.1-4.7-2.9-4.7-5.8V3.5z" stroke-linejoin="round" />
	{:else if kind === 'attributeBreakdown'}
		<rect x="2" y="6.3" width="12" height="3.4" rx="1" />
		<path d="M6.3 6.3v3.4M9.3 6.3v3.4" stroke-linecap="round" />
	{:else if kind === 'stats'}
		<path d="M2 13.5h12M3.5 13.5V8M7 13.5V4.5M10.5 13.5V10M14 13.5V6" stroke-linecap="round" />
	{:else if kind === 'codex'}
		<path
			d="M8 3.4C6.4 2.4 4.3 2.2 2.5 2.6v9.2c1.8-.4 3.9-.2 5.5.8 1.6-1 3.7-1.2 5.5-.8V2.6c-1.8-.4-3.9-.2-5.5.8zM8 3.4v9.4"
			stroke-linejoin="round"
			stroke-linecap="round"
		/>
	{:else if kind === 'options'}
		<circle cx="8" cy="8" r="2.2" />
		<path
			d="M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.5 1.5M11.1 11.1l1.5 1.5M3.4 12.6l1.5-1.5M11.1 4.9l1.5-1.5"
			stroke-linecap="round"
		/>
	{:else if kind === 'help'}
		<circle cx="8" cy="8" r="5.5" />
		<path d="M6.3 6.3c0-1 .8-1.8 1.7-1.8s1.7.8 1.7 1.7c0 1.4-1.7 1.3-1.7 3" stroke-linecap="round" />
		<circle cx="8" cy="11.5" r="0.5" fill={color} stroke="none" />
	{:else if kind === 'quit'}
		<path
			d="M6 2.5H3a1 1 0 00-1 1v9a1 1 0 001 1h3M9.5 5L12.5 8l-3 3M5 8h7.5"
			stroke-linecap="round"
			stroke-linejoin="round"
		/>
	{:else if kind === 'admin'}
		<path d="M8 1.5l5 1.7v4.5c0 2.9-2.2 4.7-5 5.8-2.8-1.1-5-2.9-5-5.8V3.2z" stroke-linejoin="round" />
		<path d="M5.6 8l1.7 1.7L10.5 6.5" stroke-linecap="round" stroke-linejoin="round" />
	{/if}
</svg>

<script lang="ts">
interface Props {
	kind: string;
	active?: boolean;
}

const { kind, active = false }: Props = $props();
const color = $derived(active ? 'var(--accent)' : 'color-mix(in srgb, var(--text-primary) 70%, transparent)');
</script>
