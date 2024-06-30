interface IAttribute {
	id: EAttribute;
	name: string;
	description: string;
}

interface IAttributeDistribution {
	attributeId: EAttribute;
	baseAmount: number;
	amountPerLevel: number;
}

interface IAttributeMultiplier {
	attributeId: EAttribute;
	multiplier: number;
}

interface IAttributeUpdate {
	attributeId: number;
	amount: number;
}

interface IBattlerAttribute {
	attributeId: EAttribute;
	amount: number;
}