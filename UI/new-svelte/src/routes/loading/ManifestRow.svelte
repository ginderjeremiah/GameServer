<div class="manifest-row" style:opacity>
	<div class="row-icon">
		{#if item.status === 'done'}
			<svg width="14" height="14" viewBox="0 0 16 16" fill="none">
				<path
					d="M3 8.5l3 3 7-7"
					stroke="var(--success)"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
				/>
			</svg>
		{:else if item.status === 'error'}
			<svg width="14" height="14" viewBox="0 0 16 16" fill="none">
				<path d="M4 4l8 8M12 4l-8 8" stroke="var(--error)" stroke-width="2" stroke-linecap="round" />
			</svg>
		{:else if item.status === 'loading'}
			<div class="row-spinner"></div>
		{:else}
			<div class="row-dot"></div>
		{/if}
	</div>
	<div
		class="row-label"
		class:loading-label={item.status === 'loading' && active}
		class:done-label={item.status === 'done'}
		class:error-label={item.status === 'error'}
		class:pending-label={item.status === 'pending'}
	>
		{item.label}
	</div>
	<div
		class="row-status"
		class:done-status={item.status === 'done'}
		class:loading-status={item.status === 'loading'}
		class:error-status={item.status === 'error'}
	>
		{#if item.status === 'done'}
			{item.durationMs ? `${item.durationMs}ms` : 'done'}
		{:else if item.status === 'loading'}
			loading…
		{:else if item.status === 'error'}
			failed
		{:else}
			queued
		{/if}
	</div>
</div>

<script lang="ts">
import type { LoadItem } from './loading-view.svelte';

type Props = {
	item: LoadItem;
	active: boolean;
	opacity: number;
};

let { item, active, opacity }: Props = $props();
</script>

<style lang="scss">
.manifest-row {
	height: 42px;
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 0 14px;
}

.row-icon {
	width: 14px;
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
}

.row-spinner {
	width: 12px;
	height: 12px;
	border: 1.5px solid color-mix(in srgb, var(--accent) 28%, transparent);
	border-top-color: var(--accent);
	border-radius: 50%;
	animation: spin 0.9s linear infinite;
}

.row-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	background: color-mix(in srgb, var(--text-primary) 35%, transparent);
}

.row-label {
	flex: 1;
	font-size: 14px;
	color: color-mix(in srgb, var(--text-primary) 65%, transparent);
	letter-spacing: 0.1px;

	&.loading-label {
		color: var(--text-primary);
		font-weight: 500;
	}

	&.done-label {
		color: color-mix(in srgb, var(--text-primary) 70%, transparent);
	}

	&.error-label {
		color: var(--error);
	}
}

.row-status {
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.5px;
	color: var(--text-muted);
	min-width: 58px;
	text-align: right;

	&.done-status {
		color: color-mix(in srgb, var(--success) 85%, transparent);
	}

	&.loading-status {
		color: var(--accent-light);
	}

	&.error-status {
		color: var(--error);
	}
}
</style>
