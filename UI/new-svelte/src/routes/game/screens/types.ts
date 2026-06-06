import {
	Fight,
	Inventory,
	Attributes,
	AttributeBreakdown,
	Challenges,
	Statistics,
	Help,
	Options,
	CardGame,
	PlaceholderScreen
} from './';

export const screenMap = {
	Fight,
	Inventory,
	Attributes,
	AttributeBreakdown,
	Challenges,
	Statistics,
	Help,
	Options,
	CardGame,
	PlaceholderScreen
};

export type GameScreen = keyof typeof screenMap;
