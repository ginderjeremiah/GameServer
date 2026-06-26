/**
 * Focus-trap action shared by the blocking (`ModalHost`) and non-blocking (`Popover`) overlays — the
 * interaction model their content stays free of: it keeps Tab focus inside the trapped element, routes
 * Escape to a dismissal callback, locks background scroll, and restores focus to the element that was
 * focused before the trap mounted. Attach it to the overlay's shell while it is open (the action's
 * lifetime is the overlay's open lifetime, since the shell is rendered behind an `{#if}`).
 *
 * Initial focus is deliberately NOT owned here: the two consumers place it differently (the modal aims
 * at its kind-appropriate action and must re-aim per queued modal; the popover takes the first
 * focusable), so each keeps its own one-shot focus step and reuses `FOCUSABLE_SELECTOR` for it.
 */

/**
 * Tab-reachable elements. Intentionally broader than a bare `button` so a modal/popover containing a
 * link, input, or `[tabindex]` element still traps and anchors onto it.
 */
export const FOCUSABLE_SELECTOR =
	'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface FocusTrapOptions {
	/** Called when Escape is pressed while the trap is active (the overlay's dismiss path). */
	onEscape?: () => void;
	/** When set, the class toggled on `<body>` to lock background scroll while the trap is active. */
	scrollLockClass?: string;
}

export function focusTrap(node: HTMLElement, options: FocusTrapOptions = {}) {
	let opts = options;
	// Captured on mount (before the consumer moves focus inward) and restored on destroy.
	const restoreFocus = document.activeElement as HTMLElement | null;

	const onKeydown = (event: KeyboardEvent) => {
		if (event.key === 'Escape') {
			event.preventDefault();
			opts.onEscape?.();
			return;
		}
		if (event.key !== 'Tab') {
			return;
		}
		const focusables = [...node.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)];
		if (focusables.length === 0) {
			return;
		}
		const first = focusables[0];
		const last = focusables[focusables.length - 1];
		const focused = document.activeElement;
		if (!node.contains(focused)) {
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

	window.addEventListener('keydown', onKeydown);
	if (opts.scrollLockClass) {
		document.body.classList.add(opts.scrollLockClass);
	}

	return {
		update(next: FocusTrapOptions) {
			// Swap the scroll-lock class if it changed (rare); options are otherwise read live.
			if (opts.scrollLockClass !== next.scrollLockClass) {
				if (opts.scrollLockClass) {
					document.body.classList.remove(opts.scrollLockClass);
				}
				if (next.scrollLockClass) {
					document.body.classList.add(next.scrollLockClass);
				}
			}
			opts = next;
		},
		destroy() {
			window.removeEventListener('keydown', onKeydown);
			if (opts.scrollLockClass) {
				document.body.classList.remove(opts.scrollLockClass);
			}
			restoreFocus?.focus();
		}
	};
}
