import { describe, it, expect } from 'vitest';

/* Theming rule (docs/frontend.md → Styling): all colours must be CSS variables, never hard-coded hex,
   so a future theme can restyle every surface by overriding the tokens alone. This guards the Synthesis
   screen sources against a literal hex colour slipping back in. Vite resolves the glob at transform time,
   so the test reads the real component sources as raw strings. */
const sources = import.meta.glob('/src/routes/game/screens/synthesis/**/*.{svelte,ts}', {
	query: '?raw',
	import: 'default',
	eager: true
}) as Record<string, string>;

// A 3/4/6/8-digit hex run. Decimal-only runs (e.g. `#1125` issue references in comments) are not
// colours, so a colour match must contain at least one a–f letter.
const HEX = /#[0-9a-fA-F]{3,8}\b/g;
const isHexColour = (match: string): boolean => /[a-f]/i.test(match.slice(1));

describe('Synthesis screen token compliance', () => {
	const entries = Object.entries(sources);

	it('scans every synthesis screen source', () => {
		// Guard against the glob silently matching nothing (which would make the suite vacuously pass).
		expect(entries.length).toBeGreaterThan(0);
	});

	for (const [path, raw] of entries) {
		it(`${path} uses theme tokens, not literal hex colours`, () => {
			const hexColours = (raw.match(HEX) ?? []).filter(isHexColour);
			expect(hexColours).toEqual([]);
		});
	}
});
