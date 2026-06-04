<div class="toast-container">
	{#each toasts.data as toast (toast.id)}
		<Toast
			message={toast.message}
			type={toast.type}
			dismissible={toast.dismissible}
			onDismiss={() => dismissToast(toast.id)}
		/>
	{/each}
</div>

<script lang="ts">
import Toast from './Toast.svelte';
import { toasts, dismissToast } from '$stores';
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
