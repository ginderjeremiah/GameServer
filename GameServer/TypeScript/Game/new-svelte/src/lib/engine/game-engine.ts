import { onDestroy } from "svelte";
import { BattleStage, battleStage, initBattleEngine, resetBattle } from "./battle-engine";
import { ELogSetting, IEnemyInstance, IPlayerData } from "$lib/api";
import { enemies, player } from "$stores";
import { Inventory } from "$lib/inventory";
import { delay, formatNum, writableEx, createHook, getEventCounter } from "$lib/common";
import { ApiSocket, IApiSocketResponse } from "$lib/api/api-socket";
import { logMessage } from "./log";
import { startRenderEngine } from "./render-engine";

export const tickSize = 40; //ms
export let logicalTime: DOMHighResTimeStamp;
export let logicalTickRate = writableEx(0);

const logicalUpdateHook = createHook<number>();
const notifyLogicalUpdate = logicalUpdateHook.notify;
export const onLogicalUpdate = logicalUpdateHook.onNotified;

const inventory = new Inventory();
const apiSocket = new ApiSocket();
let currentEnemy: IEnemyInstance | undefined;
let newEnemyPromise: Promise<IApiSocketResponse<"NewEnemy">> | undefined;
let timeBank = 0;
let lastTime: DOMHighResTimeStamp;
let countTick = getEventCounter(t => logicalTickRate.set(Math.round(t)));

export const startGame = () => {
   if (!lastTime) {
      lastTime = performance.now();
      logicalTime = lastTime;
      initBattleEngine();
      const unwatch = watchBattleState();
      const handle = window.setInterval(logicLoop, 10);
      startRenderEngine();
      onDestroy(() => {
         unwatch();
         window.clearInterval(handle);
         lastTime = 0;
      })
   }
};

const logicLoop = () => {
   const now = performance.now()
   const ts = now - lastTime;
   lastTime = now;
   update(ts);
}

const update = (timeDelta: number) => {
   timeBank += timeDelta;
   while (timeBank >= tickSize) {
      countTick();
      timeBank -= tickSize;
      logicalTime += tickSize;
      notifyLogicalUpdate(tickSize);
   }
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
   return battleStage.subscribe(async (state) => {
      if (state === BattleStage.Victorious && currentEnemy) {
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
      } else if (state === BattleStage.Defeated) {
         logMessage(ELogSetting.EnemyDefeated, "You've been defeated!");
      }

      if (state !== BattleStage.Active) {
         currentEnemy = await getNewEnemy();
         resetBattle(currentEnemy);
      }
   });
}