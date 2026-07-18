<!--
	Reusable, non-blocking popover/overlay primitive. It owns the interaction model the content
	stays free of — backdrop-click / Escape dismissal, a focus trap, focus capture+restore, and a
	body scroll lock — the same chrome `ModalHost` provides for blocking dialogs, but driven by a
	plain `open` boolean rather than the promise-based modal queue (so it suits in-screen filter
	popovers and other non-blocking overlays).

	The layer is absolutely positioned, so it overlays its nearest positioned ancestor rather than
	the whole viewport; mount it inside a `position: relative` container.
-->
{#if open}
	<div class="popover-layer">
		<button class="popover-backdrop" type="button" tabindex="-1" aria-label={closeLabel} onclick={onClose}></button>
		<div
			class="popover-shell"
			role="dialog"
			aria-modal="true"
			aria-label={label}
			tabindex="-1"
			bind:this={shell}
			use:focusTrap={{ onEscape: onClose, scrollLockClass: 'popover-open' }}
		>
			{@render children()}
		</div>
	</div>
{/if}

<script lang="ts">
import type { Snippet } from 'svelte';
import { focusTrap, FOCUSABLE_SELECTOR } from './focus-trap';

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

// On open, move focus onto the first focusable inside the popover (or the shell itself if it has
// none) so the trap has somewhere to anchor. Trap/Escape/scroll-lock/restore are owned by focusTrap.
// Content can also swap while open (e.g. a loading placeholder replaced once an async fetch resolves);
// if that unmounts the focused element, focus falls back to <body>, so a MutationObserver re-places it
// onto the (possibly new) first focusable whenever a swap drops focus outside the shell.
$effect(() => {
	if (open && shell) {
		const placeFocus = () => {
			const target = shell?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR);
			(target ?? shell)?.focus();
		};
		placeFocus();

		const observer = new MutationObserver(() => {
			if (!shell?.contains(document.activeElement)) {
				placeFocus();
			}
		});
		observer.observe(shell, { childList: true, subtree: true });
		return () => observer.disconnect();
	}
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
