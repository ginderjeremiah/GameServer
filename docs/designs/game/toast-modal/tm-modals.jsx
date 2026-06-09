/* Modal directions — safe → bold. <ModalCard> renders one dialog for a given
   content kind; <ModalScene> drops it (animated) over a dimmed game backdrop
   for the canvas + playground.

   A · Sheet       — quiet centered card. Mono eyebrow, title, body, buttons.
   B · Framed      — diamond header, accent rail + divider rules, two buttons.
   C · Atmospheric — heightened: emblem, glow-pulse border, vignette. Rewards/boss.

   kind: 'confirm' (two actions) · 'acknowledge' (single) · 'destructive' (danger). */

const MODAL_DIRECTIONS = [
  { id: 'sheet',       name: 'A · Sheet',       blurb: 'Quiet centered card' },
  { id: 'framed',      name: 'B · Framed',      blurb: 'Diamond header, accent rail' },
  { id: 'atmospheric', name: 'C · Atmospheric', blurb: 'Emblem, glow, vignette' },
];

const MODAL_COPY = {
  confirm:     { eyebrow: 'Confirm',  title: 'Engage the zone boss?',        body: 'The Catacomb Warden hits harder than the field. You can retreat any time, but unspent charges are lost on entry.', primary: 'Engage', secondary: 'Not yet', tone: 'accent' },
  acknowledge: { eyebrow: 'Reward',   title: 'Zone 03 cleared',              body: 'The Forgotten Catacombs are yours. Zone 04 — the Sunken Vault — is now open, and the Warden stays re-challengeable for farming.', primary: 'Continue', secondary: null, tone: 'accent' },
  destructive: { eyebrow: 'Warning',  title: 'Reset attribute build?',       body: 'This refunds every allocated point and clears your current loadout bonuses. This action can\u2019t be undone.', primary: 'Reset build', secondary: 'Keep build', tone: 'danger' },
};

function ModalBtn({ children, tone = 'ghost', onClick }) {
  const map = {
    accent:  { bg: 'color-mix(in srgb, #a1c2f7 16%, transparent)', bd: '#a1c2f7', fg: '#c0d8ff', hover: 'color-mix(in srgb, #a1c2f7 26%, transparent)' },
    danger:  { bg: 'color-mix(in srgb, #f0a094 15%, transparent)', bd: 'color-mix(in srgb, #f0a094 70%, transparent)', fg: '#f0a094', hover: 'color-mix(in srgb, #f0a094 24%, transparent)' },
    ghost:   { bg: 'transparent', bd: T.borderLight, fg: T.textSecondary, hover: 'rgba(255,255,255,0.05)' },
  };
  const c = map[tone];
  const [h, setH] = React.useState(false);
  return (
    <button onClick={onClick} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)} style={{
      flex: tone === 'ghost' ? '0 0 auto' : '0 0 auto',
      padding: '8px 18px', borderRadius: 2, cursor: 'pointer',
      background: h ? c.hover : c.bg, border: `1px solid ${c.bd}`, color: c.fg,
      fontFamily: T.sans, fontSize: 12.5, fontWeight: 500, letterSpacing: 0.2,
      transition: 'background .14s',
    }}>{children}</button>
  );
}

function ModalCard({ direction, kind = 'confirm', animateIn = true, onClose }) {
  const cp = MODAL_COPY[kind];
  const accent = cp.tone === 'danger' ? T.error : T.accent;
  const close = onClose || (() => {});
  // Transition-based reveal (rests visible where keyframe motion is suppressed).
  const [shown, setShown] = React.useState(!animateIn);
  React.useEffect(() => {
    if (!animateIn) return;
    const id = requestAnimationFrame(() => setShown(true));
    return () => cancelAnimationFrame(id);
  }, [animateIn]);
  const reveal = {
    transition: 'opacity .32s cubic-bezier(.22,.61,.36,1), transform .32s cubic-bezier(.22,.61,.36,1)',
    opacity: shown ? 1 : 0,
    transform: shown ? 'none' : 'translateY(10px) scale(.965)',
  };
  const buttons = (
    <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginTop: 18 }}>
      {cp.secondary && <ModalBtn tone="ghost" onClick={close}>{cp.secondary}</ModalBtn>}
      <ModalBtn tone={cp.tone === 'danger' ? 'danger' : 'accent'} onClick={close}>{cp.primary}</ModalBtn>
    </div>
  );

  /* ── A · Sheet ── */
  if (direction === 'sheet') {
    return (
      <div role="dialog" aria-modal="true" style={{
        width: 380, padding: '22px 24px 20px', fontFamily: T.sans,
        background: T.surfaceAlpha, border: `1px solid ${T.borderLight}`,
        borderRadius: T.radius, boxShadow: '0 24px 60px rgba(0,0,0,0.6)', ...reveal,
      }}>
        <MonoLabel color={accent}>{cp.eyebrow}</MonoLabel>
        <div style={{ marginTop: 9, fontSize: 19, fontWeight: 500, color: T.textPrimary, letterSpacing: -0.2 }}>{cp.title}</div>
        <div style={{ marginTop: 10, fontSize: 13, lineHeight: 1.55, color: T.textSecondary }}>{cp.body}</div>
        {kind === 'acknowledge'
          ? <div style={{ marginTop: 18, display: 'flex', justifyContent: 'flex-end' }}><ModalBtn tone="accent" onClick={close}>{cp.primary}</ModalBtn></div>
          : buttons}
      </div>
    );
  }

  /* ── B · Framed ── */
  if (direction === 'framed') {
    return (
      <div role="dialog" aria-modal="true" style={{
        width: 392, fontFamily: T.sans, overflow: 'hidden',
        background: T.surfaceAlpha, border: `1px solid ${T.borderLight}`,
        borderLeft: `3px solid ${accent}`, borderRadius: T.radius,
        boxShadow: '0 24px 60px rgba(0,0,0,0.62)', ...reveal,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '15px 20px 13px', borderBottom: `1px solid ${T.borderSubtle}` }}>
          <Diamond size={6} color={accent} />
          <MonoLabel color={accent}>{cp.eyebrow}</MonoLabel>
          <div style={{ flex: 1, height: 1, background: T.borderSubtle }} />
          <button onClick={close} aria-label="Close" style={{ background: 'none', border: 'none', color: T.textTertiary, fontSize: 16, lineHeight: 1, cursor: 'pointer', padding: 0 }}>×</button>
        </div>
        <div style={{ padding: '16px 20px 20px' }}>
          <div style={{ fontSize: 19, fontWeight: 500, color: T.textPrimary, letterSpacing: -0.2 }}>{cp.title}</div>
          <div style={{ marginTop: 10, fontSize: 13, lineHeight: 1.55, color: T.textSecondary }}>{cp.body}</div>
          {kind === 'acknowledge'
            ? <div style={{ marginTop: 18, display: 'flex', justifyContent: 'flex-end' }}><ModalBtn tone="accent" onClick={close}>{cp.primary}</ModalBtn></div>
            : buttons}
        </div>
      </div>
    );
  }

  /* ── C · Atmospheric ── */
  return (
    <div role="dialog" aria-modal="true" style={{
      width: 400, fontFamily: T.sans, position: 'relative', overflow: 'hidden',
      background: `linear-gradient(180deg, color-mix(in srgb, ${accent} 7%, ${T.surface}) 0%, ${T.surfaceAlpha} 60%)`,
      border: `1px solid color-mix(in srgb, ${accent} 28%, transparent)`,
      borderRadius: 4,
      boxShadow: `0 20px 50px rgba(0,0,0,0.6), 0 0 18px -10px ${accent}`,
      ...reveal,
    }}>
      <div style={{ position: 'absolute', inset: 0, pointerEvents: 'none', background: `radial-gradient(120% 70% at 50% -10%, color-mix(in srgb, ${accent} 9%, transparent), transparent 62%)` }} />
      <div style={{ position: 'relative', padding: '24px 26px 22px', textAlign: 'center' }}>
        <div style={{ fontSize: 22, fontWeight: 500, color: T.textPrimary, letterSpacing: -0.4 }}>{cp.title}</div>
        <div style={{ marginTop: 11, fontSize: 13, lineHeight: 1.6, color: T.textSecondary, maxWidth: 320, marginLeft: 'auto', marginRight: 'auto' }}>{cp.body}</div>
        <div style={{ marginTop: 20, display: 'flex', gap: 10, justifyContent: 'center' }}>
          {cp.secondary && <ModalBtn tone="ghost" onClick={close}>{cp.secondary}</ModalBtn>}
          <ModalBtn tone={cp.tone === 'danger' ? 'danger' : 'accent'} onClick={close}>{cp.primary}</ModalBtn>
        </div>
      </div>
    </div>
  );
}

// Canvas / playground: modal centered over the dimmed game.
function ModalScene({ direction, kind = 'confirm', animateIn = true, dim = 0.6, onClose }) {
  return (
    <div style={{ width: '100%', height: '100%', position: 'relative', overflow: 'hidden' }}>
      <GameBackdrop dim={dim} />
      <div style={{
        position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24,
        animation: animateIn ? 'tm-backdrop-in .26s ease both' : 'none',
      }}>
        <ModalCard direction={direction} kind={kind} animateIn={animateIn} onClose={onClose} />
      </div>
    </div>
  );
}

Object.assign(window, { MODAL_DIRECTIONS, MODAL_COPY, ModalCard, ModalScene, ModalBtn });
