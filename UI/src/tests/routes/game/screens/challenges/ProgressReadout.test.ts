import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import type { ChallengeVM, ProgressInfo } from '$routes/game/screens/challenges/challenges-view.svelte';

vi.mock('$stores', () => ({ staticData: {} }));

import ProgressReadout from '$routes/game/screens/challenges/ProgressReadout.svelte';

const makeVM = (prog: Partial<ProgressInfo>, over: Partial<ChallengeVM> = {}): ChallengeVM =>
	({
		state: 'active',
		unit: 'kills',
		typeAccent: 'var(--challenge-enemies-killed)',
		prog: { atMost: false, percent: 50, value: 5, goal: 10, best: 0, target: 0, hasData: true, ...prog },
		...over
	}) as unknown as ChallengeVM;

afterEach(cleanup);

describe('ProgressReadout — accumulating (atLeast) goal', () => {
	it('shows the count, goal, unit and percent for an in-progress challenge', () => {
		const { container } = render(ProgressReadout, { props: { c: makeVM({ percent: 50, value: 5, goal: 10 }) } });
		const text = (container.querySelector('.count') as HTMLElement).textContent ?? '';
		expect(text).toContain('5');
		expect(text).toContain('/ 10');
		expect(text).toContain('kills');
		expect(screen.getByText('50%')).toBeTruthy();
	});

	it('omits the percent once the challenge is done', () => {
		const { container } = render(ProgressReadout, {
			props: { c: makeVM({ percent: 100, value: 10, goal: 10 }, { state: 'done' }) }
		});
		expect(container.querySelector('.pct')).toBeNull();
		// The done value carries the success accent class.
		expect(container.querySelector('.count .done')).toBeTruthy();
	});
});

describe('ProgressReadout — minimisation (atMost) goal', () => {
	it('shows the best time and the beat target when a qualifying value exists', () => {
		const { container } = render(ProgressReadout, {
			props: { c: makeVM({ atMost: true, percent: 80, best: 90, target: 75, hasData: true }, { unit: 'time' }) }
		});
		expect((container.querySelector('.best-value') as HTMLElement).textContent?.trim()).toBe('1:30');
		expect(screen.getByText('best')).toBeTruthy();
		expect((container.querySelector('.beat-target') as HTMLElement).textContent).toContain('≤ 1:15');
	});

	it('reads "no time yet" and hides the best label when no value is recorded', () => {
		const { container } = render(ProgressReadout, {
			props: { c: makeVM({ atMost: true, percent: 0, best: 0, target: 75, hasData: false }, { unit: 'time' }) }
		});
		expect((container.querySelector('.best-value') as HTMLElement).textContent?.trim()).toBe('no time yet');
		expect(screen.queryByText('best')).toBeNull();
	});
});

describe('ProgressReadout — bar toggle', () => {
	it('renders the progress bar by default', () => {
		const { container } = render(ProgressReadout, { props: { c: makeVM({}) } });
		expect(container.querySelector('.bar-track')).toBeTruthy();
	});

	it('omits the bar when showBar is false', () => {
		const { container } = render(ProgressReadout, { props: { c: makeVM({}), showBar: false } });
		expect(container.querySelector('.bar-track')).toBeNull();
	});
});
