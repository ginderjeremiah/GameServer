<button
	type="button"
	class="overlay-button"
	{draggable}
	aria-label={label}
	onclick={onActivate}
	ondblclick={onDoubleClick}
	ondragstart={onDragStart}
	ondragend={onDragEnd}
></button>

<script lang="ts">
interface Props {
	/** Accessible name for the action — the card has no visible button text of its own. */
	label: string;
	draggable?: boolean;
	onActivate?: (e: MouseEvent) => void;
	onDoubleClick?: (e: MouseEvent) => void;
	onDragStart?: (e: DragEvent) => void;
	onDragEnd?: (e: DragEvent) => void;
}

const { label, draggable = false, onActivate, onDoubleClick, onDragStart, onDragEnd }: Props = $props();
</script>

<style lang="scss">
// A full-bleed primary action that turns a presentational card into an accessible control:
// it stretches over the whole card beneath any sibling action buttons, giving the card real
// <button> semantics (native keyboard + focus) without nesting interactive elements.
.overlay-button {
	position: absolute;
	inset: 0;
	z-index: 1;
	width: 100%;
	height: 100%;
	margin: 0;
	padding: 0;
	border: none;
	border-radius: inherit;
	background: transparent;
	appearance: none;
	// Inherit the card's cursor (grab on a drag-source card, pointer on a static one).
	cursor: inherit;

	// Drawn inset so the ring stays visible inside the card's `overflow: hidden` clip.
	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: -2px;
	}
}
</style>
