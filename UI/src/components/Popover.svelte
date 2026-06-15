<!--
	Reusable, non-blocking popover/overlay primitive. It owns the interaction model the content
	stays free of — backdrop-click / Escape dismissal, a focus trap, focus capture+restore, and a
	body scroll lock — the same chrome `ModalHost` provides for blocking dialogs, but driven by a
	plain `open` boolean rather than the promise-based modal queue (so it suits in-screen filter
	popovers and other non-blocking overlays).

	The layer is absolutely positioned, so it overlays its nearest positioned ancestor rather than
	the whole viewport; mount it inside a `position: relative` container.
-->
<svelte:window onkeydown={onKeydown} />

{#if open}
	<div class="popover-layer">
		<button class="popover-backdrop" type="button" tabindex="-1" aria-label={closeLabel} onclick={onClose}></button>
		<div class="popover-shell" role="dialog" aria-modal="true" aria-label={label} tabindex="-1" bind:this={shell}>
			{@render children()}
		</div>
	</div>
{/if}

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Whether the popover is shown. */
	open: boolean;
	/** Called on every dismissal path (backdrop click, Escape). */
	onClose: () => void;
	/** Accessible name for the dialog. */
	label: string;
	/** Accessible name for the backdrop dismiss button. */
	closeLabel?: string;
	/** The popover's content. */
	children: Snippet;
}

const { open, onClose, label, closeLabel = 'Close', children }: Props = $props();

let shell = $state<HTMLElement | null>(null);
// The element focus is returned to once the popover closes. A plain field (not reactive): only read
// inside effects/handlers, never rendered.
let restoreFocus: HTMLElement | null = null;

const onKeydown = (event: KeyboardEvent) => {
	if (!open) {
		return;
	}
	if (event.key === 'Escape') {
		event.preventDefault();
		onClose();
	} else if (event.key === 'Tab') {
		trapTab(event);
	}
};

// Keep Tab focus inside the popover while it is open.
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

// On open, capture the outgoing focus and move it onto the first focusable inside the popover (or
// the shell itself if it has none) so the trap has somewhere to anchor.
$effect(() => {
	if (open && shell) {
		restoreFocus ??= document.activeElement as HTMLElement | null;
		const target = shell.querySelector<HTMLElement>('button:not([disabled])');
		(target ?? shell).focus();
	}
});

// On close, return focus to wherever it was before the popover opened.
$effect(() => {
	if (!open && restoreFocus) {
		restoreFocus.focus();
		restoreFocus = null;
	}
});

// Lock background scroll while open.
$effect(() => {
	if (!open) {
		return;
	}
	document.body.classList.add('popover-open');
	return () => document.body.classList.remove('popover-open');
});
</script>

<style lang="scss">
:global(body.popover-open) {
	overflow: hidden;
}

.popover-layer {
	position: absolute;
	inset: 0;
	z-index: 40;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 16px;
}

.popover-backdrop {
	position: absolute;
	inset: 0;
	padding: 0;
	border: none;
	cursor: pointer;
	background: color-mix(in srgb, var(--black) 55%, transparent);
	backdrop-filter: blur(3px);
}

.popover-shell {
	position: relative;
	outline: none;
}
</style>
