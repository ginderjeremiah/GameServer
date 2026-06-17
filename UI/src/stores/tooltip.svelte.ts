import { onDestroy } from 'svelte';
import { SvelteMap } from 'svelte/reactivity';

export interface TooltipComponent {
	// May be undefined until the tooltip component's root element has mounted.
	getBaseNode: () => HTMLDivElement | undefined;
}

export interface TooltipData {
	id: number;
	component: () => TooltipComponent | undefined;
	position?: Position;
	visible: boolean;
}

export interface Position {
	x: number;
	y: number;
}

/** A tooltip anchor: a pointer event (positioned at the cursor) or an element
 *  (positioned off its box), so a tooltip is reachable by both mouse and keyboard focus. */
export type TooltipAnchor = MouseEvent | HTMLElement;

/**
 * The DOM id given to a tooltip's container, shared between the rendered container ({@link Tooltip})
 * and any focusable trigger that references it via `aria-describedby` (see `describedByTooltip`). The
 * registration's numeric id is the single source of this id, so the two sides always agree.
 */
export const tooltipElementId = (id: number): string => `tooltip-${id}`;

/** Resolve a tooltip {@link Position} from a cursor (pointer) or an element's box (focus). */
export const anchorPosition = (anchor: TooltipAnchor): Position => {
	if (anchor instanceof HTMLElement) {
		const rect = anchor.getBoundingClientRect();
		return { x: rect.left + rect.width / 2, y: rect.bottom };
	}
	return { x: anchor.clientX, y: anchor.clientY };
};

// Whether the user's most recent focus-moving interaction came from the keyboard rather than a
// pointer. A mouse click also focuses the element it hits, but that element's hover handlers are
// already tracking the cursor, so re-anchoring the tooltip off the element's box on that focus would
// make it jump away from the pointer (#880). `:focus-visible` isn't reliably readable inside a focus
// handler, so we mirror its heuristic from the raw input events: keydown ⇒ keyboard, pointer ⇒ mouse.
let focusViaKeyboard = true;
if (typeof document !== 'undefined') {
	// Capture phase so the modality is recorded before the focus the interaction triggers fires.
	document.addEventListener('keydown', () => (focusViaKeyboard = true), true);
	document.addEventListener('pointerdown', () => (focusViaKeyboard = false), true);
}

/**
 * The element a tooltip should anchor off of for a focus event, or `undefined` when the focus was a
 * mouse/pointer click — whose hover handlers already track the cursor, so re-anchoring off the box
 * would make the tooltip jump. Keyboard focus has no cursor, so it still pins the tooltip to the box.
 */
export const focusAnchor = (event: FocusEvent): HTMLElement | undefined =>
	focusViaKeyboard && event.currentTarget instanceof HTMLElement ? event.currentTarget : undefined;

// Keyed by the tooltip's stable id rather than held in a reactive array. An
// array relied on `findIndex(... === data)` + `splice` to unregister, which is
// not robust when screens overlap during navigation (the new screen mounts its
// tooltips before the old screen's onDestroy runs): the interleaved push/splice
// on the reactive array dropped the wrong entries and left undefined holes,
// causing tooltips to disappear when switching between screens that both render
// them (e.g. Fight <-> Inventory). Removing by id makes unregistration
// reference-independent and order-independent.
const tooltipsData = new SvelteMap<number, TooltipData>();

export const tooltips = {
	get data() {
		return tooltipsData.values();
	}
};

let id = 1;

export const registerTooltipComponent = <T extends TooltipComponent>(component: () => T | undefined) => {
	const tooltipId = id++;
	const data = $state<TooltipData>({ id: tooltipId, component, visible: false, position: undefined });

	const setTooltipPosition = (position: Position) => {
		data.position = position;
	};

	const showTooltip = () => {
		data.visible = true;
	};

	const hideTooltip = () => {
		data.visible = false;
	};

	tooltipsData.set(tooltipId, data);

	onDestroy(() => {
		tooltipsData.delete(tooltipId);
	});

	return {
		/**
		 * Stable DOM id of this tooltip's container, for wiring a focusable trigger's `aria-describedby`
		 * (via `describedByTooltip`) so assistive tech announces the tooltip's explanation on focus.
		 */
		describedById: tooltipElementId(tooltipId),
		setTooltipPosition,
		showTooltip,
		hideTooltip
	};
};
