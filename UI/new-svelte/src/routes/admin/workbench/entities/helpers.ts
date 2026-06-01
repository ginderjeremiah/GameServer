import type { SelectOption } from './types';

/** First option not already chosen (falls back to the first option when all are taken). */
export const firstFree = (taken: number[], options: SelectOption[]): number => {
	const free = options.find((option) => !taken.includes(option.value));
	return (free ?? options[0]).value;
};
