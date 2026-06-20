import { describe, it, expect } from 'vitest';
import type { LevelRange } from '$routes/game/screens/codex/enemy-level';
import {
	enemyAccent,
	enemyKindLabel,
	formatBand,
	formatCooldown,
	tabAccent
} from '$routes/game/screens/codex/codex-display';

describe('formatBand', () => {
	it('shows a fixed boss level as L{n}', () => {
		expect(formatBand({ min: 10, max: 10, fixed: true } as LevelRange)).toBe('L10');
	});

	it('shows a ranged band as min–max', () => {
		expect(formatBand({ min: 18, max: 28, fixed: false } as LevelRange)).toBe('18–28');
	});
});

describe('formatCooldown', () => {
	it('renders a utility (zero) cooldown as a dash', () => {
		expect(formatCooldown(0)).toBe('—');
	});

	it('renders whole-second cooldowns without a decimal', () => {
		expect(formatCooldown(2000)).toBe('2s');
		expect(formatCooldown(6000)).toBe('6s');
	});

	it('renders sub-second precision to one decimal', () => {
		expect(formatCooldown(1800)).toBe('1.8s');
		expect(formatCooldown(900)).toBe('0.9s');
	});
});

describe('accents + labels', () => {
	it('tints a boss gold and a normal enemy salmon', () => {
		expect(enemyAccent(true)).toBe('var(--boss-accent)');
		expect(enemyAccent(false)).toBe('var(--enemy-accent)');
	});

	it('labels the enemy kind', () => {
		expect(enemyKindLabel(true)).toBe('Boss');
		expect(enemyKindLabel(false)).toBe('Enemy');
	});

	it('maps each tab to its themed section accent', () => {
		expect(tabAccent('enemies')).toBe('var(--enemy-accent)');
		expect(tabAccent('zones')).toBe('var(--accent)');
		expect(tabAccent('skills')).toBe('var(--attr-intellect)');
	});
});
