export type Comparator<Type> = (currData: Type, objData: Type) => boolean;

export type Action<T extends any[] = []> = (...args: T) => void;

export type Converter<Type, Type2> = (data: Type) => Type2;

export type CheckFunc<Type> = (data: Type) => boolean;

export type ProgressionFunc = (percentComplete: number) => number

export type SlotVariant = "equipped" | "inventory";

export type Screens = "Fight" | "Attributes" | "Stats" | "Help" | "Options" | "Quit" | "Inventory" | "cardGame"