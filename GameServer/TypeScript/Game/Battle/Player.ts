/// <reference path="Battler.ts"/>
class Player extends Battler {

    name: string;
    level: number;
    currentExp: number;
    maxSkills: number = 4;
    statPointsGained: number;
    statPointsUsed: number;
    

    skillsContainer = document.getElementById("playerSkillsContainer") as HTMLDivElement;
    healthBar = document.getElementById("playerHealth") as HTMLMeterElement;
    healthLabel = document.getElementById("playerHealthLabel") as HTMLSpanElement;
    lvlLabel = document.getElementById("playerLvl") as HTMLSpanElement;
    nameLabel = document.getElementById("playerName") as HTMLSpanElement;
    label = "player"

    constructor(skillDatas: SkillData[]) {
        const playerData = GameManager.getPlayerData();
        super(GameManager.getPlayerData, skillDatas);
        this.name = playerData.playerName;
        this.level = playerData.level;
        this.currentExp = playerData.exp;
        this.statPointsGained = playerData.statPointsGained;
        this.statPointsUsed = playerData.statPointsUsed;
        this.updateLvlDisplay();
        this.initSkillsDisplay();
        this.updateSkillsDisplay();
        this.updateHealthDisplay();
        this.updateLvlDisplay();
        this.nameLabel.textContent = this.name;
    }

    grantExp(expReward: number): void {
        LogManager.logMessage("Earned " + formatNum(expReward) + " exp.", "Exp")
        this.currentExp += expReward;
        if (this.currentExp >= this.level * 100) {
            this.levelUp();
        }
    }

    levelUp(): void {
        this.currentExp -= this.level * 100;
        this.level++;
        this.statPointsGained += 6;
        this.derivedStats = this.calculateDerivativeStats();
        this.currentHealth = this.derivedStats.maxHealth;
        this.updateHealthDisplay();
        this.updateLvlDisplay();
        LogManager.logMessage("Congratulations, you leveled up!", "LevelUp");
        LogManager.logMessage("You are now level " + this.level + ".", "LevelUp");
    }

    updateExpDisplay(): void {
        //TODO create meter for displaying exp?
    }
}
