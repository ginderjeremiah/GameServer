import { describe, it, expect } from 'vitest';
import { flushSync } from 'svelte';
import { statify } from '$lib/common';

class Inner {
	value = 1;
}

class Outer {
	count = 0;
	inner = new Inner();
	items: Inner[] = [new Inner()];
}

// Reads the `__statifyPerformed` idempotency marker the utility stamps on every processed object.
const wasStatified = (obj: unknown) => (obj as { __statifyPerformed?: boolean }).__statifyPerformed === true;

describe('statify', () => {
	it('returns non-class values unchanged', () => {
		expect(statify(5)).toBe(5);
		expect(statify('hello')).toBe('hello');
		expect(statify(null)).toBe(null);

		// A plain object (constructor.name === 'Object') is not treated as a class and is left as-is.
		const plain = { a: 1 };
		expect(statify(plain)).toBe(plain);
		expect(wasStatified(plain)).toBe(false);

		// A bare array at the top level is not a class either, so it is returned as the same reference.
		const arr = [1, 2, 3];
		expect(statify(arr)).toBe(arr);
	});

	it('stamps the idempotency guard and is a no-op on re-entry', () => {
		const outer = statify(new Outer());
		expect(wasStatified(outer)).toBe(true);

		// A second call short-circuits on the guard and returns the same instance untouched.
		expect(statify(outer)).toBe(outer);
	});

	it('stamps the idempotency guard as non-enumerable', () => {
		const outer = statify(new Outer());

		expect(Object.keys(outer)).not.toContain('__statifyPerformed');
		expect(JSON.stringify(outer)).not.toContain('__statifyPerformed');
	});

	it('recursively statifies nested class instances and array elements', () => {
		const outer = statify(new Outer());

		expect(wasStatified(outer.inner)).toBe(true);
		expect(wasStatified(outer.items[0])).toBe(true);
	});

	it('reacts to structural changes on a statified array', () => {
		const outer = statify(new Outer());

		let observedLength = -1;
		const cleanup = $effect.root(() => {
			const length = $derived(outer.items.length);
			$effect(() => {
				observedLength = length;
			});
		});

		flushSync();
		expect(observedLength).toBe(1);

		outer.items.push(new Inner());
		flushSync();
		expect(observedLength).toBe(2);

		cleanup();
	});

	it('makes class properties reactive to a $derived read', () => {
		const outer = statify(new Outer());

		let observed = -1;
		const cleanup = $effect.root(() => {
			const derivedCount = $derived(outer.count);
			$effect(() => {
				observed = derivedCount;
			});
		});

		flushSync();
		expect(observed).toBe(0);

		outer.count = 7;
		flushSync();
		expect(observed).toBe(7);

		cleanup();
	});

	it('re-statifies a class-typed property reassigned through its setter', () => {
		const outer = statify(new Outer());

		const replacement = new Inner();
		outer.inner = replacement;

		// The setter routes a class-typed assignment back through statify.
		expect(wasStatified(replacement)).toBe(true);
	});
});
