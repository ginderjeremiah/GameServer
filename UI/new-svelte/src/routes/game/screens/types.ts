import {
	Fight,
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
	Inventory,
	Attributes,
	AttributeBreakdown,
	Challenges,
	Statistics,
	Options,
	PlaceholderScreen
};

export type GameScreen = keyof typeof screenMap;
