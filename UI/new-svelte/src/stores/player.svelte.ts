import { IPlayerData } from '$lib/api';

let playerData = $state<IPlayerData>();

export const loadPlayerData = (data: IPlayerData) => {
	playerData = data;
};

export const player = {
	get data() {
		return playerData as IPlayerData;
	}
};
