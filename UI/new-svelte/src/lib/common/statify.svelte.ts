type Statified<T extends {}> = T & {
	__statifyPerformed: boolean;
};

enum StatifyType {
	None,
	Class,
	Array
}

export const statify = <T extends {}>(state: T) => {
	const statified = state as Statified<T>;
	if (statified?.__statifyPerformed || !isClass(typeof state, state)) {
		return state;
	}
	statified.__statifyPerformed = true;

	for (const property in state) {
		let data = statified[property];
		const dataType = typeof data;
		let statifyType = StatifyType.None;
		if (typeof data !== 'function') {
			if (isClass(dataType, data)) {
				data = statify(data);
				statifyType = StatifyType.Class;
			} else if (isArray(data)) {
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

const isClass = (dataType: string, data: any): data is {} => {
	return (
		dataType === 'object' &&
		data?.constructor?.name !== undefined &&
		data.constructor.name !== 'Object' &&
		data.constructor.name !== 'Array'
	);
};

const isArray = (data: any): data is any[] => {
	return data?.constructor?.name === 'Array';
};

const statifyArray = (data: any) => {
	for (let i = 0; i < data.length; i++) {
		data[i] = statify(data[i]);
	}

	return new Proxy(data, {
		set(target, prop, value) {
			return Reflect.set(target, prop, statify(value));
		}
	});
};
