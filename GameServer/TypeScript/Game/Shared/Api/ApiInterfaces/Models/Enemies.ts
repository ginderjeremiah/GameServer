interface IEnemy {
	enemyDrops: IItemDrop[];
	attributeDistribution: IAttributeDistribution[];
	enemyName: string;
	enemyId: number;
	selectedSkills: number[];
}