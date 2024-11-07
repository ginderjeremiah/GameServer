import { IBattlerAttribute, EAttribute } from "$lib/api";
import { normalizeText } from "$lib/common";

type AttributeMapData = {
    name: string,
    value: number
}

export interface BattleAttributes {
    attributes: number[];
    getValue(attId: EAttribute): number;
    getAttributeMap(includeZeroes?: boolean): AttributeMapData[];
}

const attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;

const calculateDerivedStats = (attributes: number[]) => {
    attributes[EAttribute.MaxHealth] += 50 + (20 * attributes[EAttribute.Endurance]) + (5 * attributes[EAttribute.Strength]);
    attributes[EAttribute.Defense] += 2 + attributes[EAttribute.Endurance] + (0.5 * attributes[EAttribute.Agility]);
    attributes[EAttribute.CooldownRecovery] += (0.4 * attributes[EAttribute.Agility]) + (0.1 * attributes[EAttribute.Dexterity]);
    attributes[EAttribute.DropBonus] += Math.log10(attributes[EAttribute.Luck]);
    // attributes[EAttribute.CriticalChance] = 0;
    // attributes[EAttribute.CriticalDamage] = 0;
    // attributes[EAttribute.DodgeChance] = 0;
    // attributes[EAttribute.BlockChance] = 0;
    // attributes[EAttribute.BlockReduction] = 0;
}

export const newBattleAttributes = (attList: IBattlerAttribute[], calcDerivedStats: boolean = true) => {
    const attributes = new Array<number>(attributesMaxId);
    attributes.fill(0, 0, attributesMaxId);
    attList.forEach(att => attributes[att.attributeId] += att.amount);
    if (calcDerivedStats) {
        calculateDerivedStats(attributes);
    }

    const getValue = (attId: EAttribute) => {
        return attributes[attId];
    }

    const getAttributeMap = (includeZeroes: boolean = false) => {
        return attributes
            .map((att, i) => ({ name: normalizeText(EAttribute[i]), value: att }))
            .filter(att => att.value != 0 || includeZeroes);
    }

    return {
        attributes,
        getValue,
        getAttributeMap
    }
}