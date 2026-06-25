import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import PipTrack from '$routes/game/screens/proficiencies/PipTrack.svelte';

afterEach(() => cleanup());

const renderPips = (props: { level: number; maxLevel: number; milestoneLevels: number[] }) => {
	const { container } = render(PipTrack, props);
	return [...container.querySelectorAll('.pip')];
};

describe('PipTrack', () => {
	it('draws one pip per level up to the cap', () => {
		expect(renderPips({ level: 0, maxLevel: 10, milestoneLevels: [] })).toHaveLength(10);
		expect(renderPips({ level: 0, maxLevel: 5, milestoneLevels: [] })).toHaveLength(5);
	});

	it('fills pips up to (and including) the current level', () => {
		const filled = renderPips({ level: 3, maxLevel: 10, milestoneLevels: [] }).map((p) =>
			p.classList.contains('filled')
		);
		expect(filled).toEqual([true, true, true, false, false, false, false, false, false, false]);
	});

	it('marks milestone levels as diamonds, filling them like any other pip', () => {
		const pips = renderPips({ level: 5, maxLevel: 10, milestoneLevels: [5, 10] });
		expect(pips.filter((p) => p.classList.contains('milestone'))).toHaveLength(2);
		// Level 5 (index 4) is a reached milestone → filled diamond; level 10 (index 9) is unreached.
		expect(pips[4].classList.contains('milestone')).toBe(true);
		expect(pips[4].classList.contains('filled')).toBe(true);
		expect(pips[9].classList.contains('milestone')).toBe(true);
		expect(pips[9].classList.contains('filled')).toBe(false);
	});

	it('renders no milestone diamonds when there are none', () => {
		const pips = renderPips({ level: 2, maxLevel: 10, milestoneLevels: [] });
		expect(pips.some((p) => p.classList.contains('milestone'))).toBe(false);
	});

	it('exposes the level and cap as an accessible label', () => {
		const { container } = render(PipTrack, { level: 3, maxLevel: 10, milestoneLevels: [] });
		expect(container.querySelector('[aria-label="Level 3 of 10"]')).toBeTruthy();
	});
});
