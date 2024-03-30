class BattleManager {
    #player! : Player;
    #enemy! : Enemy;
    #battleActive: boolean;
    #battleReady: boolean;
    static #battleManager: BattleManager
    #tickSize = 6; //number of ms per logic tick
    #msStore = 0; //number of ms to be allocated to ticks

    constructor() {
        if(BattleManager.#battleManager) {
            throw new ReferenceError("BattleManager already exists.")
        } else {
            BattleManager.#battleManager = this;
            this.#battleActive = false;
            this.#battleReady = false;
            this.#player = new Player();
            this.newEnemy();
        }
    }

    update(timeDelta: number) {
        this.#battleActive = this.#battleReady ? !(this.#battleReady = false) : this.#battleActive;
        if (this.#battleActive) {
            this.#msStore += timeDelta;
            while (this.#battleActive && this.#msStore > this.#tickSize) {
                this.#msStore -= this.#tickSize;
                this.computeTick();
                if (this.#enemy.isDead) {
                    this.#battleActive = false;
                    DataManager.defeatEnemy(this.#enemy.enemyInstance).then(response => {
                        const responseData = response.data
                        if (response.status == 200 && responseData.rewards) {
                            LogManager.logMessage(this.#enemy.name + " was defeated!", "Enemy Defeated");
                            this.#player.grantExp(responseData.rewards.expReward);
                            GameManager.addItems(responseData.rewards.drops);
                        } else {
                            LogManager.logMessage("There was an error defeating the enemy.", "ERROR");
                        }
                        delay(responseData.cooldown).then(() => this.reset())
                    });
                }
                else if (this.#player.isDead) {
                    LogManager.logMessage("You've been defeated!", "Enemy Defeated");
                    this.reset();
                }
            }
        }
    }

    computeTick() {
        const playerSkillsFired = this.#player.advanceCooldown(this.#tickSize);
        playerSkillsFired.forEach((skill) => {
            const dmg = skill.calculateDamage();
            let finalDmg = this.#enemy.takeDamage(dmg);
            LogManager.logMessage("You used " + skill.skillName + " and dealt " + formatNum(finalDmg) + " damage!", "Damage");
        });
        if (!this.#enemy.isDead) {
            const enemySkillsFired = this.#enemy.advanceCooldown(this.#tickSize);
            enemySkillsFired.forEach((skill) => {
                const dmg = skill.calculateDamage();
                let finalDmg = this.#player.takeDamage(dmg);
                LogManager.logMessage(this.#enemy.name + " used " + skill.skillName + " and dealt " + formatNum(finalDmg) + " damage!", "Damage");
            });
        }
    }


    reset(zoneId?: number) {
        this.#battleActive = false;
        this.#battleReady = false;
        this.#enemy.clearSkillsDisplay();
        this.#player.reset(GameManager.getPlayerData());
        this.#msStore = 0;
        this.newEnemy(zoneId);
    }

    async newEnemy(zoneId?: number) {
        return DataManager.newEnemy(zoneId).then((results) => {
            this.#enemy = new Enemy(results.enemyInstance, results.enemyData);
            this.#battleReady = true;
        });
    }

    getPlayerAttributes() {
        return {statsVersion: this.#player.statsVersion, attributes: this.#player.attributes};
    }

    getOpponent(battler: Battler) {
        return battler === this.#player ? this.#enemy : this.#player;
    }

    incrementPlayerStatsVersion() {
        this.#player.statsVersion++;
    }
}