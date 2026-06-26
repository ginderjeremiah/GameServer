<!--
	Host for the global modal store: renders the active blocking dialog over a dimmed backdrop and
	owns the interaction model the dialog itself stays free of — backdrop-click / Escape dismissal,
	a focus trap, focus capture+restore, and a body scroll lock. Mounted once in the root layout
	alongside the toast container.
-->
{#if active}
	<div class="modal-layer">
		<button class="backdrop" type="button" tabindex="-1" aria-label="Dismiss dialog" onclick={dismiss}></button>
		<div
			class="shell"
			tabindex="-1"
			bind:this={shell}
			use:focusTrap={{ onEscape: dismiss, scrollLockClass: 'modal-open' }}
		>
			<!-- Key on the modal id so a queued modal remounts and replays the entrance animation
			     rather than just swapping its props in place. -->
			{#key active.id}
				<Modal
					title={active.title}
					body={active.body}
					kind={active.kind}
					confirmLabel={active.confirmLabel}
					cancelLabel={active.cancelLabel}
					onConfirm={confirmActiveModal}
					onCancel={cancelActiveModal}
				/>
			{/key}
		</div>
	</div>
{/if}

<script lang="ts">
import Modal from './Modal.svelte';
import { focusTrap } from './focus-trap';
import { activeModal, confirmActiveModal, cancelActiveModal, dismissActiveModal } from '$stores';

const active = $derived(activeModal.current);

let shell = $state<HTMLElement | null>(null);

// Backdrop click and Escape are non-button dismissals — a cancel for confirm/destructive, an
// acknowledgement for the single-button acknowledge kind.
const dismiss = () => dismissActiveModal();

// On open (and per queued modal — the shell persists while the inner card remounts), move focus into
// the dialog — the cancel/safe action for a destructive dialog so an accidental Enter can't confirm
// it, otherwise the primary action. Trap/Escape/scroll-lock/restore are owned by focusTrap.
$effect(() => {
	if (active && shell) {
		const selector = active.kind === 'destructive' ? '[data-modal-cancel]' : '[data-modal-primary]';
		const target = shell.querySelector<HTMLElement>(selector);
		(target ?? shell).focus();
	}
});
</script>

<style lang="scss">
:global(body.modal-open) {
	overflow: hidden;
}

.modal-layer {
	position: fixed;
	inset: 0;
	z-index: 200;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 24px;
}

.backdrop {
	position: absolute;
	inset: 0;
	padding: 0;
	border: none;
	cursor: default;
	background: color-mix(in srgb, var(--black) 55%, transparent);
	backdrop-filter: blur(2px);
	// Fade in; collapsed to ~instant under `prefers-reduced-motion`.
	animation: modal-backdrop-in 0.26s ease both;
}

.shell {
	position: relative;
	outline: none;
}

@keyframes modal-backdrop-in {
	from {
		opacity: 0;
	}
	to {
		opacity: 1;
	}
}
</style>
