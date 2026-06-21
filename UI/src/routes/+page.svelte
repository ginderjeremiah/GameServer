<div class="login-screen" data-testid="login-screen">
	<div class="login-form">
		<DiamondMark size={18} marginBottom={36} />

		<LoginHeading {mode} />

		<form onsubmit={preventDefault(handleSubmit)}>
			<UnderlineInput
				testid="username-input"
				id="username"
				label="Username"
				autocomplete="username"
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
				id="password"
				label="Password"
				autocomplete={mode === 'signup' ? 'new-password' : 'current-password'}
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
						aria-label={showPassword ? 'Hide password' : 'Show password'}
						aria-pressed={showPassword}
						onclick={() => (showPassword = !showPassword)}
					>
						<EyeIcon visible={showPassword} />
					</button>
				{/snippet}
				{#snippet below()}
					{#if mode === 'signup' && password}
						<PasswordStrengthMeter score={strength.score} label={strength.label} />
					{/if}
				{/snippet}
			</UnderlineInput>

			{#if mode === 'signup'}
				<UnderlineInput
					testid="confirm-input"
					id="confirm-password"
					label="Confirm password"
					autocomplete="new-password"
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

		<ModeToggle {mode} onToggle={toggleMode} />
	</div>
</div>

<script lang="ts">
import { ApiRequest, reportDeviceInfo, setTokens } from '$lib/api';
import { playerManager } from '$lib/engine';
import { preventDefault } from '$lib/common/event-wrappers';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import DiamondMark from '$components/DiamondMark.svelte';
import EyeIcon from './login/EyeIcon.svelte';
import LoginHeading from './login/LoginHeading.svelte';
import ModeToggle from './login/ModeToggle.svelte';
import PasswordStrengthMeter from './login/PasswordStrengthMeter.svelte';
import StatusLine from './login/StatusLine.svelte';
import SubmitButton from './login/SubmitButton.svelte';
import UnderlineInput from './login/UnderlineInput.svelte';
import ValidationIcon from './login/ValidationIcon.svelte';
import {
	deriveStatusLine,
	passwordStrength,
	validateConfirm,
	validatePassword,
	validateUsername,
	type LoginMode
} from './login/login-validation';
import { confirmSessionTakeover } from './login/session-takeover';

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

const statusLine = $derived(
	deriveStatusLine({
		serverError,
		success,
		mode,
		usernameError,
		passwordError,
		confirmError,
		capsLock,
		password,
		strengthLabel: strength.label,
		formValid,
		username
	})
);

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
	if (response.status === 200) {
		setTokens(response.data.tokens);

		// Login now returns the account's characters; selecting one binds the session and rotates the
		// token to carry the chosen player. The real player-select screen is #1070 — until then, auto-select
		// the first character so the existing single-character flow keeps working.
		const summaries = response.data.playerSummaries;
		if (summaries.length === 0) {
			submitting = false;
			serverError = 'This account has no characters.';
			return;
		}
		const selected = await new ApiRequest('Login/SelectPlayer').post({
			playerId: summaries[0].id,
			refreshToken: response.data.tokens.refreshToken
		});
		if (selected.status !== 200) {
			submitting = false;
			serverError = selected.error ?? 'Could not enter the game.';
			return;
		}
		setTokens(selected.data.tokens);

		// Warn before entering the game if the player is already connected elsewhere — entering would
		// take over (disconnect) that session. This per-player presence check runs after selection.
		// Declining logs this freshly-issued session back out.
		if (!(await confirmSessionTakeover())) {
			submitting = false;
			return;
		}
		// Fire-and-forget: report this device's capabilities now that we're authenticated.
		void reportDeviceInfo();
		enterWorld(selected.data.player);
	} else if (mode === 'signup') {
		submitting = false;
		serverError = response.error ?? 'Account created but login failed.';
	} else {
		submitting = false;
		serverError = response.error ?? 'Incorrect username or password.';
	}
};
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

.eye-button {
	border: none;
	background: transparent;
	cursor: pointer;
	padding: 6px;
	display: flex;

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}
}
</style>
