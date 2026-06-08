import { getRoles } from './token-store';

/**
 * Access roles a user can be granted, mirroring the backend `Game.Core.ERole`. The access token
 * carries the role's *name* (e.g. "Admin") as the claim value, so this is a string enum keyed to
 * those names rather than the backend's numeric ids. Like the other hand-maintained domain enums on
 * the frontend, it is intentionally not codegen'd from the API DTOs.
 */
export enum ERole {
	Admin = 'Admin'
}

/** Whether the currently authenticated user holds the given role (read from the access token). */
export const hasRole = (role: ERole): boolean => getRoles().includes(role);
