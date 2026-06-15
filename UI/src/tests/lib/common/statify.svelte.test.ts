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

	// CHARACTERIZATION TEST documenting a known limitation (see follow-up issue): the `statifyArray`
	// set-trap that is meant to re-statify values pushed/assigned after construction never fires,
	// because the property is wrapped in `$state(...)` whose own deep proxy shadows the statify proxy.
	// As a result a class instance added to a statified array later is NOT made reactive at the field
	// level. This test pins the current behaviour so a future fix to `statify` updates it deliberately.
	it('does not field-reactify a class instance pushed after construction (known limitation)', () => {
		const outer = statify(new Outer());

		const pushed = new Inner();
		outer.items.push(pushed);

		let observedValue = -1;
		const cleanup = $effect.root(() => {
			const value = $derived(outer.items[1].value);
			$effect(() => {
				observedValue = value;
			});
		});

		flushSync();
		expect(observedValue).toBe(1);

		// Mutating the pushed instance's field does not propagate — it was never statified.
		outer.items[1].value = 99;
		flushSync();
		expect(observedValue).toBe(1);

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
