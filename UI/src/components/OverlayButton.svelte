<button
	type="button"
	class="overlay-button"
	{draggable}
	aria-label={label}
	onclick={onActivate}
	ondblclick={onDoubleClick}
	ondragstart={onDragStart}
	ondragend={onDragEnd}
	onfocus={onFocus}
	onblur={onBlur}
	use:describedByTooltip={describedById}
></button>

<script lang="ts">
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';

interface Props {
	/** Accessible name for the action — the card has no visible button text of its own. */
	label: string;
	draggable?: boolean;
	/**
	 * Stable id of the tooltip this control surfaces, wired onto its `aria-describedby` so a screen
	 * reader announces the explanation on focus. The card's tooltip lives on its presentational
	 * container (hover-anchored), but the focusable control is this button, so the association sits here.
	 */
	describedById?: string;
	onActivate?: (e: MouseEvent) => void;
	onDoubleClick?: (e: MouseEvent) => void;
	onDragStart?: (e: DragEvent) => void;
	onDragEnd?: (e: DragEvent) => void;
	/** Focus/blur on the button itself, so a card's tooltip can surface on keyboard focus. */
	onFocus?: (e: FocusEvent) => void;
	onBlur?: (e: FocusEvent) => void;
}

const {
	label,
	draggable = false,
	describedById,
	onActivate,
	onDoubleClick,
	onDragStart,
	onDragEnd,
	onFocus,
	onBlur
}: Props = $props();
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
