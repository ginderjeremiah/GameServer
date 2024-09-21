import {
	IDefeatEnemyResponse,
	IEnemyInstance,
	INewEnemyModel,
	INewEnemyRequest
} from "./"

export type ApiSocketResponseTypes = {
	'DefeatEnemy': IDefeatEnemyResponse
	'NewEnemy': INewEnemyModel
	'SocketReplaced': undefined
}

export type ApiSocketRequestTypes = {
	'DefeatEnemy': IEnemyInstance
	'NewEnemy': INewEnemyRequest
}

export type ApiSocketCommand = keyof ApiSocketResponseTypes

export type ApiSocketCommandWithRequest = keyof ApiSocketRequestTypes

export type ApiSocketCommandNoRequest = Exclude<ApiSocketCommand, ApiSocketCommandWithRequest>

export type ApiSocketResponseType = ApiSocketResponseTypes[ApiSocketCommand]