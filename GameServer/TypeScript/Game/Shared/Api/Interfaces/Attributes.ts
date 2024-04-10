interface IAttribute {
	attributeId: AttributeType;
	attributeName: string;
	attributeDesc: string;
}

interface IAttributeDistribution {
	attributeId: AttributeType;
	baseAmount: number;
	amountPerLevel: number;
}

interface IAttributeMultiplier {
	attributeId: AttributeType;
	multiplier: number;
}

interface IAttributeUpdate {
	attributeId: number;
	amount: number;
}

interface IBattlerAttribute {
	attributeId: AttributeType;
	amount: number;
}