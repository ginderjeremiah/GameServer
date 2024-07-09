import { EAttribute } from "../Types"

export interface IAttribute {
	id: EAttribute;
	name: string;
	description: string;
}

export interface IBattlerAttribute {
	attributeId: EAttribute;
	amount: number;
}

export interface IAttributeUpdate {
	attributeId: number;
	amount: number;
}

export interface IAttributeDistribution {
	attributeId: EAttribute;
	baseAmount: number;
	amountPerLevel: number;
}

export interface IAttributeMultiplier {
	attributeId: EAttribute;
	multiplier: number;
}