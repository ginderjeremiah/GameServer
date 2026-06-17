<!-- Boot-sequence progress bar: the shared Bar primitive accented by load phase (accent while
     loading, error tint when paused, accent→success gradient when done), with the companion
     count/status labels beneath. -->
<Bar
	value={view.progressPct}
	testId="progress-bar"
	ariaLabel="Loading progress"
	valueText="{view.completed} of {view.items.length}"
	--bar-height="2px"
	--bar-radius="1px"
	--bar-track-bg="color-mix(in srgb, var(--text-primary) 14%, transparent)"
	--bar-fill={fill}
	--bar-fill-shadow="0 0 6px color-mix(in srgb, var(--accent) 45%, transparent)"
	--bar-transition="width 480ms cubic-bezier(0.4, 0, 0.2, 1), background 200ms"
/>
<div class="progress-labels">
	<span>{view.completed} of {view.items.length}</span>
	<span>{statusLabel}</span>
</div>

<script lang="ts">
import Bar from '$components/Bar.svelte';
import type { LoadingView, Phase } from './loading-view.svelte';

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

const PHASE_FILLS: Partial<Record<Phase, string>> = {
	error: 'color-mix(in srgb, var(--error) 85%, transparent)',
	done: 'linear-gradient(90deg, var(--accent) 0%, var(--success) 100%)'
};

const statusLabel = $derived(STATUS_LABELS[view.phase]);
const fill = $derived(PHASE_FILLS[view.phase] ?? 'var(--accent)');
</script>

<style lang="scss">
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
