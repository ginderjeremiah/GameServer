<!--
	Anchored tutorial tour ("coach-marks"): highlights a registered {@link tutorialAnchor} target and
	positions a callout beside it with the current step's text and next/back/done controls. Distinct
	from `Popover` (a container overlay with no target anchoring) — this positions relative to a real
	on-screen control, and degrades to a centered, unanchored callout when the step names no anchor or
	the anchor isn't (yet) registered, rather than failing.

	A controlled component: the caller owns `open`/`steps` and is told about early dismissal
	(`onDismiss` — Escape, backdrop, Skip) versus finishing the last step (`onComplete`) separately, so
	it can decide what each means for the lesson's read state.
-->
{#if open && currentStep}
	<div class="tour-layer" data-testid="tutorial-tour">
		<button class="tour-backdrop" type="button" tabindex="-1" aria-label="Dismiss tour" onclick={onDismiss}></button>

		{#if spotlightRect}
			<div
				class="tour-spotlight"
				style:top="{spotlightRect.top}px"
				style:left="{spotlightRect.left}px"
				style:width="{spotlightRect.width}px"
				style:height="{spotlightRect.height}px"
			></div>
		{/if}

		<div
			class="tour-callout"
			class:tour-callout--centered={!calloutPos}
			role="dialog"
			aria-modal="true"
			aria-label={label}
			data-tour-position={calloutPos ? 'anchored' : 'centered'}
			tabindex="-1"
			bind:this={shell}
			style:top={calloutPos ? `${calloutPos.top}px` : undefined}
			style:left={calloutPos ? `${calloutPos.left}px` : undefined}
			use:focusTrap={{ onEscape: onDismiss, scrollLockClass: 'tour-open' }}
		>
			<div class="tour-callout__body" aria-live="polite">
				<p class="tour-callout__step">{stepIndex + 1} of {steps.length}</p>
				<p class="tour-callout__text">{currentStep.text}</p>
			</div>
			<div class="tour-callout__controls">
				<button class="tour-callout__skip" type="button" data-testid="tutorial-tour-skip" onclick={onDismiss}
					>Skip</button
				>
				<div class="tour-callout__nav">
					<button type="button" disabled={isFirst} onclick={goBack}>Back</button>
					{#if isLast}
						<button type="button" bind:this={primaryButton} onclick={onComplete}>Done</button>
					{:else}
						<button type="button" bind:this={primaryButton} onclick={goNext}>Next</button>
					{/if}
				</div>
			</div>
		</div>
	</div>
{/if}

<script lang="ts">
import { focusTrap } from '../focus-trap';
import { getTutorialAnchor } from './tutorial-anchor';
import type { TourStep } from './tour-types';

interface Props {
	/** Whether the tour is shown. */
	open: boolean;
	/** The tour's ordered steps. Resets to the first step whenever `open` transitions to `true`. */
	steps: TourStep[];
	/** Accessible name for the dialog (e.g. the lesson's display name). */
	label: string;
	/** Called on early dismissal — Escape, backdrop click, or the Skip control. */
	onDismiss: () => void;
	/** Called when the player finishes the tour via Done on the last step. */
	onComplete: () => void;
}

const { open, steps, label, onDismiss, onComplete }: Props = $props();

let stepIndex = $state(0);
let shell = $state<HTMLElement | null>(null);
let primaryButton = $state<HTMLButtonElement | null>(null);
let calloutPos = $state<{ top: number; left: number } | null>(null);
let spotlightRect = $state<{ top: number; left: number; width: number; height: number } | null>(null);

// A fresh open always starts from the first step; steps changing while already open (not a supported
// use — reopen per lesson instead) deliberately leaves the current position alone.
$effect(() => {
	if (open) {
		stepIndex = 0;
	}
});

const currentStep = $derived(steps[stepIndex] as TourStep | undefined);
const isFirst = $derived(stepIndex === 0);
const isLast = $derived(stepIndex === steps.length - 1);
const anchorEl = $derived(currentStep?.anchorKey ? getTutorialAnchor(currentStep.anchorKey) : undefined);

const goBack = () => {
	if (!isFirst) {
		stepIndex -= 1;
	}
};
const goNext = () => {
	if (!isLast) {
		stepIndex += 1;
	}
};

// Recomputed on resize so a viewport change while the tour is open doesn't leave it pointing at a
// stale rect (getBoundingClientRect is a snapshot, not itself reactive).
let resizeTick = $state(0);
$effect(() => {
	if (!open) {
		return;
	}
	const onResize = () => (resizeTick += 1);
	window.addEventListener('resize', onResize);
	return () => window.removeEventListener('resize', onResize);
});

const GAP = 12;
const EDGE_PADDING = 8;
const SPOTLIGHT_PADDING = 6;

$effect(() => {
	void resizeTick;
	// Re-run on every step change too, not just anchor/viewport changes: consecutive steps sharing an
	// anchorKey (or both unanchored) leave `anchorEl` unchanged, but the callout's rendered height can
	// still differ with the new step's text.
	void stepIndex;
	if (!open || !anchorEl) {
		spotlightRect = null;
		calloutPos = null;
		return;
	}

	const rect = anchorEl.getBoundingClientRect();
	spotlightRect = {
		top: rect.top - SPOTLIGHT_PADDING,
		left: rect.left - SPOTLIGHT_PADDING,
		width: rect.width + SPOTLIGHT_PADDING * 2,
		height: rect.height + SPOTLIGHT_PADDING * 2
	};

	if (!shell) {
		return;
	}
	// Measure the positioned shell itself (padding + border included), not the inner text body —
	// otherwise the box is under-reported by the shell's own padding/border on every side.
	const calloutWidth = shell.offsetWidth;
	const calloutHeight = shell.offsetHeight;

	let top = rect.bottom + GAP;
	if (top + calloutHeight + EDGE_PADDING > window.innerHeight) {
		top = rect.top - calloutHeight - GAP;
	}
	top = Math.max(EDGE_PADDING, Math.min(top, window.innerHeight - calloutHeight - EDGE_PADDING));

	let left = rect.left + rect.width / 2 - calloutWidth / 2;
	left = Math.max(EDGE_PADDING, Math.min(left, window.innerWidth - calloutWidth - EDGE_PADDING));

	calloutPos = { top, left };
});

// Initial focus lands on the primary action (Next/Done) rather than the first focusable control
// (Skip), so a keyboard user driving the tour via Enter is never bounced onto Skip. `primaryButton`
// is a fresh DOM node only when Next/Done swap (isLast flips) — not on every step within the same
// Next/Back pair — so this naturally leaves focus wherever the user already put it (e.g. clicked
// Back) undisturbed. Trap/Escape/scroll-lock/restore are owned by focusTrap.
$effect(() => {
	if (open) {
		(primaryButton ?? shell)?.focus();
	}
});
</script>

<style lang="scss">
:global(body.tour-open) {
	overflow: hidden;
}

.tour-layer {
	position: fixed;
	inset: 0;
	z-index: 60;
}

.tour-backdrop {
	position: absolute;
	inset: 0;
	padding: 0;
	border: none;
	cursor: pointer;
	background: color-mix(in srgb, var(--black) 40%, transparent);
}

.tour-spotlight {
	position: fixed;
	border-radius: var(--border-radius);
	// The oversized box-shadow paints the dimmed backdrop everywhere *except* this box, giving the
	// highlighted control a cutout without needing an actual clip-path mask.
	box-shadow:
		0 0 0 9999px color-mix(in srgb, var(--black) 40%, transparent),
		0 0 0 2px var(--accent);
	pointer-events: none;
}

.tour-callout {
	position: fixed;
	max-width: 320px;
	background: var(--tooltip-bg);
	border: 1px solid var(--border-light);
	border-radius: var(--border-radius);
	box-shadow:
		0 12px 28px color-mix(in srgb, var(--black) 55%, transparent),
		0 0 0 1px color-mix(in srgb, var(--black) 40%, transparent);
	backdrop-filter: blur(6px);
	color: var(--text-primary);
	outline: none;
	padding: 16px;

	&--centered {
		top: 50%;
		left: 50%;
		transform: translate(-50%, -50%);
	}
}

.tour-callout__step {
	margin: 0 0 4px;
	font-size: 0.75rem;
	color: var(--text-tertiary);
}

.tour-callout__text {
	margin: 0 0 12px;
	line-height: 1.4;
}

.tour-callout__controls {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
}

.tour-callout__skip {
	background: none;
	border: none;
	color: var(--text-secondary);
	cursor: pointer;
	padding: 0;
}

.tour-callout__nav {
	display: flex;
	gap: 8px;
}
</style>
