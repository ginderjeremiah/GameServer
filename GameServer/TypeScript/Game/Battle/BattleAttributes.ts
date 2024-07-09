import { EAttribute } from "../Shared/Api/Enums";
import { IBattlerAttribute } from "../Shared/Api/Types";
import { normalizeText } from "../Shared/GlobalFunctions";

export class BattleAttributes {
    static #attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;
    attributes: number[];
    //battlerAttributes: IBattlerAttribute[];

    constructor(attList: IBattlerAttribute[], calcDerivedStats: boolean = true) {
        //this.battlerAttributes = attList;
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

    // resetAttributes(calcDerivedStats: boolean = true) {
    //     this.attributes.fill(0, 0, BattleAttributes.#attributesMaxId);
    //     this.battlerAttributes.forEach(att => this.attributes[att.attributeId] += att.amount);
    //     if (calcDerivedStats) {
    //         this.#calculateDerivedStats()
    //     }
    // }

    // addAttributes(attList: IBattlerAttribute[], calcDerivedStats: boolean = true) {
    //     this.battlerAttributes.push(...attList);
    //     this.resetAttributes(calcDerivedStats);
    // }

    getAttributeMap(includeZeroes: boolean = false) {
        return this.attributes
            .map((att, i) => ({name: normalizeText(EAttribute[i]), value: att}))
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