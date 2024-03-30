/// <reference path="Battler.ts"/>
class Enemy extends Battler {
    name: string;
    level: number;
    drops: ItemDrop[];
    //droppedItems: InventoryItem[];
    //victory: boolean;
    enemyInstance: EnemyInstance

    skillsContainer = document.getElementById("enemySkillsContainer") as HTMLDivElement;
    healthBar = document.getElementById("enemyHealth") as HTMLMeterElement;
    healthLabel = document.getElementById("enemyHealthLabel") as HTMLSpanElement;
    lvlLabel = document.getElementById("enemyLvl") as HTMLSpanElement;
    nameLabel = document.getElementById("enemyName") as HTMLSpanElement
    label = "enemy"

    constructor(enemyInstance: EnemyInstance, enemyData: EnemyData) {
        super(new BattleAttributes(enemyInstance.attributes), enemyData.selectedSkills);
        this.enemyInstance = enemyInstance;
        this.name = enemyData.enemyName;
        this.drops = enemyData.enemyDrops;
        this.level = enemyInstance.enemyLevel;
        //this.droppedItems = enemyInstance.droppedItems;
        //this.victory = enemyInstance.victory;
        this.nameLabel.textContent = this.name;
        this.initSkillsDisplay();
        this.updateSkillsDisplay();
        this.updateHealthDisplay();
        this.updateLvlDisplay();
    }
}
