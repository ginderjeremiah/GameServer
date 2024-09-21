import { IBattlerAttribute, IPlayerData } from "$lib/api";
import { writableEx } from "$lib/common";

export const player = writableEx<IPlayerData>();
export const equipmentStats = writableEx<IBattlerAttribute[]>();