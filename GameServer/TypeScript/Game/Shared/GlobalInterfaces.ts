import { ListNode } from "./ListNode";
import { IEnemyInstance } from "./Api/Types";

export interface Dict<Type> {
    [key: string]: Type;
}

export interface Listable<Type extends Listable<Type>> {
    lNode: ListNode<Type>;
}

export function IsEnemyInstance(instance: IEnemyInstance | any): instance is IEnemyInstance {
    return instance.seed !== undefined;
}

export interface SlotType {
    slotTypeId: number;
    slotTypeName: string;
}

export interface SelOptions {
    allowBlanks?: boolean;
    options: {id: number, name: string}[];
}

export interface BattleResult {
    victory: boolean;
    enemyInstance: IEnemyInstance;
    timeElapsed: number;
}