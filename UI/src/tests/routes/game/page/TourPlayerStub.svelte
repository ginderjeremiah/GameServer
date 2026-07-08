<!-- Test stub for TourPlayer: surfaces `open`/`label`/`steps` as data/text so the page test can assert
     the shell wired them from `tutorialTour`, and exposes dismiss/complete buttons wired to the real
     callbacks, without the real coach-mark overlay. -->
<div data-testid="tour-player" data-open={open} data-step-count={steps.length}>
	{label}
	<button data-testid="tour-player-dismiss" onclick={onDismiss}>Dismiss</button>
	<button data-testid="tour-player-complete" onclick={onComplete}>Complete</button>
</div>

<script lang="ts">
interface Step {
	text: string;
	anchorKey?: string;
}

const {
	open,
	steps,
	label,
	onDismiss,
	onComplete
}: {
	open: boolean;
	steps: Step[];
	label: string;
	onDismiss: () => void;
	onComplete: () => void;
} = $props();
</script>
