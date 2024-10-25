import { onDestroy } from "svelte";
import { BattleStage, battleState, initBattleEngine, resetBattle } from "./battle-engine.svelte";
import { ELogSetting, IEnemyInstance, IPlayerData } from "$lib/api";
import { staticData, player, inventory, addInventoryItems } from "$stores";
import { delay, formatNum, createHook, getEventCounter } from "$lib/common";
import { apiSocket, ApiSocket, IApiSocketResponse } from "$lib/api/api-socket";
import { logMessage } from "./log";
import { startRenderEngine } from "./render-engine.svelte";

export const tickSize = 40; //ms
let logicalTime = $state(0);
let logicalTickRate = $state(0);

export const logicalState = {
   get time() {
      return logicalTime;
   },
   get tickRate() {
      return logicalTickRate;
   }
}

const logicalUpdateHook = createHook<number>();
const notifyLogicalUpdate = logicalUpdateHook.notify;
export const onLogicalUpdate = logicalUpdateHook.onNotified;

let currentEnemy: IEnemyInstance | undefined;
let newEnemyPromise: Promise<IApiSocketResponse<"NewEnemy">> | undefined;
let timeBank = 0;
let lastTime: DOMHighResTimeStamp;
let countTick = getEventCounter(t => logicalTickRate = Math.round(t));

export const startGame = () => {
   if (!lastTime) {
      lastTime = performance.now();
      logicalTime = lastTime;
      initBattleEngine();
      watchBattleState();
      const handle = window.setInterval(logicLoop, 10);
      startRenderEngine();
      onDestroy(() => {
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
   player.data.exp += exp;
   if (player.data.exp >= player.data.level * 100) {
      levelUp(player.data);
   }
}

const getNewEnemy = async () => {
   if (!newEnemyPromise) {
      newEnemyPromise = apiSocket.sendSocketCommand("NewEnemy", { newZoneId: player.data.currentZone });
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
   $effect(() => {
      (async () => {
         if (battleState.stage === BattleStage.Victorious && currentEnemy) {
            const defeatResponse = await apiSocket.sendSocketCommand("DefeatEnemy", currentEnemy);
            if (!defeatResponse.error && defeatResponse.data.rewards) {
               const rewards = defeatResponse.data.rewards
               grantExp(rewards.expReward);
               addInventoryItems(rewards.drops);
               logMessage(ELogSetting.EnemyDefeated, staticData.enemies[currentEnemy.id].name + " was defeated!");
            } else {
               logMessage(ELogSetting.Debug, "There was an error defeating the enemy: " + defeatResponse.error);
            }
            if (defeatResponse.data.cooldown > 0) {
               await delay(defeatResponse.data.cooldown);
            }
         } else if (battleState.stage === BattleStage.Defeated) {
            logMessage(ELogSetting.EnemyDefeated, "You've been defeated!");
         }

         if (battleState.stage !== BattleStage.Active) {
            currentEnemy = await getNewEnemy();
            resetBattle(currentEnemy);
         }
      })();
   })
}