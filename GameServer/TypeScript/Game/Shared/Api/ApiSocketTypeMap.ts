type ApiSocketResponseTypes = {
	'DefeatEnemy': IDefeatEnemyResponse
	'NewEnemy': INewEnemyModel
	'SocketReplaced': undefined
}

type ApiSocketRequestTypes = {
	'DefeatEnemy': IEnemyInstance
	'NewEnemy': INewEnemyRequest
}

type ApiSocketCommand = keyof ApiSocketResponseTypes

type ApiSocketCommandWithRequest = keyof ApiSocketRequestTypes

type ApiSocketCommandNoRequest = Exclude<ApiSocketCommand, ApiSocketCommandWithRequest>

type ApiSocketResponseType = ApiSocketResponseTypes[ApiSocketCommand]