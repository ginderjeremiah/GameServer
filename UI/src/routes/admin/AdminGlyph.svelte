<svg width="16" height="16" viewBox="0 0 16 16" fill="none" {stroke} stroke-width="1.4" aria-hidden="true">
	{#if kind === 'skull'}
		<path
			d="M8 1.5c3 0 5 2.2 5 5.2 0 1.8-.8 2.7-.8 3.8 0 .8-.6 1.2-1.4 1.2H5.2c-.8 0-1.4-.4-1.4-1.2 0-1.1-.8-2-.8-3.8 0-3 2-5.2 5-5.2z"
			stroke-linejoin="round"
		/>
		<circle cx="6" cy="7.2" r="1.1" />
		<circle cx="10" cy="7.2" r="1.1" />
		<path d="M6.5 13v1.5M9.5 13v1.5" stroke-linecap="round" />
	{:else if kind === 'bars'}
		<path d="M3 13.5V8.5M7 13.5V3.5M11 13.5V6.5" stroke-linecap="round" />
		<circle cx="3" cy="6.5" r="1.2" fill={stroke} stroke="none" />
		<circle cx="7" cy="2.2" r="1.2" fill={stroke} stroke="none" />
		<circle cx="11" cy="5" r="1.2" fill={stroke} stroke="none" />
	{:else if kind === 'rune'}
		<path d="M8 2v12M4 5l8 6M12 5l-8 6" stroke-linecap="round" />
	{:else if kind === 'box'}
		<path d="M2.5 5.2L8 2.5l5.5 2.7v5.6L8 13.5l-5.5-2.7z" stroke-linejoin="round" />
		<path d="M2.5 5.2L8 8l5.5-2.8M8 8v5.5" stroke-linejoin="round" />
	{:else if kind === 'bolt'}
		<path d="M9 1.5L3.5 9H7l-1 5.5L12.5 7H9z" stroke-linejoin="round" />
	{:else if kind === 'multiply'}
		<path d="M4 4l8 8M12 4l-8 8" stroke-linecap="round" />
	{:else if kind === 'map'}
		<path d="M2 4l4-1.5 4 1.5 4-1.5v9.5L10 13.5 6 12 2 13.5z" stroke-linejoin="round" />
		<path d="M6 2.5V12M10 4v9.5" />
	{:else if kind === 'pin'}
		<path d="M8 14s4.2-4 4.2-7A4.2 4.2 0 008 2.8 4.2 4.2 0 003.8 7c0 3 4.2 7 4.2 7z" stroke-linejoin="round" />
		<circle cx="8" cy="6.9" r="1.4" />
	{:else if kind === 'tag'}
		<path d="M2.5 2.5h5L13 8l-5 5-5.5-5.5z" stroke-linejoin="round" />
		<circle cx="5" cy="5" r="0.9" fill={stroke} stroke="none" />
	{:else if kind === 'back'}
		<path d="M7 3.5L2.5 8 7 12.5M2.5 8H13" stroke-linecap="round" stroke-linejoin="round" />
	{:else if kind === 'trophy'}
		<path d="M5 2.5h6v3a3 3 0 0 1-6 0z" stroke-linejoin="round" />
		<path d="M5 3.3H3.2a1.5 1.5 0 0 0 1.9 2.2M11 3.3h1.8a1.5 1.5 0 0 1-1.9 2.2" />
		<path d="M8 8.5v2.2M6 13.5h4M6.6 11h2.8l.4 2.5H6.2z" stroke-linecap="round" stroke-linejoin="round" />
	{:else if kind === 'inbox'}
		<path d="M2.5 3.5h11v9h-11z" stroke-linejoin="round" />
		<path d="M2.5 9h3l1 1.5h3l1-1.5h3" stroke-linejoin="round" />
	{/if}
</svg>

<script lang="ts" module>
export type AdminGlyphKind =
	| 'skull'
	| 'bars'
	| 'rune'
	| 'box'
	| 'bolt'
	| 'multiply'
	| 'map'
	| 'pin'
	| 'tag'
	| 'back'
	| 'trophy'
	| 'inbox';
</script>

<script lang="ts">
interface Props {
	kind: AdminGlyphKind;
	active?: boolean;
}

const { kind, active = false }: Props = $props();
const stroke = $derived(active ? 'var(--accent)' : 'color-mix(in srgb, var(--text-primary) 70%, transparent)');
</script>
