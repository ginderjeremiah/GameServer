import { describe, it, expect } from 'vitest';
import { ERarity } from '$lib/api';
import { rarityColor, rarityGlow, rarityLabel, rarityLevel, rarityTint } from '$lib/common/rarity';

describe('rarity helpers', () => {
	it('maps each rarity to a distinct themeable colour variable', () => {
		const colors = Object.values(ERarity)
			.filter((v): v is ERarity => typeof v === 'number')
			.map((r) => rarityColor(r));
		expect(new Set(colors).size).toBe(6);
		expect(rarityColor(ERarity.Rare)).toBe('var(--rarity-rare)');
		expect(rarityColor(ERarity.Mythic)).toBe('var(--rarity-mythic)');
	});

	it('exposes the per-tier glow intensity variable', () => {
		expect(rarityGlow(ERarity.Legendary)).toBe('var(--rarity-legendary-glow)');
	});

	it('derives label and level from the enum', () => {
		expect(rarityLabel(ERarity.Epic)).toBe('Epic');
		expect(rarityLevel(ERarity.Common)).toBe(1);
		expect(rarityLevel(ERarity.Mythic)).toBe(6);
	});

	it('builds a color-mix tint at the given opacity', () => {
		expect(rarityTint(ERarity.Rare, 0.6)).toBe('color-mix(in srgb, var(--rarity-rare) 60%, transparent)');
		expect(rarityTint(ERarity.Common, 0.05 + 1 * 0.012)).toBe(
			'color-mix(in srgb, var(--rarity-common) 6.2%, transparent)'
		);
	});
});
