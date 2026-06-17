# Frontend Overlay Systems (Toast, Modal, Popover)

The global overlay UI — transient notifications, blocking dialogs, and non-blocking overlays — shares one standardized design. It is split from [frontend.md](./frontend.md) so the main doc stays an overview; the per-system mechanics live here next to each other since they're variations on the same pattern (a store/host pair in the root layout, presentation-only cards, CSS-driven motion that collapses under `prefers-reduced-motion`).

## Toast notifications

User-facing notifications (currently errors; the system is type-aware for future success/warning/info) are a single global toast system. The `toast` store owns the active toasts in a keyed map (mirroring the tooltip store, so dismissal is order-independent) and exposes `showToast` plus per-type helpers; a single `ToastContainer` in the root layout renders the stack and the individual `Toast` components are presentation-only. The visual treatment derives from a single per-type `--toast-accent` token so a theme can restyle a status by overriding that one variable.

- **The store is the single entry point** — anywhere imports a helper and calls it; error sources are wired to the store rather than rendering ad-hoc UI (socket failures are subscribed once in the root layout; the Workbench reports save/load failures that were previously swallowed).
- **Duplicate messages collapse** — a repeated identical message refreshes the existing toast's timer instead of stacking, so a flapping connection can't bury the screen.
- **Auto-dismiss is opt-out** — pass `duration: 0` for a sticky toast.
- **The store removes synchronously; the view animates the exit.** Dismissal deletes from the store immediately (logically gone), but `ToastContainer` keeps its own render list and lets a removed toast linger marked `leaving` until its CSS exit animation finishes — so the motion lives in CSS and collapses under `prefers-reduced-motion`, while the store stays the single synchronous source of truth.

(An optional inline `action` button and an `onDismiss` callback are part of the standardized design and provided for future use.)

## Modal dialogs

Blocking confirmation dialogs are the toast system's counterpart, from the same standardized design. The `modal` store is the single entry point; a single `ModalHost` in the root layout renders the active dialog, and the presentation-only `Modal` flexes across `confirm`/`acknowledge`/`destructive` kinds.

- **Promise-based, await-able API.** `showModal(options)` (and the `confirmModal`/`acknowledgeModal`/`dangerModal` helpers) returns a `Promise<boolean>`, so a caller writes `if (await confirmModal({ … })) { … }`. Used by the game's quit control and the `SocketReplaced` handler (which, when the player's one allowed connection is taken over, stops the engines and acknowledges before returning to login via a client-side navigation — not a reload, which would bounce a still-authenticated client back through the loading screen).
- **Two sides of the single-connection model.** The `SocketReplaced` handler is the *losing* session's experience; the *winning* side is warned **before** taking over — on login, `confirmSessionTakeover()` calls the authenticated `Login/ActiveSession` endpoint (over HTTP, so it never opens a socket of its own) and confirms if the player already has a live connection elsewhere. It fails open (a non-200 proceeds rather than blocking a legitimate login) and runs only on explicit login, not the silent resume path.
- **A queue, not a single slot** — only the head is shown, but requests queue (each with its own resolve) so two callers each get an answer.
- **Dismissal semantics depend on kind** — backdrop-click/Escape are a cancel for `confirm`/`destructive` but an acknowledgement for `acknowledge`.
- **The host owns the interaction model so the card stays presentational** — backdrop/Escape dismissal, a focus trap, focus capture+restore, and a scroll lock; it focuses the safe action for a destructive dialog (so a stray Enter can't confirm) and the primary action otherwise. All motion is CSS so `prefers-reduced-motion` collapses it.

## Popover (non-blocking overlays)

**Non-blocking overlays compose `Popover` (`components/Popover.svelte`), not a hand-rolled backdrop.** It is the `ModalHost` counterpart for overlays that aren't a blocking confirm — driven by a plain `open` boolean + `onClose` rather than the promise queue — and owns the same chrome: backdrop/Escape dismissal, a focus trap, focus capture+restore, and a body scroll lock, with the content passed as a snippet. The layer is absolutely positioned, so it overlays its nearest positioned ancestor (mount it inside a `position: relative` container) rather than the whole viewport.
