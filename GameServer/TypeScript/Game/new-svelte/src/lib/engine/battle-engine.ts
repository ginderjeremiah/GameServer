import { get, writable } from "svelte/store";
import { registerHook, triggerHook } from "./hooks";
import { Battler } from "$lib/battle";
import { enemies, equipmentStats, player } from "$stores";
import { ELogSetting, IEnemyInstance } from "$lib/api";
import { logMessage } from "./log";
import { formatNum, WritableEx, writableEx } from "$lib/common";

export enum BattleState {
   Idle,
   Active,
   Victorious,
   Defeated,
}

const Idle = BattleState.Idle;
const Active = BattleState.Active;
const Victorious = BattleState.Victorious;
const Defeated = BattleState.Defeated;

export const battleState = writableEx(Idle);

export class BattleEngine {
   private timeStore = 0;
   private tickSize = 6;

   player: WritableEx<Battler>;
   enemy: WritableEx<Battler | undefined>;
   timeElapsed = 0;

   constructor() {
      this.player = writableEx(new Battler(player.value, equipmentStats.value));
      this.enemy = writableEx();
      registerHook('update', this.update.bind(this))
   }

   update(timeDelta: number) {
      const enemy = this.enemy.value;
      if (enemy && battleState.value === Active) {
         this.timeStore += timeDelta;
         while (this.timeStore >= this.tickSize) {
            this.timeStore -= this.tickSize;
            this.timeElapsed += this.tickSize;
            const playerSkillsFired = this.player.value.advanceCooldown(this.tickSize);
            playerSkillsFired.forEach(skill => {
               const dmg = skill.calculateDamage();
               let finalDmg = enemy.takeDamage(dmg);
               logMessage(ELogSetting.Damage, `You used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
            });
            if (!enemy.isDead) {
               const enemySkillsFired = enemy.advanceCooldown(this.tickSize);
               enemySkillsFired.forEach(skill => {
                  const dmg = skill.calculateDamage();
                  let finalDmg = this.player.value.takeDamage(dmg);
                  logMessage(ELogSetting.Damage, `${enemy.name} used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
               });
            }
            if (enemy.isDead) {
               battleState.set(Victorious);
            } else if (this.player.value.isDead) {
               battleState.set(Defeated);
            }
         }
         this.player.refresh();
         this.enemy.refresh();
      }
   }

   pause() {
      battleState.set(Idle);
   }

   resume() {
      if (!this.player.value.isDead && !this.enemy.value?.isDead) {
         battleState.set(Active);
         triggerHook('battle-start');
      }
   }

   reset(enemyInstance: IEnemyInstance) {
      const enemyData = enemies.value;
      this.timeStore = 0;
      this.timeElapsed = 0;
      this.player.value.reset(player.value, equipmentStats.value);
      this.player.update(p => p);
      this.enemy.set(new Battler({ ...enemyInstance, ...enemyData[enemyInstance.id] }));
      this.resume();
   }

   getPlayerAttributes() {
      return this.player.value.attributes;
   }

   getOpponent(battler: Battler) {
      return battler === this.player.value ? this.enemy.value : this.player.value;
   }
}