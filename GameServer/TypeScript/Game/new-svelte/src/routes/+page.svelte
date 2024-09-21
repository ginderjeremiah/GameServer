<h1 class="login-title">Login</h1>
<div class="center-content">
	<div class="login-container round-border">
		<div class="login-subcontainer">
			<TextInput bind:value="{username}" type="text" name="username" label="Username" />
		</div>
		<div class="login-subcontainer">
			<TextInput bind:value="{password}" type="password" name="password" label="Password" />
		</div>
		{#if loginFailedReason}
			<div class="login-subcontainer">
				<span class="error-message">An error occured while attempting to login.</span>
				<span class="error-message">{loginFailedReason}</span>
			</div>
		{/if}
		<div class="login-subcontainer">
			<Button on:click="{attemptLogin}" text="Login" loading="{loginLoading}" />
		</div>
	</div>
</div>

<script lang="ts">
import { player } from '$stores';
import { ApiRequest } from '$lib/api/api-request';
import { routeTo } from '$lib/common';
import Button from '$components/Button.svelte';
import TextInput from '$components/TextInput.svelte';

let username = '';
let password = '';

$: loginFailedReason = '';
$: loginLoading = false;

const attemptLogin = async () => {
	loginLoading = true;
	const response = await new ApiRequest('/Login').post({ username, password });
	loginLoading = false;
	if (response.status === 200) {
		$player = response.data;
		routeTo('/loading');
	} else {
		loginFailedReason = response.error;
	}
};
</script>

<style lang="scss">
.login-title {
	text-align: center;
	margin-bottom: 2.5 rem;
	color: var(--default-title-color);
	text-shadow: var(--default-shadow);
}

.login-container {
	background-color: var(--default-title-color);
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
