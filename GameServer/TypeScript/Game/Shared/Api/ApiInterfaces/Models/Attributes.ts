interface IAttribute {
	attributeId: number;
	attributeName: string;
	attributeDesc: string;
}

interface IAttributeDistribution {
	enemyId: number;
	attributeId: number;
	baseAmount: number;
	amountPerLevel: number;
}

interface IAttributeMultiplier {
	attributeId: number;
	multiplier: number;
}