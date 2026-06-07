import { confirmModal } from '$stores';
import { logout } from '$lib/api';

/**
 * Asks the user to confirm before logging out. Logging out tears down all in-memory game state
 * (engines, managers, websocket connection), so this guard prevents an accidental click on the
 * quit control from ending the session without warning.
 */
export const confirmQuit = async (): Promise<void> => {
	const confirmed = await confirmModal({
		title: 'Log out?',
		body: "You'll be signed out and returned to the login screen.",
		confirmLabel: 'Log out',
		cancelLabel: 'Stay'
	});
	if (confirmed) {
		void logout();
	}
};
