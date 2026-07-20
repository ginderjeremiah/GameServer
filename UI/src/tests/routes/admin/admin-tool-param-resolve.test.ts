import { describe, it, expect } from 'vitest';
import { resolve } from '$app/paths';

/*
 * `+page.svelte`'s tool switch is the only `resolve(...)` call site in the codebase that passes a
 * query string rather than a bare pathname (every other call site is `resolve('/admin')`,
 * `resolve('/game')`, …). `admin-tool-param.test.ts` mocks `resolve` as an identity function, so it
 * never exercises the real implementation — pinned here instead, against the real (unmocked)
 * `$app/paths` module: `resolve_route` only substitutes bracketed `[param]` segments, so a literal
 * `?query` suffix rides through a plain pathname segment unchanged (#2219 review).
 */
describe('resolve() with an appended query string', () => {
	it('passes the query string through unchanged', () => {
		expect(resolve('/admin?tool=items')).toBe('/admin?tool=items');
	});

	it('resolves a bare pathname unchanged', () => {
		expect(resolve('/admin')).toBe('/admin');
	});
});
