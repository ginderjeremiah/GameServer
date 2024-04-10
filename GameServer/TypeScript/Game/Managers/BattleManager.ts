class BattleManager {
    #player: Player;
    #enemy: Enemy;
    #enemyInstance: IEnemyInstance
    #battleActive: boolean;
    #tickSize = 6; //number of ms per logic tick
    #msStore = 0; //number of ms to be allocated to ticks
    #timeElapsed = 0; //number of ms simulated during battle

    constructor(playerData: IPlayerData, enemyInstance: IEnemyInstance) {
        const enemyData = DataManager.enemies;
        this.#battleActive = true;
        this.#player = new Player(playerData);
        this.#enemy = new Enemy(enemyInstance, enemyData[enemyInstance.enemyId]);
        this.#enemyInstance = enemyInstance;
    }

    update(timeDelta: number): BattleResult | void {
        if (this.#battleActive) {
            this.#msStore += timeDelta;
            while (this.#battleActive && this.#msStore > this.#tickSize) {
                this.#msStore -= this.#tickSize;
                this.#timeElapsed += this.#tickSize;
                this.computeTick();
                if (this.#enemy.isDead || this.#player.isDead) {
                    this.#battleActive = false;
                    return {
                        victory: this.#enemy.isDead,
                        enemyInstance: this.#enemyInstance,
                        timeElapsed: this.#timeElapsed
                    }
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

    stop() {
        this.#battleActive = false;
    }

    reset(playerData: IPlayerData, enemyInstance: IEnemyInstance) {
        const enemyData = DataManager.enemies;
        this.#msStore = 0;
        this.#timeElapsed = 0;
        this.#player.reset(playerData);
        this.#enemy = new Enemy(enemyInstance, enemyData[enemyInstance.enemyId]);
        this.#enemyInstance = enemyInstance;
        this.#battleActive = true;
    }

    getPlayerAttributes() {
        return this.#player.attributes;
    }

    getOpponent(battler: Battler) {
        return battler === this.#player ? this.#enemy : this.#player;
    }
}