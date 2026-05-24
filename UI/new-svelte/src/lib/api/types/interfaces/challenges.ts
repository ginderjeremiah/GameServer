export interface IChallenge {
	id: number;
	name: string;
	description: string;
	challengeTypeId: number;
	targetEntityId?: number;
	targetCount: number;
	rewardItemId?: number;
	rewardItemModId?: number;
};

export interface IPlayerChallenge {
	challengeId: number;
	progress: number;
	completed: boolean;
	completedAt?: string;
};