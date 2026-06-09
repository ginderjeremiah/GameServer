/* Shared foundation for the Toast & Modal system exploration.
   Pulls real tokens from the game's _colors.scss / page-base so every
   variant sits in the existing visual language:
     dark #14151b surfaces · Geist + Geist Mono · rotated-diamond marks ·
     thin #ffffff24 borders · ~3px radii · accent-glow shadows.
   Exports: T (tokens), STATUS (per-type meta), StatusGlyph, Diamond,
            MonoLabel, GameBackdrop, and one-time keyframe injection. */

const T = {
  surface:       '#14151b',
  surfaceAlpha:  'rgba(20,21,27,0.97)',
  page:          '#0f1014',
  panel:         '#16171e',
  panel2:        '#1b1c24',
  accent:        '#a1c2f7',
  accentLight:   '#c0d8ff',
  enemy:         '#e08778',
  textPrimary:   '#f0f0f0',
  textSecondary: 'rgba(240,240,240,0.78)',
  textTertiary:  'rgba(240,240,240,0.55)',
  textMuted:     'rgba(240,240,240,0.40)',
  borderSubtle:  'rgba(255,255,255,0.08)',
  borderLight:   'rgba(255,255,255,0.14)',
  borderMedium:  'rgba(255,255,255,0.18)',
  success:       '#bde0b4',
  warning:       '#f0d28a',
  error:         '#f0a094',
  mono: '"Geist Mono", ui-monospace, monospace',
  sans: 'Geist, system-ui, sans-serif',
  radius: 3,
};

// Per-status accent + label + glyph kind. Mirrors the four ToastType values
// already in the svelte store (info / success / warning / error).
const STATUS = {
  info:    { color: T.accent,   label: 'Info',    glyph: 'info'  },
  success: { color: T.success,  label: 'Success', glyph: 'check' },
  warning: { color: T.warning,  label: 'Warning', glyph: 'warn'  },
  error:   { color: T.error,    label: 'Error',   glyph: 'cross' },
};

const STATUS_ORDER = ['info', 'success', 'warning', 'error'];

// Sample copy per type so every demo reads like real game telemetry.
const SAMPLE = {
  info:    'Surge is ready — Aelara can act this tick.',
  success: 'Forgotten Catacombs cleared. Zone 4 unlocked.',
  warning: 'Connection unstable — retrying battle sync…',
  error:   'Your attribute changes could not be saved.',
};

/* ── small chrome ─────────────────────────────────────────────────── */

// Rotated-square diamond mark — the game's universal accent glyph.
function Diamond({ size = 6, color = T.accent, glow = 0.7, style }) {
  return (
    <span style={{
      width: size, height: size, flexShrink: 0,
      background: color, transform: 'rotate(45deg)',
      boxShadow: glow ? `0 0 ${size}px ${color}${Math.round(glow * 255).toString(16).padStart(2, '0')}` : 'none',
      display: 'inline-block', ...style,
    }} />
  );
}

// Minimal status glyph (primitives only: diamond, check, triangle, x).
function StatusGlyph({ kind, color, size = 13 }) {
  const s = { width: size, height: size, display: 'block', fill: 'none', stroke: color, strokeWidth: 1.6, strokeLinecap: 'round', strokeLinejoin: 'round' };
  if (kind === 'check') return <svg {...s} viewBox="0 0 16 16"><path d="M3.5 8.5l3 3 6-6.5" /></svg>;
  if (kind === 'cross') return <svg {...s} viewBox="0 0 16 16"><path d="M4 4l8 8M12 4l-8 8" /></svg>;
  if (kind === 'warn')  return <svg {...s} viewBox="0 0 16 16"><path d="M8 2.5l5.5 10.5h-11z" /><path d="M8 6.5v3.2" /><path d="M8 11.4v.1" /></svg>;
  // info — circle + stem
  return <svg {...s} viewBox="0 0 16 16"><circle cx="8" cy="8" r="5.5" /><path d="M8 7.2v3.6" /><path d="M8 5.1v.1" /></svg>;
}

// Mono eyebrow label, uppercase, tracked — the recurring micro-label pattern.
function MonoLabel({ children, color = T.textMuted, size = 9.5, style }) {
  return (
    <span style={{
      fontFamily: T.mono, fontSize: size, letterSpacing: 1.7,
      textTransform: 'uppercase', color, ...style,
    }}>{children}</span>
  );
}

/* ── faux game backdrop (so modals read as overlays) ──────────────── */

function GameBackdrop({ children, dim = 0.55 }) {
  return (
    <div style={{
      position: 'absolute', inset: 0, overflow: 'hidden',
      background: 'linear-gradient(180deg, #2a2c33 0%, #0c0d11 100%)',
      fontFamily: T.sans,
    }}>
      {/* faint combat scene */}
      <div style={{ position: 'absolute', inset: 0, padding: 30, display: 'flex', flexDirection: 'column', gap: 18, opacity: 0.5 }}>
        <div style={{ alignSelf: 'center', display: 'flex', alignItems: 'center', gap: 10, padding: '6px 14px', border: `1px solid ${T.borderLight}`, borderRadius: 3, background: 'rgba(255,255,255,0.04)' }}>
          <MonoLabel color="rgba(192,216,255,0.7)">Zone 03</MonoLabel>
          <span style={{ fontSize: 14, color: T.textPrimary }}>Forgotten Catacombs</span>
        </div>
        <div style={{ flex: 1, display: 'flex', gap: 28, justifyContent: 'center', alignItems: 'center' }}>
          {[T.accent, T.enemy].map((c, i) => (
            <div key={i} style={{ width: 240, height: 150, borderRadius: 3, background: 'rgba(255,255,255,0.03)', border: `1px solid ${T.borderLight}`, [i ? 'borderRight' : 'borderLeft']: `3px solid ${c}` }} />
          ))}
        </div>
      </div>
      {/* dimming layer */}
      <div style={{ position: 'absolute', inset: 0, background: `radial-gradient(120% 90% at 50% 45%, rgba(8,9,12,${dim * 0.7}) 0%, rgba(8,9,12,${Math.min(dim + 0.25, 0.92)}) 100%)` }} />
      {children}
    </div>
  );
}

/* ── keyframes (one-time) ─────────────────────────────────────────── */

if (typeof document !== 'undefined' && !document.getElementById('tm-keyframes')) {
  const el = document.createElement('style');
  el.id = 'tm-keyframes';
  el.textContent = `
    @keyframes tm-toast-in-right { from { opacity:0; transform: translateX(24px) scale(.98); } to { opacity:1; transform:none; } }
    @keyframes tm-toast-in-top   { from { opacity:0; transform: translateY(-16px) scale(.98); } to { opacity:1; transform:none; } }
    @keyframes tm-toast-in-bottom{ from { opacity:0; transform: translateY(16px) scale(.98); } to { opacity:1; transform:none; } }
    @keyframes tm-toast-out      { from { opacity:1; transform:none; max-height:90px; } to { opacity:0; transform: translateX(24px); max-height:0; margin-top:0; } }
    @keyframes tm-backdrop-in    { from { opacity:0; } to { opacity:1; } }
    @keyframes tm-modal-in       { from { opacity:0; transform: translateY(10px) scale(.965); } to { opacity:1; transform:none; } }
    @keyframes tm-modal-in-bold  { from { opacity:0; transform: translateY(14px) scale(.95); filter:saturate(.4) brightness(.8); } to { opacity:1; transform:none; filter:none; } }
    @keyframes tm-glow-pulse     { 0%,100% { box-shadow: 0 0 0 1px var(--gc, rgba(161,194,247,.3)), 0 18px 50px rgba(0,0,0,.6), 0 0 28px -6px var(--gc); } 50% { box-shadow: 0 0 0 1px var(--gc), 0 18px 50px rgba(0,0,0,.6), 0 0 46px -2px var(--gc); } }
    @keyframes tm-diamond-pulse  { 0%,100% { box-shadow: 0 0 6px var(--gc); } 50% { box-shadow: 0 0 16px var(--gc); } }
    @keyframes tm-timer          { from { transform: scaleX(1); } to { transform: scaleX(0); } }
  `;
  document.head.appendChild(el);
}

Object.assign(window, {
  T, STATUS, STATUS_ORDER, SAMPLE,
  Diamond, StatusGlyph, MonoLabel, GameBackdrop,
});
