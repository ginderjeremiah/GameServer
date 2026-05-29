<div class="login-screen" data-testid="login-screen">
	<div class="login-form">
		<!-- Diamond mark -->
		<div class="diamond-container">
			<div class="diamond">
				<div class="diamond-inner"></div>
			</div>
		</div>

		<div class="heading">
			<h1 data-testid="login-heading">{mode === 'login' ? 'Welcome back.' : 'Begin a new run.'}</h1>
			<p class="subtitle">
				{mode === 'login' ? 'Sign in to continue.' : 'Pick a name. Forge ahead.'}
			</p>
		</div>

		<form onsubmit={preventDefault(handleSubmit)}>
			<!-- Username -->
			<div class="field-group">
				<input
					data-testid="username-input"
					type="text"
					placeholder="Username"
					bind:value={username}
					onblur={() => (touched.username = true)}
					class="underline-input"
					class:error={usernameError}
					class:valid={username && usernameValid}
				/>
				{#if username}
					<div class="field-icon">
						{#if usernameError}
							<svg width="16" height="16" viewBox="0 0 16 16" fill="none">
								<path
									d="M4 4l8 8M12 4l-8 8"
									stroke="var(--error)"
									stroke-width="2"
									stroke-linecap="round"
								/>
							</svg>
						{:else if usernameValid}
							<svg width="16" height="16" viewBox="0 0 16 16" fill="none">
								<path
									d="M3 8.5l3 3 7-7"
									stroke="var(--success)"
									stroke-width="2"
									stroke-linecap="round"
									stroke-linejoin="round"
								/>
							</svg>
						{/if}
					</div>
				{/if}
			</div>

			<!-- Password -->
			<div class="field-group" class:extra-margin={mode === 'signup'}>
				<input
					data-testid="password-input"
					type={showPassword ? 'text' : 'password'}
					placeholder="Password"
					bind:value={password}
					onblur={() => (touched.password = true)}
					onkeydown={detectCapsLock}
					onkeyup={detectCapsLock}
					class="underline-input"
					class:error={passwordError}
					class:valid={password && passwordValid}
				/>
				<button
					type="button"
					class="eye-button"
					data-testid="password-toggle"
					onclick={() => (showPassword = !showPassword)}
				>
					{#if showPassword}
						<svg width="16" height="16" viewBox="0 0 20 20" fill="none">
							<path
								d="M2 10s3-5 8-5 8 5 8 5-3 5-8 5-8-5-8-5z"
								stroke="rgba(240,240,240,0.75)"
								stroke-width="1.4"
							/>
							<circle cx="10" cy="10" r="2.5" stroke="rgba(240,240,240,0.75)" stroke-width="1.4" />
						</svg>
					{:else}
						<svg width="16" height="16" viewBox="0 0 20 20" fill="none">
							<path
								d="M2 10s3-5 8-5c2 0 3.7.8 5 1.8M18 10s-3 5-8 5c-2 0-3.7-.8-5-1.8"
								stroke="rgba(240,240,240,0.75)"
								stroke-width="1.4"
							/>
							<path
								d="M3 3l14 14"
								stroke="rgba(240,240,240,0.75)"
								stroke-width="1.4"
								stroke-linecap="round"
							/>
						</svg>
					{/if}
				</button>
				{#if mode === 'signup' && password}
					<div class="strength-meter" data-testid="strength-meter">
						{#each [0, 1, 2, 3] as i}
							<div
								class="strength-segment"
								style:background={i < strengthScore
									? strengthScore >= 3
										? 'var(--success)'
										: strengthScore >= 2
											? '#e8b850'
											: '#e0907a'
									: 'rgba(240,240,240,0.22)'}
							></div>
						{/each}
					</div>
				{/if}
			</div>

			<!-- Confirm password -->
			{#if mode === 'signup'}
				<div class="field-group">
					<input
						data-testid="confirm-input"
						type={showPassword ? 'text' : 'password'}
						placeholder="Confirm password"
						bind:value={confirm}
						onblur={() => (touched.confirm = true)}
						class="underline-input"
						class:error={confirmError}
						class:valid={confirm && confirmValid}
					/>
					{#if confirm}
						<div class="field-icon">
							{#if confirmError}
								<svg width="16" height="16" viewBox="0 0 16 16" fill="none">
									<path
										d="M4 4l8 8M12 4l-8 8"
										stroke="var(--error)"
										stroke-width="2"
										stroke-linecap="round"
									/>
								</svg>
							{:else if confirmValid}
								<svg width="16" height="16" viewBox="0 0 16 16" fill="none">
									<path
										d="M3 8.5l3 3 7-7"
										stroke="var(--success)"
										stroke-width="2"
										stroke-linecap="round"
										stroke-linejoin="round"
									/>
								</svg>
							{/if}
						</div>
					{/if}
				</div>
			{/if}

			<!-- Status line -->
			<div class="status-line" data-testid="status-line" style:color={statusColor}>
				{#if statusLine.type === 'ok'}
					<svg width="14" height="14" viewBox="0 0 16 16" fill="none">
						<path
							d="M3 8.5l3 3 7-7"
							stroke={statusColor}
							stroke-width="2"
							stroke-linecap="round"
							stroke-linejoin="round"
						/>
					</svg>
				{:else if statusLine.type === 'err'}
					<svg width="14" height="14" viewBox="0 0 16 16" fill="none">
						<path
							d="M4 4l8 8M12 4l-8 8"
							stroke={statusColor}
							stroke-width="2"
							stroke-linecap="round"
						/>
					</svg>
				{/if}
				{statusLine.text}
			</div>

			<!-- Submit button -->
			<button
				data-testid="submit-button"
				type="submit"
				class="submit-button"
				disabled={!formValid || submitting}
				class:ready={formValid}
			>
				{#if submitting}
					<div class="submit-spinner"></div>
					...
				{:else}
					{mode === 'login' ? 'Sign In' : 'Create'}
				{/if}
			</button>
		</form>

		<!-- Mode toggle -->
		<div class="mode-toggle">
			{mode === 'login' ? 'No account yet?' : 'Already a hero?'}
			{' '}
			<button class="mode-link" data-testid="mode-toggle" onclick={toggleMode}>
				{mode === 'login' ? 'Create one' : 'Sign in'}
			</button>
		</div>
	</div>
</div>

<script lang="ts">
import { ApiRequest } from '$lib/api';
import { routeTo } from '$lib/common';
import { onMount } from 'svelte';
import { playerManager } from '$lib/engine';
import { preventDefault } from '$lib/common/event-wrappers';

type Mode = 'login' | 'signup';

let mode = $state<Mode>('login');
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

const USERNAME_RE = /^[a-zA-Z][a-zA-Z0-9_-]{2,19}$/;

const usernameValidation = $derived.by(() => {
	if (!username) return { ok: false, msg: 'Required' };
	if (username.length < 3) return { ok: false, msg: 'At least 3 characters' };
	if (username.length > 20) return { ok: false, msg: 'Max 20 characters' };
	if (/^[0-9]/.test(username)) return { ok: false, msg: "Can't start with a number" };
	if (!USERNAME_RE.test(username)) return { ok: false, msg: 'Letters, numbers, _ or - only' };
	return { ok: true, msg: 'Looks good' };
});

const passwordValidation = $derived.by(() => {
	if (!password) return { ok: false, msg: 'Required' };
	if (mode === 'login') return { ok: true, msg: '' };
	if (password.length < 8) return { ok: false, msg: 'At least 8 characters' };
	if (!/[a-zA-Z]/.test(password) || !/[0-9]/.test(password))
		return { ok: false, msg: 'Mix letters and numbers' };
	return { ok: true, msg: '' };
});

const confirmValidation = $derived.by(() => {
	if (mode === 'login') return { ok: true, msg: '' };
	if (!confirm) return { ok: false, msg: 'Required' };
	if (confirm !== password) return { ok: false, msg: "Passwords don't match" };
	return { ok: true, msg: 'Match' };
});

const strengthScore = $derived.by(() => {
	if (!password) return 0;
	let s = 0;
	if (password.length >= 8) s++;
	if (password.length >= 12) s++;
	if (/[a-z]/.test(password) && /[A-Z]/.test(password)) s++;
	if (/[0-9]/.test(password)) s++;
	if (/[^A-Za-z0-9]/.test(password)) s++;
	return Math.min(s, 4);
});

const strengthLabel = $derived.by(() => {
	const labels = ['Too short', 'Weak', 'Fair', 'Good', 'Strong', 'Strong'];
	if (!password) return '—';
	let s = 0;
	if (password.length >= 8) s++;
	if (password.length >= 12) s++;
	if (/[a-z]/.test(password) && /[A-Z]/.test(password)) s++;
	if (/[0-9]/.test(password)) s++;
	if (/[^A-Za-z0-9]/.test(password)) s++;
	return labels[s];
});

const usernameValid = $derived(usernameValidation.ok);
const passwordValid = $derived(passwordValidation.ok);
const confirmValid = $derived(confirmValidation.ok);

const showErr = (key: keyof typeof touched) => touched[key] || submitted;
const usernameError = $derived(showErr('username') && !usernameValid ? usernameValidation.msg : '');
const passwordError = $derived(showErr('password') && !passwordValid ? passwordValidation.msg : '');
const confirmError = $derived(showErr('confirm') && !confirmValid ? confirmValidation.msg : '');

const formValid = $derived(usernameValid && passwordValid && (mode === 'login' || confirmValid));

const statusLine = $derived.by(() => {
	if (serverError) return { type: 'err' as const, text: serverError };
	if (success)
		return {
			type: 'ok' as const,
			text: mode === 'login' ? 'Signed in — loading world…' : 'Account created — entering…'
		};
	if (usernameError)
		return { type: 'err' as const, text: 'Username · ' + usernameError.toLowerCase() };
	if (passwordError)
		return { type: 'err' as const, text: 'Password · ' + passwordError.toLowerCase() };
	if (confirmError)
		return { type: 'err' as const, text: 'Confirm · ' + confirmError.toLowerCase() };
	if (capsLock) return { type: 'warn' as const, text: 'Caps Lock is on' };
	if (mode === 'signup' && password)
		return { type: 'info' as const, text: `Strength · ${strengthLabel.toLowerCase()}` };
	if (formValid && username) return { type: 'ok' as const, text: 'Ready' };
	return { type: 'idle' as const, text: ' ' };
});

const statusColor = $derived(
	{
		err: 'var(--error)',
		ok: 'var(--success)',
		warn: 'var(--warning)',
		info: 'var(--text-secondary)',
		idle: 'var(--text-tertiary)'
	}[statusLine.type]
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

const handleSubmit = async () => {
	submitted = true;
	serverError = null;
	if (!formValid) return;

	submitting = true;

	if (mode === 'login') {
		const response = await new ApiRequest('Login').post({ username, password });
		submitting = false;
		if (response.status === 200) {
			success = true;
			playerManager.initialize(response.data);
			setTimeout(() => routeTo('/loading'), 600);
		} else {
			serverError = response.error ?? 'Incorrect username or password.';
		}
	} else {
		const response = await new ApiRequest('Login/CreateAccount').post({ username, password });
		if (response.status === 200) {
			const loginResponse = await new ApiRequest('Login').post({ username, password });
			submitting = false;
			if (loginResponse.status === 200) {
				success = true;
				playerManager.initialize(loginResponse.data);
				setTimeout(() => routeTo('/loading'), 600);
			} else {
				serverError = loginResponse.error ?? 'Account created but login failed.';
			}
		} else {
			submitting = false;
			serverError = response.error ?? 'Could not create account.';
		}
	}
};

onMount(async () => {
	try {
		const response = await new ApiRequest('Login/Status').get();
		if (response.status === 200) {
			playerManager.initialize(response.data);
			routeTo('/loading');
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

.diamond-container {
	display: flex;
	justify-content: center;
	margin-bottom: 36px;
}

.diamond {
	width: 18px;
	height: 18px;
	transform: rotate(45deg);
	border: 1px solid var(--accent);
	box-shadow: 0 0 8px rgba(161, 194, 247, 0.4);
	position: relative;
}

.diamond-inner {
	position: absolute;
	inset: 4px;
	background: var(--accent);
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
		font-family: 'Geist Mono', monospace;
		letter-spacing: 0.6px;
	}
}

.field-group {
	position: relative;
	margin-bottom: 22px;

	&.extra-margin {
		margin-bottom: 22px;
	}
}

.underline-input {
	width: 100%;
	height: 44px;
	padding: 0 36px 0 0;
	background: transparent;
	border: none;
	border-bottom: 1px solid rgba(240, 240, 240, 0.45);
	outline: none;
	color: var(--text-primary);
	font-size: 18px;
	font-family: inherit;
	letter-spacing: 0.2px;
	transition: border-color 160ms;

	&.error {
		border-bottom-color: #e08778;
	}

	&.valid {
		border-bottom-color: rgba(168, 212, 160, 0.85);
	}
}

.field-icon {
	position: absolute;
	right: 0;
	top: 14px;
	display: flex;
}

.eye-button {
	position: absolute;
	right: 0;
	top: 10px;
	border: none;
	background: transparent;
	cursor: pointer;
	padding: 6px;
	display: flex;
}

.strength-meter {
	display: flex;
	gap: 4px;
	justify-content: space-between;
	margin-top: 10px;
}

.strength-segment {
	flex: 1;
	height: 2px;
	transition: background 160ms;
}

.status-line {
	font-family: 'Geist Mono', monospace;
	font-size: 11.5px;
	letter-spacing: 0.6px;
	text-align: center;
	min-height: 18px;
	margin-bottom: 22px;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 6px;
	transition: color 160ms;
}

.submit-button {
	width: 100%;
	padding: 13px 0;
	background: transparent;
	color: var(--text-tertiary);
	border: 1px solid rgba(240, 240, 240, 0.35);
	border-radius: 2px;
	cursor: not-allowed;
	font-size: 13px;
	font-weight: 500;
	font-family: inherit;
	letter-spacing: 2px;
	text-transform: uppercase;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 10px;
	transition: all 180ms;

	&.ready {
		background: var(--accent);
		color: #111;
		border-color: var(--accent);
		cursor: pointer;
	}

	&:disabled {
		cursor: not-allowed;
	}
}

.submit-spinner {
	width: 11px;
	height: 11px;
	border: 2px solid rgba(0, 0, 0, 0.25);
	border-top-color: #111;
	border-radius: 50%;
	animation: spin 0.8s linear infinite;
}

.mode-toggle {
	margin-top: 28px;
	text-align: center;
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	color: rgba(240, 240, 240, 0.65);
	letter-spacing: 0.4px;
}

.mode-link {
	color: var(--accent-light);
	cursor: pointer;
	background: none;
	border: none;
	border-bottom: 1px solid rgba(192, 216, 255, 0.55);
	padding: 0 0 1px;
	font-family: inherit;
	font-size: inherit;
	letter-spacing: inherit;
}
</style>
