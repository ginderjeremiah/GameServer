/// <reference path="../Abstract/Tooltippable.ts"/>
class Skill extends Tooltippable implements ISkill {
    skillId: number
    skillName: string;
    chargeTime: number;
    baseDamage: number;
    damageMultipliers: IAttributeMultiplier[];
    skillDesc: string;
    cooldownMS: number;
    iconPath: string;
    owner: Battler;
    ownerStatsVersion = -1;
    target?: Battler;

    constructor(data: ISkill, owner: Battler) {
        super();
        this.skillId = data.skillId
        this.skillName = data.skillName;
        this.baseDamage = data.baseDamage;
        this.damageMultipliers = data.damageMultipliers;
        this.skillDesc = data.skillDesc;
        this.cooldownMS = data.cooldownMS;
        this.chargeTime = 0;
        this.iconPath = data.iconPath;
        this.owner = owner;
    }

    updateTooltipData(tooltipTitle: HTMLHeadingElement, tooltipContent: HTMLDivElement, prevId: number): number {
        const target = GameManager.getOpponent(this.owner);
        let remainingCd = (this.cooldownMS - this.chargeTime) / ((1 + this.owner.attributes.getValue(AttributeTypes.CooldownRecovery) / 100) * 1000);
        tooltipTitle.textContent = this.skillName + " (" + remainingCd.toFixed(2) + "s)";
        if (this.toolTipId !== prevId) {
            this.ownerStatsVersion = this.owner.statsVersion;
            this.target = target;
            tooltipContent.replaceChildren();

            let damageHeader = document.createElement("h3");
            damageHeader.className = "tooltipHeader";
            damageHeader.textContent = "Damage:";
            tooltipContent.appendChild(damageHeader);

            let damageList = document.createElement("ul");
            damageList.className = "tooltipList";
            damageList.id = this.skillName + "_damageList" 

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
        } else if (this.ownerStatsVersion !== this.owner.statsVersion) {
            this.ownerStatsVersion = this.owner.statsVersion;
            this.target = target;
            let damageList = document.getElementById(this.skillName + "_damageList") as HTMLUListElement;
            damageList.replaceChildren();

            this.createDamageListContent(damageList, target);
            
        } else if(this.target !== target) {
            this.target = target;
            let totDam = this.calculateDamage();
            let adjDamage = document.getElementById(this.skillName + '_aDmg') as HTMLLIElement; 
            let adjDmg = totDam - target.attributes.getValue(AttributeTypes.Defense);
            adjDmg = adjDmg > 0 ? adjDmg : 0;
            adjDamage.textContent = "Adjusted Total: " + formatNum(adjDmg);

            let adjustedDamagePerSecond = document.getElementById(this.skillName + '_aDPS') as HTMLLIElement;
            let adjustedDps = adjDmg / (this.cooldownMS / 1000 / (1 + this.owner.attributes.getValue(AttributeTypes.CooldownRecovery) / 100));
            adjustedDamagePerSecond.textContent = "Adjusted DPS: " + formatNum(adjustedDps) + '/s';
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
            multiplier.textContent = attributeData[mult.attributeId].attributeName + ": " + formatNum(dam) + " (" + formatNum(mult.multiplier) + "x)";
            totDam += dam;
            damageList.appendChild(multiplier);
        })

        let totalDam = document.createElement("li");
        totalDam.textContent = "Total: " + formatNum(totDam);
        damageList.appendChild(totalDam);

        let adjDamage = document.createElement("li"); 
        adjDamage.id = this.skillName + '_aDmg'
        let adjDmg = totDam - target.attributes.getValue(AttributeTypes.Defense);
        adjDmg = adjDmg > 0 ? adjDmg : 0;
        adjDamage.textContent = "Adjusted Total: " + formatNum(adjDmg);
        damageList.appendChild(adjDamage);

        let cooldown = document.createElement("li");
        let cd = this.cooldownMS / 1000 / (1 + this.owner.attributes.getValue(AttributeTypes.CooldownRecovery) / 100);
        cooldown.textContent = "Cooldown: " + formatNum(cd) + "s";
        damageList.appendChild(cooldown);

        let damagePerSecond = document.createElement("li");
        let dps = totDam / cd;
        damagePerSecond.textContent = "DPS: " + formatNum(dps) + "/s";
        damageList.appendChild(damagePerSecond);

        let adjDamagePerSecond = document.createElement('li');
        adjDamagePerSecond.id = this.skillName + '_aDPS';
        let adjDps = adjDmg / cd;
        adjDamagePerSecond.textContent = "Adjusted DPS: " + formatNum(adjDps) + '/s';
        damageList.appendChild(adjDamagePerSecond);
    }
}
