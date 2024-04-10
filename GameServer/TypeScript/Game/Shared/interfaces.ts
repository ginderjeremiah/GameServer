interface Dict<Type> {
    [key: string]: Type;
}

interface Listable<Type extends Listable<Type>> {
    lNode: ListNode<Type>;
}

function IsEnemyInstance(instance: IEnemyInstance | any): instance is IEnemyInstance {
    return instance.seed !== undefined;
}

interface SlotType {
    slotTypeId: number;
    slotTypeName: string;
}

interface SelOptions {
    allowBlanks?: boolean;
    options: {id: number, name: string}[];
}

interface BattleResult {
    victory: boolean;
    enemyInstance: IEnemyInstance;
    timeElapsed: number;
}