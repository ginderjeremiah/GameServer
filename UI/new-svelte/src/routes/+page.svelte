<div class="login-screen" data-testid="login-screen">
	<div class="login-form">
		<DiamondMark />

		<div class="heading">
			<h1 data-testid="login-heading">{mode === 'login' ? 'Welcome back.' : 'Begin a new run.'}</h1>
			<p class="subtitle">
				{mode === 'login' ? 'Sign in to continue.' : 'Pick a name. Forge ahead.'}
			</p>
		</div>

		<form onsubmit={preventDefault(handleSubmit)}>
			<UnderlineInput
				testid="username-input"
				placeholder="Username"
				bind:value={username}
				onblur={() => (touched.username = true)}
				error={!!usernameError}
				valid={!!username && usernameValid}
			>
				{#snippet trailing()}
					{#if username}
						<ValidationIcon type={usernameError ? 'err' : 'ok'} />
					{/if}
				{/snippet}
			</UnderlineInput>

			<UnderlineInput
				testid="password-input"
				type={showPassword ? 'text' : 'password'}
				placeholder="Password"
				bind:value={password}
				onblur={() => (touched.password = true)}
				onkeydown={detectCapsLock}
				onkeyup={detectCapsLock}
				error={!!passwordError}
				valid={!!password && passwordValid}
			>
				{#snippet trailing()}
					<button
						type="button"
						class="eye-button"
						data-testid="password-toggle"
						onclick={() => (showPassword = !showPassword)}
					>
						<EyeIcon visible={showPassword} />
					</button>
				{/snippet}
				{#snippet below()}
					{#if mode === 'signup' && password}
						<PasswordStrengthMeter score={strength.score} />
					{/if}
				{/snippet}
			</UnderlineInput>

			{#if mode === 'signup'}
				<UnderlineInput
					testid="confirm-input"
					type={showPassword ? 'text' : 'password'}
					placeholder="Confirm password"
					bind:value={confirm}
					onblur={() => (touched.confirm = true)}
					error={!!confirmError}
					valid={!!confirm && confirmValid}
				>
					{#snippet trailing()}
						{#if confirm}
							<ValidationIcon type={confirmError ? 'err' : 'ok'} />
						{/if}
					{/snippet}
				</UnderlineInput>
			{/if}

			<StatusLine type={statusLine.type} text={statusLine.text} />

			<SubmitButton ready={formValid} {submitting} label={mode === 'login' ? 'Sign In' : 'Create'} />
		</form>

		<div class="mode-toggle">
			{mode === 'login' ? 'No account yet?' : 'Already a hero?'}
			<button class="mode-link" data-testid="mode-toggle" onclick={toggleMode}>
				{mode === 'login' ? 'Create one' : 'Sign in'}
			</button>
		</div>
	</div>
</div>

<script lang="ts">
import { ApiRequest, getTokens, reportBrowserInfo, setTokens } from '$lib/api';
import { onMount } from 'svelte';
import { playerManager } from '$lib/engine';
import { preventDefault } from '$lib/common/event-wrappers';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import DiamondMark from './login/DiamondMark.svelte';
import EyeIcon from './login/EyeIcon.svelte';
import PasswordStrengthMeter from './login/PasswordStrengthMeter.svelte';
import StatusLine, { type StatusType } from './login/StatusLine.svelte';
import SubmitButton from './login/SubmitButton.svelte';
import UnderlineInput from './login/UnderlineInput.svelte';
import ValidationIcon from './login/ValidationIcon.svelte';
import {
	passwordStrength,
	validateConfirm,
	validatePassword,
	validateUsername,
	type LoginMode
} from './login/login-validation';

let mode = $state<LoginMode>('login');
let username = $state('');
let password = $state('');
let confirm = $state('');
let showPassword = $state(false);
let capsLock = $state(false);
let submitting = $state(false);
let submitted = $state(false);
let serverError = $state<string | null>(null);
let success = $state(false);
let touched = $state({ username: false, password: false, confirm: false });

const usernameValidation = $derived(validateUsername(username));
const passwordValidation = $derived(validatePassword(password, mode));
const confirmValidation = $derived(validateConfirm(confirm, password, mode));
const strength = $derived(passwordStrength(password));

const usernameValid = $derived(usernameValidation.ok);
const passwordValid = $derived(passwordValidation.ok);
const confirmValid = $derived(confirmValidation.ok);

const showErr = (key: keyof typeof touched) => touched[key] || submitted;
const usernameError = $derived(showErr('username') && !usernameValid ? usernameValidation.msg : '');
const passwordError = $derived(showErr('password') && !passwordValid ? passwordValidation.msg : '');
const confirmError = $derived(showErr('confirm') && !confirmValid ? confirmValidation.msg : '');

const formValid = $derived(usernameValid && passwordValid && (mode === 'login' || confirmValid));

const statusLine = $derived.by((): { type: StatusType; text: string } => {
	if (serverError) {
		return { type: 'err', text: serverError };
	}
	if (success) {
		return {
			type: 'ok',
			text: mode === 'login' ? 'Signed in — loading world…' : 'Account created — entering…'
		};
	}
	if (usernameError) {
		return { type: 'err', text: 'Username · ' + usernameError.toLowerCase() };
	}
	if (passwordError) {
		return { type: 'err', text: 'Password · ' + passwordError.toLowerCase() };
	}
	if (confirmError) {
		return { type: 'err', text: 'Confirm · ' + confirmError.toLowerCase() };
	}
	if (capsLock) {
		return { type: 'warn', text: 'Caps Lock is on' };
	}
	if (mode === 'signup' && password) {
		return { type: 'info', text: `Strength · ${strength.label.toLowerCase()}` };
	}
	if (formValid && username) {
		return { type: 'ok', text: 'Ready' };
	}
	return { type: 'idle', text: ' ' };
});

const detectCapsLock = (e: KeyboardEvent) => {
	if (typeof e.getModifierState === 'function') {
		capsLock = e.getModifierState('CapsLock');
	}
};

const toggleMode = () => {
	mode = mode === 'login' ? 'signup' : 'login';
	confirm = '';
	touched = { username: false, password: false, confirm: false };
	submitted = false;
	serverError = null;
	success = false;
};

const enterWorld = (data: Parameters<typeof playerManager.initialize>[0]) => {
	success = true;
	playerManager.initialize(data);
	setTimeout(() => goto(resolve('/loading')), 600);
};

const handleSubmit = async () => {
	submitted = true;
	serverError = null;
	if (!formValid) {
		return;
	}

	submitting = true;

	if (mode === 'signup') {
		const created = await new ApiRequest('Login/CreateAccount').post({ username, password });
		if (created.status !== 200) {
			submitting = false;
			serverError = created.error ?? 'Could not create account.';
			return;
		}
	}

	const response = await new ApiRequest('Login').post({ username, password });
	submitting = false;
	if (response.status === 200) {
		setTokens(response.data.tokens);
		// Fire-and-forget: report this device's browser info now that we're authenticated.
		void reportBrowserInfo();
		enterWorld(response.data.player);
	} else if (mode === 'signup') {
		serverError = response.error ?? 'Account created but login failed.';
	} else {
		serverError = response.error ?? 'Incorrect username or password.';
	}
};

onMount(async () => {
	// "Stay logged in": if a token pair survived a refresh, resume the session without re-entering
	// credentials. The request layer silently refreshes (and clears on failure) as needed, so an
	// expired/revoked pair simply falls through and leaves the user on the login screen.
	if (!getTokens()) {
		return;
	}

	try {
		const response = await new ApiRequest('Login/Status').get();
		if (response.status === 200) {
			// Fire-and-forget: refresh this device's browser info on a resumed session too.
			void reportBrowserInfo();
			playerManager.initialize(response.data);
			goto(resolve('/loading'));
		}
	} catch {
		// Session check failed — stay on login page
	}
});
</script>

<style lang="scss">
.login-screen {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	padding: 40px;
}

.login-form {
	width: 360px;
	max-width: 100%;
}

.heading {
	text-align: center;
	margin-bottom: 42px;

	h1 {
		margin: 0;
		padding: 0;
		font-size: 32px;
		font-weight: 400;
		letter-spacing: -0.5px;
		line-height: 1.1;
		color: var(--text-primary);
	}

	.subtitle {
		margin: 10px 0 0;
		font-size: 12.5px;
		color: var(--text-secondary);
		font-family: var(--mono);
		letter-spacing: 0.6px;
	}
}

.eye-button {
	border: none;
	background: transparent;
	cursor: pointer;
	padding: 6px;
	display: flex;
}

.mode-toggle {
	margin-top: 28px;
	text-align: center;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-secondary);
	letter-spacing: 0.4px;
}

.mode-link {
	color: var(--accent-light);
	cursor: pointer;
	background: none;
	border: none;
	border-bottom: 1px solid color-mix(in srgb, var(--accent-light) 55%, transparent);
	padding: 0 0 1px;
	font-family: inherit;
	font-size: inherit;
	letter-spacing: inherit;
}
</style>
