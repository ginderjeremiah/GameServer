import { IBattlerAttribute, EAttribute } from "$lib/api";
import { normalizeText } from "$lib/common";

export class BattleAttributes {
    static #attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;
    attributes: number[];

    constructor(attList: IBattlerAttribute[], calcDerivedStats: boolean = true) {
        this.attributes = new Array<number>(BattleAttributes.#attributesMaxId);
        this.attributes.fill(0, 0, BattleAttributes.#attributesMaxId);
        attList.forEach(att => this.attributes[att.attributeId] += att.amount);
        if (calcDerivedStats) {
            this.#calculateDerivedStats()
        }
    }

    getValue(attId: number) {
        return this.attributes[attId];
    }

    getAttributeMap(includeZeroes: boolean = false) {
        return this.attributes
            .map((att, i) => ({ name: normalizeText(EAttribute[i]), value: att }))
            .filter(att => att.value != 0 || includeZeroes);
    }

    #calculateDerivedStats() {
        this.attributes[EAttribute.MaxHealth] += 50 + (20 * this.attributes[EAttribute.Endurance]) + (5 * this.attributes[EAttribute.Strength]);
        this.attributes[EAttribute.Defense] += 2 + this.attributes[EAttribute.Endurance] + (0.5 * this.attributes[EAttribute.Agility]);
        this.attributes[EAttribute.CooldownRecovery] += (0.4 * this.attributes[EAttribute.Agility]) + (0.1 * this.attributes[EAttribute.Dexterity]);
        this.attributes[EAttribute.DropBonus] += Math.log10(this.attributes[EAttribute.Luck]);
        // this.attributes[EAttribute.CriticalChance] = 0;
        // this.attributes[EAttribute.CriticalDamage] = 0;
        // this.attributes[EAttribute.DodgeChance] = 0;
        // this.attributes[EAttribute.BlockChance] = 0;
        // this.attributes[EAttribute.BlockReduction] = 0;
    }
}