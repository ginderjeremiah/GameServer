type Statified<T extends object> = T & {
	__statifyPerformed: boolean;
};

enum StatifyType {
	None,
	Class,
	Array
}

export const statify = <T extends object>(state: T) => {
	const statified = state as Statified<T>;
	if (statified?.__statifyPerformed || !isClass(typeof state, state)) {
		return state;
	}
	statified.__statifyPerformed = true;

	for (const property in state) {
		let data = statified[property];
		const dataType = typeof data;
		let statifyType = StatifyType.None;
		if (dataType !== 'function') {
			if (isClass(dataType, data)) {
				data = statify(data);
				statifyType = StatifyType.Class;
			} else if (Array.isArray(data)) {
				data = statifyArray(data);
				statifyType = StatifyType.Array;
			}

			let statifiedProp = $state(data);
			Object.defineProperty(state, property, {
				get() {
					return statifiedProp;
				},
				set(value) {
					if (statifyType === StatifyType.Class) {
						value = statify(value);
					} else if (statifyType === StatifyType.Array) {
						value = statifyArray(value);
					}
					statifiedProp = value;
				}
			});
		}
	}

	return statified;
};

const isClass = (dataType: string, data: unknown): data is object => {
	return (
		dataType === 'object' &&
		data?.constructor?.name !== undefined &&
		data.constructor.name !== 'Object' &&
		!Array.isArray(data)
	);
};

const statifyArray = <T extends unknown[]>(data: T) => {
	for (let i = 0; i < data.length; i++) {
		const item = data[i];
		data[i] = statifyAnything(item);
	}

	return new Proxy(data, {
		set(target, prop, value) {
			return Reflect.set(target, prop, statifyAnything(value));
		}
	});
};

const statifyAnything = <T>(value: T) => {
	const dataType = typeof value;
	if (isClass(dataType, value)) {
		return statify(value);
	} else if (Array.isArray(value)) {
		return statifyArray(value);
	} else {
		return $state(value);
	}
};
