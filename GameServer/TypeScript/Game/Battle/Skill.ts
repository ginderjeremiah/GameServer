import { Tooltippable } from "../Abstract/Tooltippable";
import { ISkill, IAttributeMultiplier, EAttribute } from "../Shared/Api/Types";
import { Battler } from "./Battler";
import { GameManager } from "../Managers/GameManager";
import { DataManager } from "../Managers/DataManager";
import { formatNum } from "../Shared/GlobalFunctions";

export class Skill extends Tooltippable implements ISkill {
    id: number
    name: string;
    chargeTime: number;
    baseDamage: number;
    damageMultipliers: IAttributeMultiplier[];
    description: string;
    cooldownMS: number;
    iconPath: string;
    owner: Battler;
    ownerStatsVersion = -1;
    target?: Battler;

    constructor(data: ISkill, owner: Battler) {
        super();
        this.id = data.id
        this.name = data.name;
        this.baseDamage = data.baseDamage;
        this.damageMultipliers = data.damageMultipliers;
        this.description = data.description;
        this.cooldownMS = data.cooldownMS;
        this.chargeTime = 0;
        this.iconPath = data.iconPath;
        this.owner = owner;
    }

    updateTooltipData(tooltipTitle: HTMLHeadingElement, tooltipContent: HTMLDivElement, prevId: number): number {
        const target = GameManager.getOpponent(this.owner);
        let remainingCd = (this.cooldownMS - this.chargeTime) / ((1 + this.owner.attributes.getValue(EAttribute.CooldownRecovery) / 100) * 1000);
        tooltipTitle.textContent = this.name + " (" + remainingCd.toFixed(2) + "s)";
        if (this.toolTipId !== prevId || (!this.target && target)) {
            this.target = target;
            tooltipContent.replaceChildren();

            let damageHeader = document.createElement("h3");
            damageHeader.className = "tooltipHeader";
            damageHeader.textContent = "Damage:";
            tooltipContent.appendChild(damageHeader);

            let damageList = document.createElement("ul");
            damageList.className = "tooltipList";
            damageList.id = this.name + "_damageList" 

            this.createDamageListContent(damageList, target);

            tooltipContent.appendChild(damageList);

            let descHeader = document.createElement("h3");
            descHeader.className = "tooltipHeader";
            descHeader.textContent = "Description:";
            tooltipContent.appendChild(descHeader);

            let desc = document.createElement("p");
            desc.className = "tooltipText";
            desc.textContent = "Descriptions coming soon!";
            tooltipContent.appendChild(desc);
        }
        return this.toolTipId;
    }

    calculateDamage() {
        let dmg = this.baseDamage;
        this.damageMultipliers.forEach((dmgType) => {
            dmg += this.owner.attributes.getValue(dmgType.attributeId) * dmgType.multiplier;
        })
        return dmg
    }

    createDamageListContent(damageList: HTMLUListElement, target: Battler) {
        const attributeData = DataManager.attributes;
        let baseDam = document.createElement("li");
        baseDam.textContent = "Base: " + this.baseDamage;
        damageList.appendChild(baseDam);

        let totDam = this.baseDamage;

        this.damageMultipliers.forEach((mult) => {
            let multiplier = document.createElement("li");
            let dam = this.owner.attributes.getValue(mult.attributeId) * mult.multiplier;
            multiplier.textContent = attributeData[mult.attributeId].name + ": " + formatNum(dam) + " (" + formatNum(mult.multiplier) + "x)";
            totDam += dam;
            damageList.appendChild(multiplier);
        })

        let totalDam = document.createElement("li");
        totalDam.textContent = "Total: " + formatNum(totDam);
        damageList.appendChild(totalDam);

        if (target) {
            let adjDamage = document.createElement("li"); 
            adjDamage.id = this.name + '_aDmg'
            let adjDmg = totDam - target.attributes.getValue(EAttribute.Defense);
            adjDmg = adjDmg > 0 ? adjDmg : 0;
            adjDamage.textContent = "Adjusted Total: " + formatNum(adjDmg);
            damageList.appendChild(adjDamage);
        }

        let cooldown = document.createElement("li");
        let cd = this.cooldownMS / 1000 / (1 + this.owner.attributes.getValue(EAttribute.CooldownRecovery) / 100);
        cooldown.textContent = "Cooldown: " + formatNum(cd) + "s";
        damageList.appendChild(cooldown);

        let damagePerSecond = document.createElement("li");
        let dps = totDam / cd;
        damagePerSecond.textContent = "DPS: " + formatNum(dps) + "/s";
        damageList.appendChild(damagePerSecond);

        if (target) {
            let adjDamagePerSecond = document.createElement('li');
            adjDamagePerSecond.id = this.name + '_aDPS';
            let adjDps = (totDam - target.attributes.getValue(EAttribute.Defense)) / cd;
            adjDamagePerSecond.textContent = "Adjusted DPS: " + formatNum(adjDps) + '/s';
            damageList.appendChild(adjDamagePerSecond);

        }
    }
}
