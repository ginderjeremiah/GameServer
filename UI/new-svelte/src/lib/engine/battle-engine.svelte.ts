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
   Loading,
}

const { Idle, Active, Victorious, Defeated, Loading } = BattleStage

const battleStageChangedHook = createHook<[BattleStage]>();
const notifyBattleStageChanged = battleStageChangedHook.notify;
export const onBattleStageChanged = battleStageChangedHook.onNotified;

let battleStage = $state(Idle);
let battlePlayer = $state(newBattler(player.data, inventory.equipmentStats));
let battleEnemy = $state<Battler>();
let battleTimeElapsed = $state(0);
let battleLoadingTime = $state(0);

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

export const setLoadingState = (loadingTime: number) => {
   battleLoadingTime = loadingTime;
   setBattleStage(Loading);
   const { promise, resolve } = Promise.withResolvers<void>();
   onRenderUpdate((delta, unhook) => {
      battleLoadingTime -= delta;
      if (battleLoadingTime <= 0) {
         resolve();
         unhook();
      }
   }, false);
   return promise;
}

export const pauseBattle = () => {
   setBattleStage(Idle);
}

export const resumeBattle = () => {
   if (!battlePlayer.isDead && !battleEnemy?.isDead) {
      setBattleStage(Active);
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

const setBattleStage = (stage: BattleStage) => {
   battleStage = stage;
   notifyBattleStageChanged(stage);
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
         setBattleStage(Victorious);
      } else if (battlePlayer.isDead) {
         setBattleStage(Defeated);
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