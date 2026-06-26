<div {id} class="status-line" data-testid="status-line" role="status" aria-live="polite" style:color>
	{#if type === 'ok' || type === 'err'}
		<ValidationIcon {type} size={14} {color} />
	{/if}
	{text}
</div>

<script lang="ts">
import ValidationIcon from './ValidationIcon.svelte';
import type { StatusType } from './login-validation';

const COLORS: Record<StatusType, string> = {
	err: 'var(--error)',
	ok: 'var(--success)',
	warn: 'var(--warning)',
	info: 'var(--text-secondary)',
	idle: 'var(--text-tertiary)'
};

// `id` lets fields associate their error with this live region via `aria-describedby`.
let { type, text, id }: { type: StatusType; text: string; id?: string } = $props();

const color = $derived(COLORS[type]);
</script>

<style lang="scss">
.status-line {
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.6px;
	text-align: center;
	min-height: 18px;
	margin-bottom: 22px;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 6px;
	transition: color 160ms;
}
</style>
