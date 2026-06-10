import {
	Fight,
	CardGame,
	Inventory,
	Skills,
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
	Skills,
	Attributes,
	AttributeBreakdown,
	Challenges,
	Statistics,
	Options,
	PlaceholderScreen
};

export type GameScreen = keyof typeof screenMap;
