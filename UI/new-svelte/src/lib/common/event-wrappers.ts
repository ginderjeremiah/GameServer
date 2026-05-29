export const preventDefault = (fn: (event: Event) => void) => {
	return (event: Event) => {
		event.preventDefault();
		fn(event);
	};
};

export const stopPropagation = (fn: (event: Event) => void) => {
	return (event: Event) => {
		event.stopPropagation();
		fn(event);
	};
};
