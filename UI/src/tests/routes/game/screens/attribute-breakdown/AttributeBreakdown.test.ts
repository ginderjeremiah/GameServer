import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EAttribute, type IBattlerAttribute } from '$lib/api';

const { mockPlayerManager, mockInventoryManager, staticData } = vi.hoisted(() => ({
	mockPlayerManager: { attributes: [] as IBattlerAttribute[], name: 'Aelara', level: 12 },
	mockInventoryManager: { equippedSlots: [] as unknown[] },
	staticData: { attributes: undefined as unknown }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData }));

import AttributeBreakdown from '$routes/game/screens/attribute-breakdown/AttributeBreakdown.svelte';

beforeEach(() => {
	mockPlayerManager.attributes = [
		{ attributeId: EAttribute.Strength, amount: 14 },
		{ attributeId: EAttribute.Endurance, amount: 12 }
	];
	mockInventoryManager.equippedSlots = [];
	staticData.attributes = undefined;
});

afterEach(() => cleanup());

describe('Attribute Breakdown screen', () => {
	it('renders the rail with every implemented attribute and the player header', () => {
		render(AttributeBreakdown);
		expect(screen.getByTestId('attribute-breakdown-screen')).toBeTruthy();
		expect(screen.getByText(/Aelara · Level 12/)).toBeTruthy();
		expect(screen.getByTestId('attribute-list')).toBeTruthy();
		// 6 core + 3 derived rows.
		expect(screen.getByTestId(`attr-row-${EAttribute.Strength}`)).toBeTruthy();
		expect(screen.getByTestId(`attr-row-${EAttribute.MaxHealth}`)).toBeTruthy();
		expect(screen.getByTestId(`attr-row-${EAttribute.CooldownRecovery}`)).toBeTruthy();
	});

	it('defaults the detail to Max Health and shows its derived total', () => {
		render(AttributeBreakdown);
		const detail = screen.getByTestId('breakdown-detail');
		// MaxHealth = 50 + 20×END(12) + 5×STR(14) = 360.
		expect(detail.textContent).toContain('360');
		// The by-source + apply-order sections render for a contributing attribute.
		expect(screen.getByText('By source')).toBeTruthy();
		expect(screen.getByText('Apply order')).toBeTruthy();
	});

	it('selects a different attribute from the rail', async () => {
		render(AttributeBreakdown);
		await fireEvent.click(screen.getByTestId(`attr-row-${EAttribute.Strength}`));
		const detail = screen.getByTestId('breakdown-detail');
		// Strength is 14 from the allocation; the big value reflects the selection.
		expect(detail.textContent).toContain('14');
	});

	it('associates each rail row with the shared attribute tooltip via aria-describedby', () => {
		render(AttributeBreakdown);
		// So a screen reader announces the attribute explanation (not just the row name) on focus.
		const row = screen.getByTestId(`attr-row-${EAttribute.Strength}`);
		expect(row.getAttribute('aria-describedby')).toMatch(/^tooltip-\d+$/);
	});
});
