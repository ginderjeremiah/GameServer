/// <reference path="Battler.ts"/>
class Enemy extends Battler {
    drops: IItemDrop[];
    //droppedItems: InventoryItem[];
    //victory: boolean;
    enemyInstance: IEnemyInstance;

    constructor(enemyInstance: IEnemyInstance, enemyData: IEnemy) {
        super({...enemyInstance, ...enemyData}, "enemy");
        this.drops = enemyData.drops;
        this.enemyInstance = enemyInstance;
    }
}
