import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { ERole, hasRole } from '$lib/api';
import { toastError } from '$stores';

/**
 * Client-side guard for the admin area. Grants access only to holders of the {@link ERole.Admin}
 * role; otherwise it surfaces an access-denied toast and returns the user to the login page, then
 * reports that access was denied so the caller can keep the privileged UI from rendering.
 *
 * This is the UX layer only — the backend independently enforces the Admin role on every admin
 * endpoint (`AdminRoleAuthorizationFilter`), so the data calls would 403 even without this guard.
 */
export const ensureAdminAccess = (): boolean => {
	if (hasRole(ERole.Admin)) {
		return true;
	}

	toastError('You do not have access to the admin area.');
	void goto(resolve('/'));
	return false;
};
