import { IPlayerData, IBattlerAttribute, IEnemyInstance } from "../Shared/Api/Types";
import { BattleResult } from "../Shared/GlobalInterfaces";
import { Player } from "../Battle/Player";
import { Enemy } from "../Battle/Enemy";
import { DataManager } from "./DataManager";
import { LogManager } from "./LogManager";
import { ScreenManager } from "./ScreenManager";
import { Battler } from "../Battle/Battler";
import { formatNum } from "../Shared/GlobalFunctions";

export class BattleManager {
    #player: Player;
    #enemy: Enemy;
    #battleActive: boolean;
    #tickSize = 6; //number of ms per logic tick
    #msStore = 0; //number of ms to be allocated to ticks
    #timeElapsed = 0; //number of ms simulated during battle

    constructor(playerData: IPlayerData, equipmentAttributes: IBattlerAttribute[], enemyInstance: IEnemyInstance) {
        const enemyData = DataManager.enemies;
        this.#battleActive = true;
        this.#player = new Player(playerData, equipmentAttributes);
        this.#enemy = new Enemy(enemyInstance, enemyData[enemyInstance.id]);
    }

    update(timeDelta: number): BattleResult | void {
        if (this.#battleActive) {
            this.#msStore += timeDelta;
            const tickComputed = this.#msStore >= this.#tickSize;
            while (this.#msStore >= this.#tickSize) {
                this.#msStore -= this.#tickSize;
                this.#timeElapsed += this.#tickSize;
                this.computeTick();
                if (this.#enemy.isDead || this.#player.isDead) {
                    return this.endBattle();
                }
            }
            if (tickComputed && ScreenManager.currentScreen === "Fight") {
                this.#player.updateCombatDisplays();
                this.#enemy.updateCombatDisplays();
            }
        }
    }

    computeTick() {
        const playerSkillsFired = this.#player.advanceCooldown(this.#tickSize);
        playerSkillsFired.forEach(skill => {
            const dmg = skill.calculateDamage();
            let finalDmg = this.#enemy.takeDamage(dmg);
            LogManager.logMessage("You used " + skill.name + " and dealt " + formatNum(finalDmg) + " damage!", "Damage");
        });
        if (!this.#enemy.isDead) {
            const enemySkillsFired = this.#enemy.advanceCooldown(this.#tickSize);
            enemySkillsFired.forEach(skill => {
                const dmg = skill.calculateDamage();
                let finalDmg = this.#player.takeDamage(dmg);
                LogManager.logMessage(this.#enemy.name + " used " + skill.name + " and dealt " + formatNum(finalDmg) + " damage!", "Damage");
            });
        }
    }

    endBattle() {
        this.#battleActive = false;
        this.#player.updateCombatDisplays();
        this.#enemy.updateCombatDisplays();
        return {
            victory: this.#enemy.isDead,
            enemyInstance: this.#enemy.enemyInstance,
            timeElapsed: this.#timeElapsed
        }
    }

    pause() {
        this.#battleActive = false;
    }

    resume() {
        this.#battleActive = !this.#enemy?.isDead && !this.#player.isDead;
    }

    reset(playerData: IPlayerData, equipmentAttributes: IBattlerAttribute[], enemyInstance: IEnemyInstance) {
        const enemyData = DataManager.enemies;
        this.#msStore = 0;
        this.#timeElapsed = 0;
        this.#player.reset(playerData, equipmentAttributes);
        this.#enemy = new Enemy(enemyInstance, enemyData[enemyInstance.id]);
        this.#battleActive = true;
    }

    getPlayerAttributes() {
        return this.#player.attributes;
    }

    getOpponent(battler: Battler) {
        return battler === this.#player ? this.#enemy : this.#player;
    }
}