import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';

import AdminGlyph, { ADMIN_GLYPH_KINDS } from '$routes/admin/AdminGlyph.svelte';

afterEach(cleanup);

describe('AdminGlyph', () => {
	it.each(ADMIN_GLYPH_KINDS.map((kind) => [kind] as const))('draws visible geometry for %s', (kind) => {
		const { container } = render(AdminGlyph, { props: { kind } });
		// Regression guard for #1502: a kind with no template branch rendered an empty <svg>.
		expect(container.querySelectorAll('path, circle').length).toBeGreaterThan(0);
	});

	it('strokes with the accent color only when active', () => {
		const active = render(AdminGlyph, { props: { kind: 'gauge', active: true } });
		expect(active.container.querySelector('svg')?.getAttribute('stroke')).toBe('var(--accent)');
		cleanup();

		const idle = render(AdminGlyph, { props: { kind: 'gauge' } });
		expect(idle.container.querySelector('svg')?.getAttribute('stroke')).not.toBe('var(--accent)');
	});
});
