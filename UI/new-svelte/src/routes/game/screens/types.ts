import { Fight, Inventory, Attributes, Challenges, Stats, Help, Options, CardGame, Quit, PlaceholderScreen } from './';

export const screenMap = {
	Fight,
	Inventory,
	Attributes,
	Challenges,
	Stats,
	Help,
	Options,
	CardGame,
	Quit,
	PlaceholderScreen
};

export type GameScreen = keyof typeof screenMap;
