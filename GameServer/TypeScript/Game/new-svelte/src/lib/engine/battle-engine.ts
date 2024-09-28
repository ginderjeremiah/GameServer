import { Battler } from "$lib/battle";
import { enemies, equipmentStats, player } from "$stores";
import { ELogSetting, IEnemyInstance } from "$lib/api";
import { logMessage } from "./log";
import { formatNum, writableEx, createHook } from "$lib/common";
import { onLogicalUpdate } from "./game-engine";

export enum BattleStage {
   Idle,
   Active,
   Victorious,
   Defeated,
}

const { Idle, Active, Victorious, Defeated } = BattleStage

const battleStartHook = createHook();
const notifyBattleStart = battleStartHook.notify;

export const onBattleStart = battleStartHook.onNotified;
export const battleStage = writableEx(Idle);
export const battlePlayer = writableEx(new Battler(player.value, equipmentStats.value));
export const battleEnemy = writableEx<Battler | undefined>();
export let battleTimeElapsed = 0;

let initialized = false;

export const initBattleEngine = () => {
   if (!initialized) {
      initialized = true;
      onLogicalUpdate(logicalUpdate);
   }
}

export const pauseBattle = () => {
   battleStage.set(Idle);
}

export const resumeBattle = () => {
   if (!battlePlayer.value.isDead && !battleEnemy.value?.isDead) {
      battleStage.set(Active);
      notifyBattleStart();
   }
}

export const resetBattle = (enemyInstance: IEnemyInstance) => {
   const enemyData = enemies.value;
   battleTimeElapsed = 0;
   battlePlayer.value.reset(player.value, equipmentStats.value);
   battlePlayer.update(p => p);
   battleEnemy.set(new Battler({ ...enemyInstance, ...enemyData[enemyInstance.id] }));
   resumeBattle();
}

export const getPlayerAttributes = () => {
   return battlePlayer.value.attributes;
}

export const getOpponent = (battler: Battler) => {
   return battler === battlePlayer.value ? battleEnemy.value : battlePlayer.value;
}

const logicalUpdate = (timeDelta: number) => {
   const enemy = battleEnemy.value;
   if (enemy && battleStage.value === Active) {
      const playerSkillsFired = battlePlayer.value.advanceCooldown(timeDelta);
      playerSkillsFired.forEach(skill => {
         const dmg = skill.calculateDamage();
         let finalDmg = enemy.takeDamage(dmg);
         logMessage(ELogSetting.Damage, `You used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
      });
      if (!enemy.isDead) {
         const enemySkillsFired = enemy.advanceCooldown(timeDelta);
         enemySkillsFired.forEach(skill => {
            const dmg = skill.calculateDamage();
            let finalDmg = battlePlayer.value.takeDamage(dmg);
            logMessage(ELogSetting.Damage, `${enemy.name} used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
         });
      }
      if (enemy.isDead) {
         battleStage.set(Victorious);
      } else if (battlePlayer.value.isDead) {
         battleStage.set(Defeated);
      }
   }
   battlePlayer.refresh();
   battleEnemy.refresh();
   battleTimeElapsed += timeDelta;
}