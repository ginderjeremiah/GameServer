<div class="loading-screen">
	<div class="loading-form">
		<!-- Diamond mark -->
		<div class="diamond-container">
			<div class="diamond" class:pulsing={phase !== 'done'} class:error-diamond={phase === 'error'}>
				<div class="diamond-inner" class:error-inner={phase === 'error'}></div>
			</div>
		</div>

		<!-- Header -->
		<div class="heading">
			<h1 class:error-text={phase === 'error'}>{title}</h1>
			<p class="subtitle">
				{#if phase === 'checking'}
					Verifying cached reference data…
				{:else if phase === 'loading' && currentItem}
					Loading <span class="highlight">{currentItem.label.toLowerCase()}</span>…
				{:else if phase === 'error' && currentItem}
					Could not load <span class="error-highlight">{currentItem.label.toLowerCase()}</span>.
				{:else if phase === 'done'}
					All systems nominal.
				{:else}
					&nbsp;
				{/if}
			</p>
		</div>

		<!-- Progress bar -->
		<div class="progress-track">
			<div
				class="progress-fill"
				class:error-fill={phase === 'error'}
				class:done-fill={phase === 'done'}
				style:width="{progressPct}%"
			></div>
		</div>
		<div class="progress-labels">
			<span>{completed} of {items.length}</span>
			<span>{phase === 'done' ? 'complete' : phase === 'error' ? 'paused' : phase === 'checking' ? 'checking' : 'loading'}</span>
		</div>

		<!-- Sliding manifest window -->
		<div class="manifest-window">
			<div class="manifest-track" style:transform="translateY({cursorY}px)">
				{#each items as item, i}
					<div class="manifest-row" style:opacity={rowOpacity(i)}>
						<div class="row-icon">
							{#if item.status === 'done'}
								<svg width="14" height="14" viewBox="0 0 16 16" fill="none">
									<path d="M3 8.5l3 3 7-7" stroke="var(--success)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
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
							class:loading-label={item.status === 'loading' && i === activeIndex}
							class:done-label={item.status === 'done'}
							class:error-label={item.status === 'error'}
							class:pending-label={item.status === 'pending'}
						>{item.label}</div>
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
				{/each}
			</div>
			{#if phase !== 'done' && phase !== 'checking'}
				<div class="active-indicator"></div>
			{/if}
		</div>

		<!-- Error retry -->
		{#if phase === 'error' && currentItem}
			<div class="error-banner">
				<div class="error-message">{currentItem.error}</div>
				<button class="retry-button" onclick={retryFailed}>Retry</button>
			</div>
		{/if}

		<!-- Enter Realm button -->
		<button
			class="enter-button"
			class:ready={phase === 'done'}
			disabled={phase !== 'done'}
			onclick={enterGame}
			style:margin-top={phase === 'error' ? '14px' : '22px'}
		>
			{phase === 'done' ? 'Enter Realm' : phase === 'error' ? 'Locked' : 'Loading…'}
		</button>
	</div>
</div>

<script lang="ts">
import { ApiRequest } from '$lib/api';
import { routeTo } from '$lib/common';
import { staticData } from '$stores';
import { onMount } from 'svelte';

type ItemStatus = 'pending' | 'loading' | 'done' | 'error';
type Phase = 'checking' | 'loading' | 'done' | 'error';

interface LoadItem {
	key: string;
	label: string;
	status: ItemStatus;
	durationMs: number;
	error: string | null;
	fetch: () => Promise<void>;
}

let items = $state<LoadItem[]>([]);
let phase = $state<Phase>('checking');
let activeIndex = $state(-1);

const ROW_HEIGHT = 42;
const VISIBLE_ROWS = 5;

const completed = $derived(items.filter(i => i.status === 'done').length);
const progressPct = $derived(items.length ? (completed / items.length) * 100 : 0);
const currentItem = $derived(activeIndex >= 0 && activeIndex < items.length ? items[activeIndex] : null);
const cursorY = $derived((2 - activeIndex) * ROW_HEIGHT);

const title = $derived({
	checking: 'Checking for updates.',
	loading: 'Preparing the realm.',
	error: 'Connection failed.',
	done: 'Ready.',
}[phase]);

const rowOpacity = (i: number) => {
	const distance = Math.abs(i - activeIndex);
	if (distance > 2) return 0;
	return 1 - distance * 0.18;
};

const enterGame = () => {
	if (phase === 'done') routeTo('/game');
};

const retryFailed = async () => {
	if (phase !== 'error' || activeIndex < 0) return;
	items[activeIndex].status = 'loading';
	items[activeIndex].error = null;
	phase = 'loading';
	await loadFrom(activeIndex);
};

const loadFrom = async (startIdx: number) => {
	for (let i = startIdx; i < items.length; i++) {
		if (items[i].status === 'done') continue;

		activeIndex = i;
		items[i].status = 'loading';
		items[i].error = null;

		const start = performance.now();
		try {
			await items[i].fetch();
			items[i].durationMs = Math.round(performance.now() - start);
			items[i].status = 'done';
		} catch (e) {
			items[i].status = 'error';
			items[i].error = e instanceof Error ? e.message : 'Network error — could not reach server.';
			phase = 'error';
			return;
		}

		await new Promise(r => setTimeout(r, 80));
	}

	phase = 'done';
	activeIndex = items.length;
};

onMount(async () => {
	const alreadyLoaded = (data: unknown) => data !== undefined && data !== null;

	items = [
		{
			key: 'zones', label: 'Zones',
			status: alreadyLoaded(staticData.zones) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.zones ??= await ApiRequest.get('Zones'); },
		},
		{
			key: 'enemies', label: 'Enemies',
			status: alreadyLoaded(staticData.enemies) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.enemies ??= await ApiRequest.get('Enemies'); },
		},
		{
			key: 'items', label: 'Items',
			status: alreadyLoaded(staticData.items) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.items ??= await ApiRequest.get('Items'); },
		},
		{
			key: 'skills', label: 'Skills',
			status: alreadyLoaded(staticData.skills) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.skills ??= await ApiRequest.get('Skills'); },
		},
		{
			key: 'itemMods', label: 'Item Mods',
			status: alreadyLoaded(staticData.itemMods) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.itemMods ??= await ApiRequest.get('ItemMods'); },
		},
		{
			key: 'attributes', label: 'Attributes',
			status: alreadyLoaded(staticData.attributes) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.attributes ??= await ApiRequest.get('Attributes'); },
		},
		{
			key: 'challenges', label: 'Challenges',
			status: alreadyLoaded(staticData.challenges) ? 'done' : 'pending',
			durationMs: 0, error: null,
			fetch: async () => { staticData.challenges ??= await ApiRequest.get('Challenges'); },
		},
	];

	// If all cached, skip straight to done
	if (items.every(i => i.status === 'done')) {
		phase = 'done';
		activeIndex = items.length;
		return;
	}

	// Checking phase
	phase = 'checking';
	await new Promise(r => setTimeout(r, 500));

	// Loading phase: sequential
	phase = 'loading';
	await loadFrom(0);
});
</script>

<style lang="scss">
.loading-screen {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	padding: 40px;
}

.loading-form {
	width: 380px;
	max-width: 100%;
}

.diamond-container {
	display: flex;
	justify-content: center;
	margin-bottom: 26px;
}

.diamond {
	width: 16px;
	height: 16px;
	transform: rotate(45deg);
	border: 1px solid var(--accent);
	box-shadow: 0 0 8px rgba(161, 194, 247, 0.4);
	position: relative;
	transition: border-color 200ms, box-shadow 200ms;

	&.pulsing {
		animation: pulse-glow 1.6s ease-in-out infinite;
	}

	&.error-diamond {
		border-color: var(--error);
		box-shadow: 0 0 8px rgba(240, 160, 148, 0.45);
	}
}

.diamond-inner {
	position: absolute;
	inset: 4px;
	background: var(--accent);
	transition: background 200ms;

	&.error-inner {
		background: var(--error);
	}
}

.heading {
	text-align: center;
	margin-bottom: 22px;

	h1 {
		margin: 0;
		padding: 0;
		font-size: 26px;
		font-weight: 400;
		letter-spacing: -0.3px;
		color: var(--text-primary);
		transition: color 200ms;

		&.error-text {
			color: var(--error);
		}
	}

	.subtitle {
		margin: 8px 0 0;
		font-size: 12.5px;
		color: var(--text-secondary);
		font-family: 'Geist Mono', monospace;
		letter-spacing: 0.5px;
		min-height: 18px;
	}

	.highlight {
		color: var(--accent-light);
	}

	.error-highlight {
		color: var(--error);
	}
}

.progress-track {
	height: 2px;
	background: rgba(240, 240, 240, 0.14);
	border-radius: 1px;
	overflow: hidden;
	position: relative;
}

.progress-fill {
	position: absolute;
	inset: 0;
	background: var(--accent);
	box-shadow: 0 0 6px rgba(161, 194, 247, 0.45);
	transition: width 480ms cubic-bezier(.4, 0, .2, 1), background 200ms;

	&.error-fill {
		background: rgba(240, 160, 148, 0.85);
	}

	&.done-fill {
		background: linear-gradient(90deg, var(--accent) 0%, var(--success) 100%);
	}
}

.progress-labels {
	display: flex;
	justify-content: space-between;
	margin-top: 5px;
	font-family: 'Geist Mono', monospace;
	font-size: 10.5px;
	color: rgba(240, 240, 240, 0.65);
	letter-spacing: 0.5px;
}

.manifest-window {
	margin-top: 18px;
	height: calc(42px * 5);
	position: relative;
	overflow: hidden;
	mask-image: linear-gradient(to bottom, transparent 0%, black 22%, black 78%, transparent 100%);
	-webkit-mask-image: linear-gradient(to bottom, transparent 0%, black 22%, black 78%, transparent 100%);
}

.manifest-track {
	position: absolute;
	left: 0;
	right: 0;
	transition: transform 420ms cubic-bezier(.4, 0, .2, 1);
}

.manifest-row {
	height: 42px;
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 0 14px;
	transition: opacity 380ms ease;
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
	border: 1.5px solid rgba(161, 194, 247, 0.28);
	border-top-color: var(--accent);
	border-radius: 50%;
	animation: spin 0.9s linear infinite;
}

.row-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	background: rgba(240, 240, 240, 0.35);
}

.row-label {
	flex: 1;
	font-size: 14px;
	color: rgba(240, 240, 240, 0.65);
	letter-spacing: 0.1px;

	&.loading-label {
		color: var(--text-primary);
		font-weight: 500;
	}

	&.done-label {
		color: rgba(240, 240, 240, 0.7);
	}

	&.error-label {
		color: var(--error);
	}
}

.row-status {
	font-family: 'Geist Mono', monospace;
	font-size: 10.5px;
	letter-spacing: 0.5px;
	color: rgba(240, 240, 240, 0.4);
	min-width: 58px;
	text-align: right;

	&.done-status {
		color: rgba(189, 224, 180, 0.85);
	}

	&.loading-status {
		color: var(--accent-light);
	}

	&.error-status {
		color: var(--error);
	}
}

.active-indicator {
	position: absolute;
	left: 0;
	right: 0;
	top: calc(42px * 2);
	height: 42px;
	border: 1px solid rgba(161, 194, 247, 0.18);
	background: rgba(161, 194, 247, 0.05);
	border-radius: 3px;
	pointer-events: none;
}

.error-banner {
	margin-top: 16px;
	padding: 10px 12px;
	background: rgba(240, 160, 148, 0.08);
	border: 1px solid rgba(240, 160, 148, 0.35);
	border-radius: 3px;
	display: flex;
	align-items: center;
	gap: 12px;

	.error-message {
		font-family: 'Geist Mono', monospace;
		font-size: 11px;
		color: var(--error);
		flex: 1;
		line-height: 1.4;
	}
}

.retry-button {
	background: rgba(240, 240, 240, 0.08);
	border: 1px solid rgba(240, 240, 240, 0.4);
	color: var(--text-primary);
	padding: 7px 14px;
	border-radius: 2px;
	font-family: inherit;
	font-size: 11.5px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	cursor: pointer;
	transition: all 140ms;
}

.enter-button {
	width: 100%;
	padding: 12px 0;
	background: transparent;
	color: rgba(240, 240, 240, 0.45);
	border: 1px solid rgba(240, 240, 240, 0.25);
	border-radius: 2px;
	cursor: not-allowed;
	font-size: 13px;
	font-weight: 500;
	font-family: inherit;
	letter-spacing: 2px;
	text-transform: uppercase;
	transition: all 200ms;

	&.ready {
		background: var(--accent);
		color: #111;
		border-color: var(--accent);
		cursor: pointer;
	}
}
</style>
