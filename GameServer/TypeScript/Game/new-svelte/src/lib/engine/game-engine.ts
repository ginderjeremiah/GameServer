import { onDestroy } from "svelte";
import { BattleEngine, BattleState, battleState } from "./battle-engine";
import { triggerHook } from "./hooks";
import { ELogSetting, IEnemyInstance, IPlayerData } from "$lib/api";
import { get } from "svelte/store";
import { enemies, player } from "$stores";
import { Inventory } from "$lib/inventory";
import { delay, formatNum } from "$lib/common";
import { ApiSocket, IApiSocketResponse } from "$lib/api/api-socket";
import { logMessage } from "./log";
import { Battler } from "$lib/battle";

let battleEngine: BattleEngine;
const inventory = new Inventory();
const apiSocket = new ApiSocket();
let currentEnemy: IEnemyInstance | undefined;
let lastTime: number;
let newEnemyPromise: Promise<IApiSocketResponse<"NewEnemy">> | undefined;

const update = (ts: DOMHighResTimeStamp) => {
   const timeDelta = ts - lastTime;
   lastTime = ts;
   triggerHook('update', Math.min(timeDelta, 1000));
}

const gameLoop = (ts: DOMHighResTimeStamp) => {
   update(ts);
   window.requestAnimationFrame(gameLoop);
}

const levelUp = (playerData: IPlayerData) => {
   playerData.exp -= playerData.level * 100;
   playerData.level++;
   playerData.statPointsGained += 6;
   logMessage(ELogSetting.LevelUp, "Congratulations, you leveled up!");
   logMessage(ELogSetting.LevelUp, `You are now level ${playerData.level}.`);
}

const grantExp = (exp: number) => {
   logMessage(ELogSetting.Exp, `Earned ${formatNum(exp)} exp.`);
   const playerData = player.value
   playerData.exp += exp;
   if (playerData.exp >= playerData.level * 100) {
      levelUp(playerData);
   }
}

const getNewEnemy = async () => {
   if (!newEnemyPromise) {
      newEnemyPromise = apiSocket.sendSocketCommand("NewEnemy", { newZoneId: player.value.currentZone });
   }

   const result = await newEnemyPromise;
   newEnemyPromise = undefined;
   if (result.data?.enemyInstance) {
      return result.data.enemyInstance;
   } else {
      if (result.data.cooldown) {
         await delay(result.data.cooldown);
      }
      return await getNewEnemy();
   }
}

const watchBattleState = () => {
   const unsub = battleState.subscribe(async (state) => {
      if (state === BattleState.Victorious && currentEnemy) {
         const defeatResponse = await apiSocket.sendSocketCommand("DefeatEnemy", currentEnemy);
         if (!defeatResponse.error && defeatResponse.data.rewards) {
            const rewards = defeatResponse.data.rewards
            grantExp(rewards.expReward);
            inventory.addItems(rewards.drops);
            logMessage(ELogSetting.EnemyDefeated, enemies.value[currentEnemy.id].name + " was defeated!");
         } else {
            logMessage(ELogSetting.Debug, "There was an error defeating the enemy: " + defeatResponse.error);
         }
         await delay(defeatResponse.data.cooldown);
      } else if (state === BattleState.Defeated) {
         logMessage(ELogSetting.EnemyDefeated, "You've been defeated!");
      }

      if (state !== BattleState.Active) {
         currentEnemy = await getNewEnemy();
         battleEngine.reset(currentEnemy);
      }
   });

   onDestroy(unsub);
}

export const getPlayer = () => {
   return battleEngine.player;
}

export const getEnemy = () => {
   return battleEngine.enemy;
}

export const getOpponent = (battler: Battler) => {
   return battleEngine.getOpponent(battler);
};

export const startGame = async () => {
   battleEngine = new BattleEngine();
   watchBattleState();
   window.requestAnimationFrame(gameLoop);
};