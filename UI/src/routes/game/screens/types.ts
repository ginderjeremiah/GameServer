import {
	Fight,
	CardGame,
	Inventory,
	Attributes,
	AttributeBreakdown,
	Challenges,
	Statistics,
	Options,
	PlaceholderScreen
} from './';

export const screenMap = {
	Fight,
	CardGame,
	Inventory,
	Attributes,
	AttributeBreakdown,
	Challenges,
	Statistics,
	Options,
	PlaceholderScreen
};

export type GameScreen = keyof typeof screenMap;
