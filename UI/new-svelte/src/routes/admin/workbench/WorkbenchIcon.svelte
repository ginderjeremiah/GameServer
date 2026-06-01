<svg
	class="wb-ic"
	width={size}
	height={size}
	viewBox="0 0 16 16"
	fill="none"
	{stroke}
	stroke-width={sw}
	stroke-linecap="round"
	stroke-linejoin="round"
	aria-hidden="true"
>
	{#each segments as d (d)}
		<path {d} />
	{/each}
	{#if kind === 'warn'}
		<circle cx="8" cy="11.6" r="0.4" fill={stroke} stroke="none" />
	{/if}
	{#if kind === 'skull'}
		<circle cx="6" cy="7.2" r="1.1" />
		<circle cx="10" cy="7.2" r="1.1" />
	{/if}
</svg>

<script lang="ts" module>
/** Glyph set ported from the Workbench design prototype (enemy-admin/ui.jsx). */
export type WorkbenchIconKind =
	| 'skull'
	| 'box'
	| 'bolt'
	| 'map'
	| 'bars'
	| 'rune'
	| 'pin'
	| 'multiply'
	| 'tag'
	| 'plus'
	| 'chevR'
	| 'chevD'
	| 'check'
	| 'x'
	| 'warn'
	| 'search'
	| 'back';

const PATHS: Record<WorkbenchIconKind, string> = {
	skull:
		'M8 1.5c3 0 5 2.2 5 5.2 0 1.8-.8 2.7-.8 3.8 0 .8-.6 1.2-1.4 1.2H5.2c-.8 0-1.4-.4-1.4-1.2 0-1.1-.8-2-.8-3.8 0-3 2-5.2 5-5.2z',
	box: 'M2.5 5.2L8 2.5l5.5 2.7v5.6L8 13.5l-5.5-2.7z|M2.5 5.2L8 8l5.5-2.8M8 8v5.5',
	bolt: 'M9 1.5L3.5 9H7l-1 5.5L12.5 7H9z',
	map: 'M2 4l4-1.5 4 1.5 4-1.5v9.5L10 13.5 6 12 2 13.5z|M6 2.5V12M10 4v9.5',
	bars: 'M3 13.5V8.5M7 13.5V3.5M11 13.5V6.5',
	rune: 'M8 2v12M4 5l8 6M12 5l-8 6',
	pin: 'M8 14s4.2-4 4.2-7A4.2 4.2 0 008 2.8 4.2 4.2 0 003.8 7c0 3 4.2 7 4.2 7z',
	multiply: 'M4 4l8 8M12 4l-8 8',
	tag: 'M2.5 2.5h5L13 8l-5 5-5.5-5.5z',
	plus: 'M8 3v10M3 8h10',
	chevR: 'M6 3.5L10.5 8L6 12.5',
	chevD: 'M3.5 6L8 10.5L12.5 6',
	check: 'M3 7.3L6 10.2L11 4',
	x: 'M4 4l8 8M12 4l-8 8',
	warn: 'M8 1.8L15 14H1zM8 6v3.5',
	search: 'M7 1.5a5.5 5.5 0 104 9.4 5.5 5.5 0 00-4-9.4zM11.2 11.2L14.5 14.5',
	back: 'M9.5 3.5L5 8l4.5 4.5M5 8h8'
};
</script>

<script lang="ts">
interface Props {
	kind: WorkbenchIconKind;
	size?: number;
	sw?: number;
	stroke?: string;
}

const { kind, size = 14, sw = 1.5, stroke = 'currentColor' }: Props = $props();
const segments = $derived((PATHS[kind] ?? '').split('|'));
</script>

<style lang="scss">
.wb-ic {
	flex-shrink: 0;
}
</style>
