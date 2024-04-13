/// <reference path="Battler.ts"/>
class Player extends Battler {
    currentExp: number;
    maxSkills: number = 4;
    statPointsGained: number;
    statPointsUsed: number;

    constructor(playerData: IPlayerData, additionalAttributes: IBattlerAttribute[]) {
        super(playerData, "player", additionalAttributes);
        this.currentExp = playerData.exp;
        this.statPointsGained = playerData.statPointsGained;
        this.statPointsUsed = playerData.statPointsUsed;
    }
    
    updateExpDisplay(): void {
        //TODO create meter for displaying exp?
    }
}
