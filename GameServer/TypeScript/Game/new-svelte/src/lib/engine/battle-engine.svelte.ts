import { Battler, newBattler } from "$lib/battle";
import { staticData, player, inventory } from "$stores";
import { ELogSetting, IEnemyInstance } from "$lib/api";
import { logMessage } from "./log";
import { formatNum, createHook } from "$lib/common";
import { onLogicalUpdate } from "./game-engine.svelte";
import { onRenderUpdate } from "./render-engine.svelte";

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
let battleStage = $state(Idle);
let battlePlayer = $state(newBattler(player.data, inventory.equipmentStats));
let battleEnemy = $state<Battler>();
let battleTimeElapsed = $state(0);

export const battleState = {
   get stage() {
      return battleStage;
   },
   get player() {
      return battlePlayer;
   },
   get enemy() {
      return battleEnemy;
   },
   get battleTimeElapsed() {
      return battleTimeElapsed;
   }
}

let initialized = false;

export const initBattleEngine = () => {
   if (!initialized) {
      initialized = true;
      onLogicalUpdate(logicalUpdate);
      onRenderUpdate(renderUpdate);
   }
}

export const pauseBattle = () => {
   battleStage = Idle;
}

export const resumeBattle = () => {
   if (!battlePlayer.isDead && !battleEnemy?.isDead) {
      battleStage = Active;
      notifyBattleStart();
   }
}

export const resetBattle = (enemyInstance: IEnemyInstance) => {
   const enemyData = staticData.enemies;
   battleTimeElapsed = 0;
   battlePlayer.reset(player.data, inventory.equipmentStats);
   battleEnemy = newBattler({ ...enemyInstance, ...enemyData[enemyInstance.id] });
   resumeBattle();
}

export const getPlayerAttributes = () => {
   return battlePlayer.attributes;
}

export const getOpponent = (battler: Battler) => {
   return battler.id === battlePlayer.id ? battleEnemy : battlePlayer;
}

const logicalUpdate = (timeDelta: number) => {
   const enemy = battleEnemy;
   if (enemy && battleStage === Active) {
      const playerSkillsFired = battlePlayer.advanceCooldowns(timeDelta);
      playerSkillsFired.forEach(skill => {
         const dmg = skill.calculateDamage();
         let finalDmg = enemy.takeDamage(dmg);
         logMessage(ELogSetting.Damage, `You used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
      });
      if (!enemy.isDead) {
         const enemySkillsFired = enemy.advanceCooldowns(timeDelta);
         enemySkillsFired.forEach(skill => {
            const dmg = skill.calculateDamage();
            let finalDmg = battlePlayer.takeDamage(dmg);
            logMessage(ELogSetting.Damage, `${enemy.name} used ${skill.name} and dealt ${formatNum(finalDmg)} damage!`);
         });
      }
      if (enemy.isDead) {
         battleStage = Victorious;
      } else if (battlePlayer.isDead) {
         battleStage = Defeated;
      }
   }
   battleTimeElapsed += timeDelta;
}

const renderUpdate = (renderDelta: number) => {
   const enemy = battleEnemy;
   if (enemy && battleStage === Active) {
      battlePlayer.updateRenderCooldowns(renderDelta);
      battleEnemy?.updateRenderCooldowns(renderDelta);
   }
}