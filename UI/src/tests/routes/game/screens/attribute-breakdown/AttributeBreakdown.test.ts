import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EAttribute, EModifierType, type IBattlerAttribute } from '$lib/api';
import { EAttributeModifierSource, type AttributeModifier } from '$lib/battle';

const { mockPlayerManager, mockInventoryManager, staticData, playerProficiencies } = vi.hoisted(() => ({
	mockPlayerManager: {
		attributes: [] as IBattlerAttribute[],
		name: 'Aelara',
		level: 12,
		battleLockedBaseModifiers: [] as AttributeModifier[],
		battleSignaturePassiveModifier: undefined as unknown as (
			resolve: (attribute: EAttribute) => number
		) => AttributeModifier
	},
	mockInventoryManager: { equippedSlots: [] as unknown[] },
	staticData: { attributes: undefined as unknown },
	playerProficiencies: { battleModifiers: [] as AttributeModifier[] }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData, playerProficiencies }));

import AttributeBreakdown from '$routes/game/screens/attribute-breakdown/AttributeBreakdown.svelte';

beforeEach(() => {
	mockPlayerManager.attributes = [
		{ attributeId: EAttribute.Strength, amount: 14 },
		{ attributeId: EAttribute.Endurance, amount: 12 }
	];
	mockPlayerManager.battleLockedBaseModifiers = [];
	// A flat no-op signature passive — contributes nothing, so the breakdown omits it.
	mockPlayerManager.battleSignaturePassiveModifier = () => ({
		attribute: EAttribute.Strength,
		amount: 0,
		type: EModifierType.Additive,
		source: EAttributeModifierSource.Class
	});
	mockInventoryManager.equippedSlots = [];
	playerProficiencies.battleModifiers = [];
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

	it('surfaces the derived Toughness and the authored-only DamageReflection (#1330)', () => {
		// Toughness derives from Endurance, so it self-selects from the allocation alone. DamageReflection has
		// no core-attribute derivation — it appears only where authored, here via an equipped item — confirming
		// the breakdown surfaces the reworked mitigation stats.
		mockInventoryManager.equippedSlots = [
			{
				name: 'Spiked Shield',
				attributes: [{ attributeId: EAttribute.DamageReflection, amount: 0.25 }],
				appliedMods: []
			}
		];
		render(AttributeBreakdown);
		expect(screen.getByTestId(`attr-row-${EAttribute.Toughness}`)).toBeTruthy();
		expect(screen.getByTestId(`attr-row-${EAttribute.DamageReflection}`)).toBeTruthy();
	});
});
