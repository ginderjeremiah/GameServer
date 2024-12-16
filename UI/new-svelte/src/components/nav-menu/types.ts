import { Action } from '$lib/common';
import { MouseEventHandler } from 'svelte/elements';

export interface INavMenuItem {
	text: string;
	onClick: Action<[Parameters<MouseEventHandler<HTMLButtonElement>>[0], INavMenuItem]>;
}
