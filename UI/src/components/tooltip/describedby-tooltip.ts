/**
 * Wires a focusable trigger's `aria-describedby` to the global tooltip container it surfaces, so a
 * screen reader announces the tooltip's explanation (not just the trigger's accessible name) when the
 * trigger is focused. Pass the `describedById` returned by `registerTooltipComponent` — directly for a
 * locally-owned tooltip, or off a tooltip controller for the shared/context-driven ones.
 *
 * The tooltip is a single global panel positioned by the tooltip store, so the association is to its
 * stable container (which carries `role="tooltip"`) rather than the relocated content; the description
 * is recomputed each time the trigger is focused, by which point the shared panel holds this trigger's
 * content. Pass `undefined` for a trigger that currently surfaces no tooltip (e.g. an unlocked arrow)
 * and the attribute is omitted. The action owns the attribute outright — call sites don't combine it
 * with an unrelated `aria-describedby`.
 */
export function describedByTooltip(node: HTMLElement, describedById: string | undefined) {
	const apply = (id: string | undefined) => {
		if (id == null) {
			node.removeAttribute('aria-describedby');
		} else {
			node.setAttribute('aria-describedby', id);
		}
	};

	apply(describedById);

	return {
		update: apply
	};
}
