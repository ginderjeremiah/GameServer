// formats a number to a specific string representation
export function formatNum(num: number): string {
	return '' + parseFloat(num.toFixed(2));
}

export async function delay(delay: number) {
	return new Promise<void>((res) => {
		setTimeout(res, delay);
	});
}

/** Whether the OS `prefers-reduced-motion: reduce` preference is set (false during SSR). Use to
 *  skip JS-driven motion the global CSS rule can't collapse (e.g. a perpetual ticker). */
export function prefersReducedMotion(): boolean {
	return (
		typeof window !== 'undefined' &&
		!!window.matchMedia &&
		window.matchMedia('(prefers-reduced-motion: reduce)').matches
	);
}

export function keys<T>(obj?: T) {
	return obj ? (Object.keys(obj) as (keyof T)[]) : [];
}

export function enumPairs(obj: Record<string | number, string | number>) {
	const allKeys = keys(obj);
	return allKeys
		.slice(0, allKeys.length / 2)
		.map((key) => ({ id: Number(key), name: normalizeText(obj[key] as string) }));
}

/** Whether every bit in `flag` is set in the bitmask `value` (mirrors C# `[Flags]` `HasFlag`). */
export function hasFlag(value: number, flag: number): boolean {
	return (value & flag) === flag;
}

/** Returns `value` with `flag`'s bits set (`on`) or cleared (`!on`). */
export function toggleFlag(value: number, flag: number, on: boolean): number {
	return on ? value | flag : value & ~flag;
}

export function capitalize(str?: string) {
	if (!str) {
		return '';
	}
	return str[0].toUpperCase() + str.slice(1);
}

export function normalizeText(str?: string) {
	return capitalize(str).replaceAll(/([a-z])([A-Z])/g, '$1 $2');
}

export function plural(str: string) {
	const last = str.length - 1;
	switch (str.at(last)) {
		case 'y':
			return str.slice(0, last) + 'ies';
		case 'x':
		case 's':
			return str + 'es';
		default:
			return str + 's';
	}
}

/**
 * Any CSS colour (hex, named, or a `var(--x)` reference) at a given opacity,
 * expressed with `color-mix` so theme overrides of the base colour flow through.
 * Prefer this over hard-coded rgba blending for themeable colours.
 */
export function tintColor(color: string, alpha: number): string {
	return `color-mix(in srgb, ${color} ${+(alpha * 100).toFixed(3)}%, transparent)`;
}

export function groupBy<T>(arr: T[], groupFn: (item: T) => string) {
	const ret: { [key: string]: T[] } = {};
	for (const t of arr) {
		const key = groupFn(t);
		if (ret[key]) {
			ret[key].push(t);
		} else {
			ret[key] = [t];
		}
	}
	return ret;
}
