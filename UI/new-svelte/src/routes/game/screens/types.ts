import { Fight, Inventory, Attributes, Challenges, Stats, Help, Options, CardGame, PlaceholderScreen } from './';

export const screenMap = {
	Fight,
	Inventory,
	Attributes,
	Challenges,
	Stats,
	Help,
	Options,
	CardGame,
	PlaceholderScreen
};

export type GameScreen = keyof typeof screenMap;
