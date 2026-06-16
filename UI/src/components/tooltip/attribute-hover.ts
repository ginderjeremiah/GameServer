import type { EAttribute } from '$lib/api';
import type { AttributeTooltipController } from './attribute-tooltip.svelte';

export interface AttributeHoverParams {
	/** The screen-level controller, resolved by the owning component via `getAttributeTooltip()`. */
	controller: AttributeTooltipController | undefined;
	id: EAttribute;
}

/**
 * Opens the shared attribute tooltip for {@link AttributeHoverParams.id} while the element is
 * hovered or focused — anchored at the cursor on hover and off the element's box on focus — and
 * hides it on leave/blur. Replaces the per-row mouse/focus-handler boilerplate. The controller is
 * passed in (rather than read here) because an action runs after init and can't call `getContext`;
 * the owning component resolves it once via `getAttributeTooltip()`.
 */
export function attributeHover(node: HTMLElement, params: AttributeHoverParams) {
	let { controller, id } = params;

	const onEnter = (e: MouseEvent) => controller?.show(id, e);
	const onMove = (e: MouseEvent) => controller?.move(e);
	const onLeave = () => controller?.hide();
	const onFocus = () => controller?.show(id, node);
	const onBlur = () => controller?.hide();

	node.addEventListener('mouseenter', onEnter);
	node.addEventListener('mousemove', onMove);
	node.addEventListener('mouseleave', onLeave);
	node.addEventListener('focus', onFocus);
	node.addEventListener('blur', onBlur);

	return {
		update(next: AttributeHoverParams) {
			({ controller, id } = next);
		},
		destroy() {
			node.removeEventListener('mouseenter', onEnter);
			node.removeEventListener('mousemove', onMove);
			node.removeEventListener('mouseleave', onLeave);
			node.removeEventListener('focus', onFocus);
			node.removeEventListener('blur', onBlur);
		}
	};
}
