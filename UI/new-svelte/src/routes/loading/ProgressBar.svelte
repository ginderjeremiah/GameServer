<div class="progress-track" data-testid="progress-bar">
	<div
		class="progress-fill"
		class:error-fill={view.phase === 'error'}
		class:done-fill={view.phase === 'done'}
		style:width="{view.progressPct}%"
	></div>
</div>
<div class="progress-labels">
	<span>{view.completed} of {view.items.length}</span>
	<span>{statusLabel}</span>
</div>

<script lang="ts">
import type { LoadingView } from './loading-view.svelte';

type Props = {
	view: LoadingView;
};

let { view }: Props = $props();

const STATUS_LABELS = {
	done: 'complete',
	error: 'paused',
	checking: 'checking',
	loading: 'loading'
} as const;

const statusLabel = $derived(STATUS_LABELS[view.phase]);
</script>

<style lang="scss">
.progress-track {
	height: 2px;
	background: color-mix(in srgb, var(--text-primary) 14%, transparent);
	border-radius: 1px;
	overflow: hidden;
	position: relative;
}

.progress-fill {
	position: absolute;
	inset: 0;
	background: var(--accent);
	box-shadow: 0 0 6px color-mix(in srgb, var(--accent) 45%, transparent);
	transition:
		width 480ms cubic-bezier(0.4, 0, 0.2, 1),
		background 200ms;

	&.error-fill {
		background: color-mix(in srgb, var(--error) 85%, transparent);
	}

	&.done-fill {
		background: linear-gradient(90deg, var(--accent) 0%, var(--success) 100%);
	}
}

.progress-labels {
	display: flex;
	justify-content: space-between;
	margin-top: 5px;
	font-family: var(--mono);
	font-size: 10.5px;
	color: color-mix(in srgb, var(--text-primary) 65%, transparent);
	letter-spacing: 0.5px;
}
</style>
