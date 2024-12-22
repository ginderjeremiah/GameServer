<h1 class="login-title">Login</h1>
<div class="center-content">
	<div class="login-container round-border">
		<div class="login-subcontainer">
			<TextInput bind:value={username} type="text" name="username" label="Username" />
		</div>
		<div class="login-subcontainer">
			<TextInput bind:value={password} type="password" name="password" label="Password" />
		</div>
		{#if errorReason}
			<div class="login-subcontainer">
				{#if errorType === 'login'}
					<span class="error-message">An error occured while attempting to login.</span>
				{:else}
					<span class="error-message">Could not create account.</span>
				{/if}
				<span class="error-message">{errorReason}</span>
			</div>
		{/if}
		<div class="login-subcontainer">
			<Button onClick={attemptLogin} text="Login" loading={loginLoading} />
		</div>
		<div class="login-subcontainer">
			<Button onClick={createAccount} text="Create Account" loading={loginLoading} />
		</div>
	</div>
</div>

<script lang="ts">
import { loadPlayerData } from '$stores';
import { ApiRequest } from '$lib/api';
import { routeTo } from '$lib/common';
import { Button, TextInput } from '$components';
import { onMount } from 'svelte';

type ErrorType = 'login' | 'create-account';

let username = $state<string>();
let password = $state<string>();
let errorReason = $state<string>();
let errorType = $state<ErrorType>();
let loginLoading = $state(false);

onMount(async () => {
	try {
		loginLoading = false;
		const response = await new ApiRequest('Login/Status').get();
		if (response.status === 200) {
			loadPlayerData(response.data);
			routeTo('/loading');
		}
	} finally {
		loginLoading = false;
	}
});

const attemptLogin = async () => {
	if (!username || !password) {
		return;
	}

	loginLoading = true;
	const response = await new ApiRequest('Login').post({ username, password });
	loginLoading = false;
	if (response.status === 200) {
		loadPlayerData(response.data);
		routeTo('/loading');
	} else {
		errorType = 'login';
		errorReason = response.error;
	}
};

const createAccount = async () => {
	if (!username || !password) {
		return;
	}

	loginLoading = true;
	const response = await new ApiRequest('Login/CreateAccount').post({ username, password });
	if (response.status === 200) {
		await attemptLogin();
	} else {
		errorType = 'create-account';
		errorReason = response.error;
		loginLoading = false;
	}
};
</script>

<style lang="scss">
.login-title {
	text-align: center;
	margin-bottom: 2.5 rem;
	color: var(--title-color);
	text-shadow: var(--default-shadow);
}

.login-container {
	background-color: var(--container-background-color);
	border: var(--default-border);
	padding: 2rem;
	width: 10rem;
	height: fit-content;
	box-shadow: var(--default-shadow);

	.login-subcontainer + .login-subcontainer {
		margin-top: 1rem;
	}

	.error-message {
		font-size: medium;
		color: var(--error-color);
	}
}
</style>
