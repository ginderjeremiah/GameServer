import {
	ApiEndpoint,
	ApiEndpointWithRequest,
	ApiEndpointNoRequest,
	ApiRequestTypes,
	ApiResponseTypes
} from './types/api-type-map';
import { ApiResponse } from './api-response';
import { keys } from '../common/functions';
import { ensureValidAccessToken, handleAuthFailure, refreshTokens } from './auth';
import { ensureDeviceFingerprint, getDeviceFingerprint } from './device-fingerprint';
import { getTokens } from './token-store';

/** Header carrying the client-computed device fingerprint, used by the backend for connection tracking. */
const DEVICE_FINGERPRINT_HEADER = 'X-Device-Fingerprint';

/**
 * Endpoints that authenticate the caller themselves (login, account creation) or operate on the
 * refresh token directly (refresh, logout). They are `[AllowAnonymous]` on the backend, so the bearer
 * token / 401-refresh machinery must be skipped for them — otherwise a logout would try to refresh a
 * token it is about to revoke, and the refresh call could recurse.
 */
const ANONYMOUS_ENDPOINTS: ReadonlySet<string> = new Set([
	'Login',
	'Login/CreateAccount',
	'Login/Refresh',
	'Login/Logout'
]);

export class ApiRequest<U extends ApiEndpoint> {
	endpoint: U;
	private readonly requiresAuth: boolean;

	constructor(endpoint: U) {
		this.endpoint = endpoint;
		this.requiresAuth = !ANONYMOUS_ENDPOINTS.has(endpoint);
	}

	public get(): U extends ApiEndpointNoRequest ? Promise<ApiResponse<ApiResponseTypes[U]>> : never;
	public get(
		urlParams: U extends ApiEndpointWithRequest ? ApiRequestTypes[U] : never
	): Promise<ApiResponse<ApiResponseTypes[U]>>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs aren't assignable to a concrete index signature.
	public get(urlParams?: Record<string, any>) {
		const params = this.encodeParams(urlParams);
		const endpoint = params ? this.endpoint + '?' + params : this.endpoint;
		return this.execute('GET', `/api/${endpoint}`);
	}

	public static get<U extends ApiEndpointNoRequest>(endpoint: U): Promise<ApiResponseTypes[U]>;
	public static get<U extends ApiEndpointWithRequest>(
		endpoint: U,
		urlParams: ApiRequestTypes[U]
	): Promise<ApiResponseTypes[U]>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs can't be expressed as a single concrete param type.
	public static async get<U extends ApiEndpoint>(endpoint: U, urlParams?: any) {
		const request = new ApiRequest(endpoint);
		const result = await request.get(urlParams);
		return result.data;
	}

	public post(): U extends ApiEndpointNoRequest ? Promise<ApiResponse<ApiResponseTypes[U]>> : never;
	public post<T extends U & ApiEndpointWithRequest>(
		payload: ApiRequestTypes[T]
	): Promise<ApiResponse<ApiResponseTypes[U]>>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs aren't assignable to a concrete index signature.
	public post(payload?: any) {
		return this.execute('POST', `/api/${this.endpoint}`, payload);
	}

	public static post<U extends ApiEndpointNoRequest>(endpoint: U): Promise<ApiResponseTypes[U]>;
	public static post<U extends ApiEndpointWithRequest>(
		endpoint: U,
		payload: ApiRequestTypes[U]
	): Promise<ApiResponseTypes[U]>;
	// eslint-disable-next-line @typescript-eslint/no-explicit-any -- implementation signature behind the typed overloads above; the per-endpoint request DTOs can't be expressed as a single concrete param type.
	public static async post<U extends ApiEndpoint>(endpoint: U, payload?: any) {
		const request = new ApiRequest(endpoint);
		const result = await request.post(payload);
		return result.data;
	}

	/**
	 * Sends the request, attaching the bearer access token, and transparently handles token expiry.
	 * Before authenticated requests the access token is refreshed pre-emptively when it is about to
	 * expire; if the server still rejects the request as unauthorized (401), the token pair is
	 * refreshed once and the request is retried. The user is only routed back to the login screen when
	 * that refresh is definitively rejected (the refresh token is spent/revoked) — a retryable failure
	 * (network blip, transient server error) just leaves the 401 response for the caller to handle.
	 */
	private async execute(
		method: 'GET' | 'POST',
		url: string,
		// eslint-disable-next-line @typescript-eslint/no-explicit-any -- the typed overloads above guarantee the payload matches the endpoint; this is the shared implementation.
		payload?: any,
		isRetry = false
	): Promise<ApiResponse<ApiResponseTypes[U]>> {
		let accessToken: string | null = null;
		if (this.requiresAuth) {
			// Refresh pre-emptively if needed and carry the resulting token straight through to send(),
			// rather than re-reading the token store there.
			accessToken = (await ensureValidAccessToken()).accessToken;
			// Compute the device fingerprint (once, then cached) so it can be attached below.
			await ensureDeviceFingerprint();
		}

		const response = await this.send(method, url, payload, accessToken);

		if (response.status === 401 && this.requiresAuth && !isRetry) {
			const outcome = await refreshTokens();
			if (outcome.status === 'success') {
				return this.execute(method, url, payload, true);
			}
			if (outcome.status === 'rejected') {
				// A concurrent tab can rotate in a fresh pair in the gap between refreshTokens()'s own
				// re-read and here; adopt it instead of tearing down a session that's still alive elsewhere.
				if (getTokens() !== null) {
					return this.execute(method, url, payload, true);
				}
				handleAuthFailure();
			}
			// A retryable failure just surfaces the original 401 to the caller — the stored refresh token
			// may still be good, so neither tokens nor session are torn down over it.
		}

		return response;
	}

	private async send(
		method: 'GET' | 'POST',
		url: string,
		// eslint-disable-next-line @typescript-eslint/no-explicit-any -- the typed overloads above guarantee the payload matches the endpoint; this is the shared implementation.
		payload?: any,
		accessToken: string | null = null
	): Promise<ApiResponse<ApiResponseTypes[U]>> {
		const headers: Record<string, string> = {};
		if (method === 'POST') {
			headers['content-type'] = 'application/json';
		}

		if (this.requiresAuth) {
			if (accessToken) {
				headers['Authorization'] = `Bearer ${accessToken}`;
			}

			const fingerprint = getDeviceFingerprint();
			if (fingerprint) {
				headers[DEVICE_FINGERPRINT_HEADER] = fingerprint;
			}
		}

		try {
			const response = await fetch(url, {
				method,
				headers,
				body: payload === undefined ? undefined : JSON.stringify(payload)
			});
			const responseText = await response.text();
			return new ApiResponse({
				status: response.status,
				statusText: response.statusText,
				responseText
			});
		} catch {
			// Network error / aborted request: surface a 0-status, bodyless response so callers handle it
			// the same way the previous XHR `onerror`/`onabort` path did.
			return new ApiResponse({ status: 0, statusText: '', responseText: '' });
		}
	}

	private encodeParams(urlParams?: Record<string, string | number | boolean>) {
		if (!urlParams) {
			return '';
		}

		return keys(urlParams)
			.filter((key) => urlParams[key] !== undefined)
			.map((key) => key + '=' + window.encodeURIComponent(urlParams[key]))
			.join('&');
	}
}
