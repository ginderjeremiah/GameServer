import {
	IDefeatEnemyResponse,
	IEnemyInstance,
	IInventoryUpdate,
	INewEnemyModel,
	INewEnemyRequest
} from "./"

export type ApiSocketResponseTypes = {
	'DefeatEnemy': IDefeatEnemyResponse
	'NewEnemy': INewEnemyModel
	'SocketReplaced': undefined
	'UpdateInventorySlots': undefined
}

export type ApiSocketRequestTypes = {
	'DefeatEnemy': IEnemyInstance
	'NewEnemy': INewEnemyRequest
	'UpdateInventorySlots': IInventoryUpdate[]
}

export type ApiSocketCommand = keyof ApiSocketResponseTypes

export type ApiSocketCommandWithRequest = keyof ApiSocketRequestTypes

export type ApiSocketCommandNoRequest = Exclude<ApiSocketCommand, ApiSocketCommandWithRequest>

export type ApiSocketResponseType = ApiSocketResponseTypes[ApiSocketCommand]