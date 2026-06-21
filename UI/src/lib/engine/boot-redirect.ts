/* The boot gate's pure navigation decision. Kept separate from session.ts (which owns the async
   session-restore + reference-cache logic) so it stays dependency-free and trivially testable — and
   so the root layout can use it without pulling the resume machinery into scope. */

import type { ResumeDestination } from './session';

/**
 * The boot gate's navigation decision after a resume: a route to `goto`, or `null` to stay on the
 * current path. Mirrors {@link ResumeDestination} (`game`/`loading`/`login`) so the layout can switch
 * on it with literal `resolve(...)` calls.
 */
export type BootRedirect = 'game' | 'loading' | 'login' | null;

/**
 * Routes that exist only as part of the boot/auth flow (login, character-select, loading). An
 * authenticated session must not be left resting on one, so a resume that lands "in the game" hands
 * off to `/game` from here — but stays put on any real in-app route (e.g. `/admin`) so a refresh keeps
 * the player exactly where they were.
 */
const BOOT_ONLY_ROUTES = new Set(['/', '/select', '/loading']);

/**
 * Routes reachable while authenticated but before a player aggregate is loaded into memory: the login
 * form and the character-select screen (where a character is chosen but not yet bound/loaded). Distinct
 * from {@link BOOT_ONLY_ROUTES}, which includes `/loading` — by the time the player reaches loading a
 * character has been selected and `playerManager` initialized.
 */
const PRE_PLAYER_ROUTES = new Set(['/', '/select']);

/**
 * The post-boot safety net's decision: once the boot gate has resolved, an in-memory player is required
 * on every route except the pre-player auth routes, so losing it (e.g. an auth teardown that cleared the
 * session) returns the user to login. Arriving at `/` or `/select` without a loaded player is expected —
 * the player isn't loaded until a character is selected — so those must not be bounced.
 */
export function shouldReturnToLogin(hasPlayer: boolean, pathname: string): boolean {
	return !hasPlayer && !PRE_PLAYER_ROUTES.has(pathname);
}

/**
 * Decides where the boot gate should navigate after `resumeSession`, given the resolved destination
 * and the path the load happened on. Returns `null` to stay on the current path.
 *
 * The key behaviour: a fully-restored session (`game`) keeps the player on whatever in-app route they
 * refreshed on, only handing off to `/game` from the transient boot/auth routes (login, loading). A
 * `loading` resume always routes to the loading screen; a failed restore (`login`) sends any protected
 * route back to the login form but leaves a load already on `/` untouched.
 */
export function bootRedirect(destination: ResumeDestination, currentPath: string): BootRedirect {
	if (destination === 'loading') {
		return 'loading';
	}
	if (destination === 'game') {
		return BOOT_ONLY_ROUTES.has(currentPath) ? 'game' : null;
	}
	return currentPath === '/' ? null : 'login';
}
