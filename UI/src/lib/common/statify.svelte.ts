type Statified<T> = T & {
	__statifyPerformed?: boolean;
};

/**
 * Makes a plain class instance reactive in Svelte: every enumerable public field is replaced with a
 * `$state`-backed accessor, recursing into nested class instances and into array elements. Use it to
 * keep complex state/logic in object-oriented classes (the battle simulation, the player/inventory
 * managers) while their fields still drive `$derived`/`$effect`, rather than spreading state across
 * stores or components.
 *
 * Arrays are reactive in place: `push`, `splice`, and index assignment all notify consumers, and any
 * class instance inserted after construction is statified on the way in (so its own fields become
 * reactive too). There is no need to reassign a whole array (`this.items = [...this.items]`) to force
 * an update — mutate it directly. (Note: a `Map`/`Set` field is not special-cased, so reassign those
 * to signal a change.)
 *
 * `#private` fields are invisible to `statify` and stay non-reactive — the escape hatch for state that
 * must not be deep-proxied (see `BattleAttributes`, whose modifier graph relies on reference
 * identity). Idempotent: a non-class value, or an instance already statified (flagged by
 * `__statifyPerformed`), is returned unchanged.
 */
export const statify = <T>(state: T) => {
	const statified = state as Statified<T>;
	if (statified?.__statifyPerformed || !isClass(typeof state, state)) {
		return state;
	}

	statified.__statifyPerformed = true;

	for (const property in state) {
		if (property === '__statifyPerformed') {
			continue;
		}

		const data = statified[property];
		const dataType = typeof data;
		if (dataType === 'function') {
			continue;
		}

		if (isClass(dataType, data)) {
			defineStatifiedClassProp<T & object>(state, property, data);
		} else if (Array.isArray(data)) {
			defineProxiedArrayProp<T & object>(state, property, data);
		} else {
			defineStandardProp<T & object>(state, property, data);
		}
	}

	return statified;
};

const defineStandardProp = <T extends object>(
	statified: T,
	property: Extract<keyof T, string>,
	data: T[Extract<keyof T, string>]
) => {
	let state = $state(data);
	Object.defineProperty(statified, property, {
		get() {
			return state;
		},
		set(value) {
			state = value;
		}
	});
};

const defineStatifiedClassProp = <T extends object>(
	statified: T,
	property: Extract<keyof T, string>,
	data: T[Extract<keyof T, string>]
) => {
	let state = $state(statify(data));
	Object.defineProperty(statified, property, {
		get() {
			return state;
		},
		set(value) {
			state = statify(value);
		}
	});
};

const defineProxiedArrayProp = <T extends object>(
	statified: T,
	property: Extract<keyof T, string>,
	data: T[Extract<keyof T, string>] & unknown[]
) => {
	statifyArrayValues(data);
	let state = $state(data);
	let stateProxy = $state.raw(proxyArray(state));
	Object.defineProperty(statified, property, {
		get() {
			return stateProxy;
		},
		set(value) {
			statifyArrayValues(value);
			state = value;
			stateProxy = proxyArray(state);
		}
	});
};

const isClass = (dataType: string, data: unknown): data is object => {
	return (
		dataType === 'object' &&
		data?.constructor?.name !== undefined &&
		data.constructor.name !== 'Object' &&
		!Array.isArray(data)
	);
};

const statifyArrayValues = <T extends unknown[]>(data: T) => {
	for (let i = 0; i < data.length; i++) {
		statify(data[i]);
	}
};

const proxyArray = <T extends unknown[]>(data: T) => {
	return new Proxy(data, {
		set(target, prop, value) {
			return Reflect.set(target, prop, statify(value));
		}
	});
};
