<div class="toast-container">
	{#each rendered as item (item.id)}
		<Toast
			message={item.data.message}
			type={item.data.type}
			dismissible={item.data.dismissible}
			action={item.data.action}
			leaving={item.leaving}
			onDismiss={() => dismissToast(item.id)}
		/>
	{/each}
</div>

<script lang="ts">
import { untrack, onDestroy } from 'svelte';
import Toast from './Toast.svelte';
import { toasts, dismissToast, type ToastData } from '$stores';

// The toast store removes a toast the instant it is dismissed (the notification is logically gone),
// but the view wants to play an exit animation first. So the container keeps its own render list
// that mirrors the store and lets a removed toast linger, marked `leaving`, until its exit
// animation finishes — keeping the store the synchronous source of truth while the view animates.
const EXIT_MS = 280;

interface RenderedToast {
	id: number;
	data: ToastData;
	leaving: boolean;
}

let rendered = $state<RenderedToast[]>([]);
// Pending removal timers, keyed by toast id. Never read during rendering, so deliberately
// non-reactive (mirrors the toast store's own timer map).
// eslint-disable-next-line svelte/prefer-svelte-reactivity
const removalTimers = new Map<number, ReturnType<typeof setTimeout>>();

const scheduleRemoval = (id: number) => {
	clearTimeout(removalTimers.get(id));
	removalTimers.set(
		id,
		setTimeout(() => {
			removalTimers.delete(id);
			rendered = rendered.filter((r) => r.id !== id);
		}, EXIT_MS)
	);
};

// Reconcile the render list against the store: append newly-added toasts, and mark toasts that have
// left the store as `leaving` so they animate out before being dropped. Wrapped in `untrack` so the
// effect only re-runs on store changes, not on its own writes to `rendered`.
$effect(() => {
	const active = [...toasts.data];
	untrack(() => {
		const activeIds = new Set(active.map((t) => t.id));
		for (const item of rendered) {
			if (!item.leaving && !activeIds.has(item.id)) {
				item.leaving = true;
				scheduleRemoval(item.id);
			}
		}
		const renderedIds = new Set(rendered.map((r) => r.id));
		for (const data of active) {
			if (!renderedIds.has(data.id)) {
				rendered.push({ id: data.id, data, leaving: false });
			}
		}
	});
});

onDestroy(() => {
	for (const timer of removalTimers.values()) {
		clearTimeout(timer);
	}
	removalTimers.clear();
});
</script>

<style lang="scss">
.toast-container {
	position: fixed;
	top: 1rem;
	right: 1rem;
	z-index: 100;
	display: flex;
	flex-direction: column;
	gap: 0.5rem;
	// Let clicks pass through the gaps between toasts; each toast re-enables
	// pointer events for itself so its dismiss control stays usable.
	pointer-events: none;
}
</style>
