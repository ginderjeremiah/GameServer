interface IAttribute {
	id: AttributeType;
	name: string;
	description: string;
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