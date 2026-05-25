export const preventDefault = (fn: (event: Event) => void) => {
	return (event: Event) => {
		event.preventDefault();
		fn(event);
	};
};
