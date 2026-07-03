import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';

import TypeGlyph from '$routes/game/screens/challenges/TypeGlyph.svelte';
import { EChallengeType } from '$lib/api';

afterEach(cleanup);

/** Every numeric member of the enum — a new challenge type joins this matrix automatically. */
const allTypes = Object.values(EChallengeType).filter((v): v is EChallengeType => typeof v === 'number');

describe('TypeGlyph', () => {
	it.each(allTypes.map((typeId) => [EChallengeType[typeId], typeId] as const))(
		'draws visible geometry for %s',
		(_name, typeId) => {
			const { container } = render(TypeGlyph, { props: { typeId, color: 'var(--accent)' } });
			const paths = [...container.querySelectorAll('path')];
			// Regression guard for #1502: a type missing from GLYPH_PATH rendered <path d={undefined}>.
			expect(paths.length).toBeGreaterThan(0);
			for (const path of paths) {
				expect(path.getAttribute('d')).toBeTruthy();
			}
		}
	);

	it('draws the TimeTrial clock (circle + hands) rather than a GLYPH_PATH entry', () => {
		const { container } = render(TypeGlyph, {
			props: { typeId: EChallengeType.TimeTrial, color: 'var(--accent)' }
		});
		expect(container.querySelector('circle')).not.toBeNull();
	});

	it('applies the accent color and sizing props to the svg', () => {
		const { container } = render(TypeGlyph, {
			props: { typeId: EChallengeType.EnemiesKilled, color: 'var(--challenge-enemies-killed)', size: 24 }
		});
		const svg = container.querySelector('svg');
		expect(svg?.getAttribute('stroke')).toBe('var(--challenge-enemies-killed)');
		expect(svg?.getAttribute('width')).toBe('24');
		expect(svg?.getAttribute('height')).toBe('24');
	});
});
