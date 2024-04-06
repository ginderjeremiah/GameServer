interface IAttribute {
	attributeId: AttributeType;
	attributeName: string;
	attributeDesc: string;
}

interface IBattlerAttribute {
	attributeId: AttributeType;
	amount: number;
	isCoreAttribute: boolean;
}

interface IAttributeUpdate {
	attributeId: number;
	amount: number;
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