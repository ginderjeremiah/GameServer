// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import type { Entity, EntityType } from '$lib/card-game';
import BoardEntity from '$routes/game/screens/card-game/loom/BoardEntity.svelte';

afterEach(cleanup);

const ent = (over: Partial<Entity> & { type: EntityType }): Entity => ({
	id: 1,
	start: 0,
	end: 3,
	resolved: false,
	label: 'X',
	...over
});

describe('BoardEntity', () => {
	it('renders an enemy channel with a windup, a labelled .ecl span and a tip', () => {
		const { container } = render(BoardEntity, {
			props: { entity: ent({ type: 'enemychannel', label: 'CHANNEL' }), x: 12, w: 90 }
		});
		const el = container.querySelector('.ent') as HTMLElement;
		expect(el.classList.contains('enemychannel')).toBe(true);
		expect(el.querySelector('.windup')).not.toBeNull();
		expect(el.querySelector('.ecl')?.textContent).toBe('CHANNEL');
		expect(el.querySelector('.tip')).not.toBeNull();
		// Geometry is driven by the parent-computed x/w.
		expect(el.style.transform).toBe('translateX(12px)');
		expect(el.style.width).toBe('90px');
	});

	it('renders a player attack with a windup + tip and a plain text label', () => {
		const { container } = render(BoardEntity, {
			props: { entity: ent({ type: 'attack', label: 'SLASH' }), x: 0, w: 30 }
		});
		const el = container.querySelector('.ent.attack') as HTMLElement;
		expect(el.querySelector('.windup')).not.toBeNull();
		expect(el.querySelector('.tip')).not.toBeNull();
		expect(el.querySelector('.ecl')).toBeNull();
		expect(el.textContent?.trim()).toBe('SLASH');
	});

	it('renders a defensive block as a cover span with no windup or tip', () => {
		const { container } = render(BoardEntity, {
			props: { entity: ent({ type: 'block', label: 'GUARD' }), x: 0, w: 60 }
		});
		const el = container.querySelector('.ent.block') as HTMLElement;
		expect(el.querySelector('.windup')).toBeNull();
		expect(el.querySelector('.tip')).toBeNull();
	});

	it('reflects resolved/cancelled state as classes', () => {
		const { container } = render(BoardEntity, {
			props: { entity: ent({ type: 'enemyhit', resolved: true, cancelled: true }), x: 0, w: 30 }
		});
		const el = container.querySelector('.ent') as HTMLElement;
		expect(el.classList.contains('resolved')).toBe(true);
		expect(el.classList.contains('cancelled')).toBe(true);
	});
});
