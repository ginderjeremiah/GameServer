<!--
	Host for the global modal store: renders the active blocking dialog over a dimmed backdrop and
	owns the interaction model the dialog itself stays free of — backdrop-click / Escape dismissal,
	a focus trap, focus capture+restore, and a body scroll lock. Mounted once in the root layout
	alongside the toast container.
-->
<svelte:window onkeydown={onKeydown} />

{#if active}
	<div class="modal-layer">
		<button class="backdrop" type="button" tabindex="-1" aria-label="Dismiss dialog" onclick={dismiss}></button>
		<div class="shell" tabindex="-1" bind:this={shell}>
			<Modal
				title={active.title}
				body={active.body}
				kind={active.kind}
				confirmLabel={active.confirmLabel}
				cancelLabel={active.cancelLabel}
				onConfirm={confirmActiveModal}
				onCancel={cancelActiveModal}
			/>
		</div>
	</div>
{/if}

<script lang="ts">
import Modal from './Modal.svelte';
import { activeModal, confirmActiveModal, cancelActiveModal, dismissActiveModal } from '$stores';

const active = $derived(activeModal.current);

let shell = $state<HTMLElement | null>(null);
// The element focus is returned to once the last modal closes. A plain field (not reactive): only
// read inside effects/handlers, never rendered.
let restoreFocus: HTMLElement | null = null;

// Backdrop click and Escape are non-button dismissals — a cancel for confirm/destructive, an
// acknowledgement for the single-button acknowledge kind.
const dismiss = () => dismissActiveModal();

const onKeydown = (event: KeyboardEvent) => {
	if (!active) {
		return;
	}
	if (event.key === 'Escape') {
		event.preventDefault();
		dismiss();
	} else if (event.key === 'Tab') {
		trapTab(event);
	}
};

// Keep Tab focus inside the dialog while it is open.
const trapTab = (event: KeyboardEvent) => {
	if (!shell) {
		return;
	}
	const focusables = [...shell.querySelectorAll<HTMLElement>('button:not([disabled])')];
	if (focusables.length === 0) {
		return;
	}
	const first = focusables[0];
	const last = focusables[focusables.length - 1];
	const focused = document.activeElement;
	if (!shell.contains(focused)) {
		event.preventDefault();
		first.focus();
	} else if (event.shiftKey && focused === first) {
		event.preventDefault();
		last.focus();
	} else if (!event.shiftKey && focused === last) {
		event.preventDefault();
		first.focus();
	}
};

// On open, capture the outgoing focus and move it into the dialog — the cancel/safe action for a
// destructive dialog so an accidental Enter can't confirm it, otherwise the primary action.
$effect(() => {
	if (active && shell) {
		restoreFocus ??= document.activeElement as HTMLElement | null;
		const selector = active.kind === 'destructive' ? '[data-modal-cancel]' : '[data-modal-primary]';
		const target = shell.querySelector<HTMLElement>(selector);
		(target ?? shell).focus();
	}
});

// On close, return focus to wherever it was before the dialog opened.
$effect(() => {
	if (!active && restoreFocus) {
		restoreFocus.focus();
		restoreFocus = null;
	}
});

// Lock background scroll while a dialog is open.
$effect(() => {
	if (!active) {
		return;
	}
	document.body.classList.add('modal-open');
	return () => document.body.classList.remove('modal-open');
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
