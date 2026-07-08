// Shared across the page test and FightScreenStub: counts how many times the stubbed 'fight' screen
// has mounted, so the test can assert whether `{#key screenNonce}` actually remounted it.
export const mountTracker = { fight: 0 };
