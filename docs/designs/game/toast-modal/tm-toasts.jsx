/* Toast directions — safe → bold. Each renders a single toast for a given
   status type; <ToastStack> shows all four for the canvas. The same
   <ToastCard> is reused live in the playground (motion + anchor).

   A · Standard    — today's toast, tightened. Accent left-rule, mono type tag.
   B · Iconed      — status tile + glyph, two-line, auto-dismiss timer bar.
   C · Atmospheric — game-flavored: pulsing diamond, accent glow + tint, action. */

const TOAST_DIRECTIONS = [
  { id: 'standard',    name: 'A · Standard',    blurb: 'Today\u2019s toast, tightened' },
  { id: 'iconed',      name: 'B · Iconed',      blurb: 'Status tile + timer bar' },
  { id: 'atmospheric', name: 'C · Atmospheric', blurb: 'Diamond, glow, inline action' },
];

// Hidden-state offset for the entrance/exit transition, based on the anchor.
function enterOffset(anchor) {
  if (anchor && anchor.endsWith('center')) return anchor.startsWith('bottom') ? 'translateY(16px)' : 'translateY(-16px)';
  return 'translateX(22px)';
}

function ToastCard({ direction, type, animateIn = true, anchor = 'top-right', dismissing = false, showTimer = true, onDismiss }) {
  const st = STATUS[type];
  const msg = SAMPLE[type];
  // Transition-based reveal: rests visible even where keyframe motion is
  // suppressed; flips from the hidden offset to resting on the next frame.
  const [shown, setShown] = React.useState(!animateIn);
  React.useEffect(() => {
    if (!animateIn) return;
    const id = requestAnimationFrame(() => setShown(true));
    return () => cancelAnimationFrame(id);
  }, [animateIn]);
  const off = enterOffset(anchor);
  const wrap = {
    pointerEvents: 'auto',
    transition: 'opacity .3s cubic-bezier(.22,.61,.36,1), transform .3s cubic-bezier(.22,.61,.36,1)',
    opacity: dismissing ? 0 : (shown ? 1 : 0),
    transform: dismissing ? off : (shown ? 'none' : off),
  };
  const dismissBtn = (extra) => onDismiss && (
    <button onClick={onDismiss} aria-label="Dismiss notification" style={{
      flexShrink: 0, background: 'none', border: 'none', color: T.textTertiary,
      fontSize: 15, lineHeight: 1, cursor: 'pointer', padding: '0 2px', ...extra,
    }}>×</button>
  );
  const timer = showTimer && (
    <div style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: 2, background: 'rgba(255,255,255,0.06)', overflow: 'hidden' }}>
      <div style={{ height: '100%', background: st.color, transformOrigin: 'left', animation: 'tm-timer 6s linear forwards', opacity: 0.7 }} />
    </div>
  );

  /* ── A · Standard ── */
  if (direction === 'standard') {
    return (
      <div role="alert" style={{
        ...wrap, position: 'relative', display: 'flex', alignItems: 'flex-start', gap: 10,
        width: 320, padding: '11px 13px 12px', background: T.surfaceAlpha,
        border: `1px solid ${T.borderLight}`, borderLeft: `3px solid ${st.color}`,
        borderRadius: T.radius, boxShadow: '0 4px 14px rgba(0,0,0,0.45)',
        overflow: 'hidden', fontFamily: T.sans,
      }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <MonoLabel color={st.color} size={9}>{st.label}</MonoLabel>
          <div style={{ marginTop: 4, fontSize: 13, lineHeight: 1.4, color: T.textPrimary }}>{msg}</div>
        </div>
        {dismissBtn()}
        {timer}
      </div>
    );
  }

  /* ── B · Iconed ── */
  if (direction === 'iconed') {
    return (
      <div role="alert" style={{
        ...wrap, position: 'relative', display: 'flex', alignItems: 'flex-start', gap: 11,
        width: 332, padding: '12px 13px 13px', background: T.surfaceAlpha,
        border: `1px solid ${T.borderLight}`, borderRadius: T.radius,
        boxShadow: '0 6px 18px rgba(0,0,0,0.5)', overflow: 'hidden', fontFamily: T.sans,
      }}>
        <div style={{
          flexShrink: 0, width: 30, height: 30, borderRadius: 2,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          background: `color-mix(in srgb, ${st.color} 12%, transparent)`,
          border: `1px solid color-mix(in srgb, ${st.color} 45%, transparent)`,
          boxShadow: `inset 0 0 10px color-mix(in srgb, ${st.color} 18%, transparent)`,
        }}>
          <StatusGlyph kind={st.glyph} color={st.color} size={15} />
        </div>
        <div style={{ flex: 1, minWidth: 0, paddingTop: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <MonoLabel color={st.color} size={9}>{st.label}</MonoLabel>
            <div style={{ flex: 1, height: 1, background: T.borderSubtle }} />
          </div>
          <div style={{ marginTop: 5, fontSize: 13, lineHeight: 1.4, color: T.textPrimary }}>{msg}</div>
        </div>
        {dismissBtn({ marginTop: -2 })}
        {timer}
      </div>
    );
  }

  /* ── C · Atmospheric ── */
  const action = type === 'error' ? 'Retry' : type === 'success' ? 'View' : type === 'warning' ? 'Details' : 'Dismiss';
  return (
    <div role="alert" style={{
      ...wrap, position: 'relative', width: 340, padding: '12px 14px 13px', overflow: 'hidden', fontFamily: T.sans,
      background: `linear-gradient(180deg, color-mix(in srgb, ${st.color} 6%, ${T.surface}) 0%, ${T.surfaceAlpha} 65%)`,
      border: `1px solid color-mix(in srgb, ${st.color} 26%, transparent)`,
      borderRadius: T.radius,
      boxShadow: `0 6px 18px rgba(0,0,0,0.5), 0 0 14px -10px ${st.color}, inset 0 1px 0 rgba(255,255,255,0.03)`,
    }}>
      {/* faint scanline atmosphere */}
      <div style={{ position: 'absolute', inset: 0, pointerEvents: 'none', background: `repeating-linear-gradient(180deg, transparent 0 3px, rgba(255,255,255,0.01) 3px 4px)` }} />
      <div style={{ position: 'relative' }}>
        {/* header — glyph + status label on one baseline */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <StatusGlyph kind={st.glyph} color={st.color} size={14} />
          <MonoLabel color={st.color} size={9}>{st.label}</MonoLabel>
          <div style={{ flex: 1 }} />
          {dismissBtn()}
        </div>
        <div style={{ marginTop: 7, fontSize: 13, lineHeight: 1.45, color: T.textPrimary }}>{msg}</div>
        <div style={{ marginTop: 9 }}>
          <button style={{
            background: 'none', border: 'none', padding: 0, cursor: 'pointer',
            fontFamily: T.mono, fontSize: 10, letterSpacing: 1.2, textTransform: 'uppercase',
            color: st.color,
          }}>{action} →</button>
        </div>
      </div>
    </div>
  );
}

// Canvas: a stack of all four status toasts for one direction.
function ToastStack({ direction }) {
  return (
    <div style={{ width: '100%', height: '100%', position: 'relative', background: 'linear-gradient(180deg, #26282f 0%, #0e0f13 100%)', overflow: 'hidden' }}>
      {/* faint backdrop texture so surfaces read as floating */}
      <div style={{ position: 'absolute', inset: 0, opacity: 0.5, background: 'radial-gradient(100% 80% at 50% 0%, rgba(255,255,255,0.04), transparent 70%)' }} />
      <div style={{ position: 'relative', height: '100%', display: 'flex', flexDirection: 'column', gap: 11, alignItems: 'center', justifyContent: 'center', padding: 24 }}>
        {STATUS_ORDER.map((type) => (
          <ToastCard key={type} direction={direction} type={type} anchor="top-right"
            animateIn={false} showTimer={type === 'warning'} onDismiss={() => {}} />
        ))}
      </div>
    </div>
  );
}

Object.assign(window, { TOAST_DIRECTIONS, ToastCard, ToastStack, enterOffset });
