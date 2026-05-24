import { Fight, Inventory, Attributes, Challenges, Stats, Help, Options, CardGame, Quit } from "./";

export const screenMap = {
  Fight,
  Inventory,
  Attributes,
  Challenges,
  Stats,
  Help,
  Options,
  CardGame,
  Quit
}

export type GameScreen = keyof typeof screenMap;
