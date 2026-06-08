/**
 * Pure validation + password-strength helpers for the login/signup form.
 * Kept free of Svelte/DOM dependencies so they can be unit-tested directly and
 * reused across the login components without duplicating the rules inline.
 */

export type LoginMode = 'login' | 'signup';

/** The five visual states the login status line can take. */
export type StatusType = 'ok' | 'err' | 'warn' | 'info' | 'idle';

export interface FieldValidity {
	/** Whether the field currently satisfies its rules. */
	ok: boolean;
	/** A short human-readable message describing the state (empty when nothing to say). */
	msg: string;
}

export interface PasswordStrength {
	/** Number of filled meter segments, 0–4. */
	score: number;
	/** Human-readable strength label. */
	label: string;
}

const USERNAME_RE = /^[a-zA-Z][a-zA-Z0-9_-]{2,19}$/;

// Indexed by the raw strength point total (0–5).
const STRENGTH_LABELS = ['Too short', 'Weak', 'Fair', 'Good', 'Strong', 'Strong'];

export const validateUsername = (username: string): FieldValidity => {
	if (!username) {
		return { ok: false, msg: 'Required' };
	}
	if (username.length < 3) {
		return { ok: false, msg: 'At least 3 characters' };
	}
	if (username.length > 20) {
		return { ok: false, msg: 'Max 20 characters' };
	}
	if (/^[0-9]/.test(username)) {
		return { ok: false, msg: "Can't start with a number" };
	}
	if (!USERNAME_RE.test(username)) {
		return { ok: false, msg: 'Letters, numbers, _ or - only' };
	}
	return { ok: true, msg: 'Looks good' };
};

export const validatePassword = (password: string, mode: LoginMode): FieldValidity => {
	if (!password) {
		return { ok: false, msg: 'Required' };
	}
	// When signing in we only require that something was entered; the strength
	// rules are reserved for choosing a new password during signup.
	if (mode === 'login') {
		return { ok: true, msg: '' };
	}
	if (password.length < 8) {
		return { ok: false, msg: 'At least 8 characters' };
	}
	if (!/[a-zA-Z]/.test(password) || !/[0-9]/.test(password)) {
		return { ok: false, msg: 'Mix letters and numbers' };
	}
	return { ok: true, msg: '' };
};

export const validateConfirm = (confirm: string, password: string, mode: LoginMode): FieldValidity => {
	if (mode === 'login') {
		return { ok: true, msg: '' };
	}
	if (!confirm) {
		return { ok: false, msg: 'Required' };
	}
	if (confirm !== password) {
		return { ok: false, msg: "Passwords don't match" };
	}
	return { ok: true, msg: 'Match' };
};

/**
 * Scores a password from 0–4 (filled meter segments) and resolves a matching
 * label. Single source of truth for both the meter and the status line, which
 * previously duplicated the scoring rules.
 */
export const passwordStrength = (password: string): PasswordStrength => {
	if (!password) {
		return { score: 0, label: '—' };
	}

	let points = 0;
	if (password.length >= 8) {
		points++;
	}
	if (password.length >= 12) {
		points++;
	}
	if (/[a-z]/.test(password) && /[A-Z]/.test(password)) {
		points++;
	}
	if (/[0-9]/.test(password)) {
		points++;
	}
	if (/[^A-Za-z0-9]/.test(password)) {
		points++;
	}

	return { score: Math.min(points, 4), label: STRENGTH_LABELS[points] };
};

/** The form state the status line summarises, in priority order of what it surfaces. */
export interface StatusLineState {
	/** Server-side error from the most recent submit, if any. */
	serverError: string | null;
	/** Whether the submit succeeded and we're entering the world. */
	success: boolean;
	mode: LoginMode;
	/** Per-field messages, already gated by touched/submitted state ('' when not shown). */
	usernameError: string;
	passwordError: string;
	confirmError: string;
	capsLock: boolean;
	password: string;
	/** Current password-strength label (signup only). */
	strengthLabel: string;
	/** Whether every field currently validates. */
	formValid: boolean;
	username: string;
}

/**
 * Resolves the single status-line message shown beneath the form. The branches are
 * ordered by priority: a server error or success outcome wins, then field errors,
 * then advisory states (caps lock, password strength), and finally a "Ready" / idle
 * resting state. Kept pure so the precedence rules can be unit-tested directly.
 */
export const deriveStatusLine = (state: StatusLineState): { type: StatusType; text: string } => {
	if (state.serverError) {
		return { type: 'err', text: state.serverError };
	}
	if (state.success) {
		return {
			type: 'ok',
			text: state.mode === 'login' ? 'Signed in — loading world…' : 'Account created — entering…'
		};
	}
	if (state.usernameError) {
		return { type: 'err', text: 'Username · ' + state.usernameError.toLowerCase() };
	}
	if (state.passwordError) {
		return { type: 'err', text: 'Password · ' + state.passwordError.toLowerCase() };
	}
	if (state.confirmError) {
		return { type: 'err', text: 'Confirm · ' + state.confirmError.toLowerCase() };
	}
	if (state.capsLock) {
		return { type: 'warn', text: 'Caps Lock is on' };
	}
	if (state.mode === 'signup' && state.password) {
		return { type: 'info', text: `Strength · ${state.strengthLabel.toLowerCase()}` };
	}
	if (state.formValid && state.username) {
		return { type: 'ok', text: 'Ready' };
	}
	return { type: 'idle', text: ' ' };
};
