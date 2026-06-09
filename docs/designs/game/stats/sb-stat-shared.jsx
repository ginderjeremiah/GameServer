/* sb-stat-shared.jsx — shared chrome + atoms for the Statistics directions.
   Self-contained (own copy of the small atoms so the page needs no other
   shared file). */

/* ── frame ───────────────────────────────────────────────────────────── */
function StatBoard({ kicker = 'Character · Statistics', label, sub, accent, children, pad = 28, scroll = true }) {
  return (
    <div style={{ width: '100%', height: '100%', background: 'linear-gradient(160deg, #16171e 0%, #0d0e12 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif', color: '#f0f0f0', display: 'flex', flexDirection: 'column',
      overflow: 'hidden' }}>
      <div style={{ padding: `20px ${pad}px 0`, flexShrink: 0 }}>
        <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 2, textTransform: 'uppercase',
          color: `${accent}b3`, marginBottom: 6 }}>{kicker}</div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 12 }}>
          <h1 style={{ margin: 0, fontSize: 23, fontWeight: 500, letterSpacing: -0.3 }}>{label}</h1>
          {sub && <span style={{ fontSize: 12.5, color: 'rgba(240,240,240,0.45)' }}>{sub}</span>}
        </div>
      </div>
      <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', padding: pad, paddingTop: 16,
        overflow: scroll ? 'auto' : 'hidden' }}>
        {children}
      </div>
    </div>
  );
}

function SMono({ children, size = 9.5, ls = 1.4, color = 'rgba(240,240,240,0.5)', style }) {
  return <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: size, letterSpacing: ls,
    textTransform: 'uppercase', color, ...style }}>{children}</span>;
}

/* ── formatting ──────────────────────────────────────────────────────── */
function fmtCount(n) { return Math.round(n).toLocaleString('en-US'); }
function fmtDamage(n) { return Math.round(n).toLocaleString('en-US'); }
function fmtTime(ms) {
  const s = ms / 1000;
  if (s < 60) return `${(s).toFixed(s < 10 ? 1 : 0)}s`;
  const m = Math.floor(s / 60), rs = Math.round(s % 60);
  if (m < 60) return `${m}m ${String(rs).padStart(2, '0')}s`;
  const h = Math.floor(m / 60), rm = m % 60;
  return `${h}h ${String(rm).padStart(2, '0')}m`;
}
function fmtValue(v, unit) {
  return unit === 'time' ? fmtTime(v) : unit === 'damage' ? fmtDamage(v) : fmtCount(v);
}
function unitLabel(unit) { return unit === 'time' ? 'time' : unit === 'damage' ? 'dmg' : 'count'; }

/* ── mini bar (one fill segment) ─────────────────────────────────────── */
function MiniBar({ frac, color, height = 6, radius = 2, track = 'rgba(255,255,255,0.06)' }) {
  return (
    <div style={{ width: '100%', height, background: track, borderRadius: radius, overflow: 'hidden' }}>
      <div style={{ height: '100%', width: `${Math.max(0, Math.min(1, frac)) * 100}%`, background: color,
        borderRadius: radius, boxShadow: `0 0 6px ${color}55`, transition: 'width 200ms cubic-bezier(.4,0,.2,1)' }} />
    </div>
  );
}

/* ── entity glyph — simple geometric stand-in per entity kind ────────── */
function EntityGlyph({ kind, size = 16, color }) {
  const c = color || ENTITY_KIND[kind].color;
  const s = { width: size, height: size, display: 'block' };
  if (kind === 'enemy') // diamond skull stand-in
    return <svg style={s} viewBox="0 0 16 16" fill="none" stroke={c} strokeWidth="1.3"><path d="M8 1.5l6 6.5-6 6.5-6-6.5z" strokeLinejoin="round" /><circle cx="6" cy="7.5" r="1" fill={c} stroke="none" /><circle cx="10" cy="7.5" r="1" fill={c} stroke="none" /></svg>;
  if (kind === 'zone') // hex tile
    return <svg style={s} viewBox="0 0 16 16" fill="none" stroke={c} strokeWidth="1.3"><path d="M8 1.5l5.5 3.2v6.6L8 14.5 2.5 11.3V4.7z" strokeLinejoin="round" /></svg>;
  // skill — rune mark
  return <svg style={s} viewBox="0 0 16 16" fill="none" stroke={c} strokeWidth="1.3"><path d="M8 2v12M4 5l8 6M12 5l-8 6" strokeLinecap="round" /></svg>;
}

/* ── underline tab bar (shared by Category Tabs + Entity Pivot) ───────── */
/* tabs: [{ key, label, count?, color, glyph?(on) }]  — glyph is an optional
   render fn that receives the active flag so it can recolor when selected. */
function UnderlineTabs({ tabs, active, onChange, style }) {
  return (
    <div style={{ display: 'flex', gap: 6, borderBottom: '1px solid rgba(255,255,255,0.08)', ...style }}>
      {tabs.map((t) => {
        const on = t.key === active;
        return (
          <button key={t.key} onClick={() => onChange(t.key)}
            style={{ position: 'relative', background: 'transparent', border: 'none', cursor: 'pointer',
              padding: '4px 14px 12px', display: 'flex', alignItems: 'center', gap: 8 }}>
            {t.glyph && t.glyph(on)}
            <span style={{ fontFamily: 'Geist, sans-serif', fontSize: 13.5, fontWeight: on ? 600 : 400,
              color: on ? '#fff' : 'rgba(240,240,240,0.55)', transition: 'color 120ms' }}>{t.label}</span>
            {t.count != null && (
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, color: on ? t.color : 'rgba(240,240,240,0.35)',
                border: `1px solid ${on ? t.color + '55' : 'rgba(255,255,255,0.12)'}`, borderRadius: 8, padding: '0 6px', lineHeight: '15px' }}>{t.count}</span>
            )}
            {on && <span style={{ position: 'absolute', left: 0, right: 0, bottom: -1, height: 2, background: t.color, borderRadius: 2 }} />}
          </button>
        );
      })}
    </div>
  );
}

/* ── category tag ────────────────────────────────────────────────────── */
function CategoryTag({ cat, active, onClick }) {
  const meta = STAT_CATEGORIES.find((c) => c.key === cat);
  return (
    <span onClick={onClick} style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1,
      textTransform: 'uppercase', color: meta.color, border: `1px solid ${meta.color}40`, borderRadius: 2,
      padding: '2px 6px', lineHeight: 1, whiteSpace: 'nowrap', cursor: onClick ? 'pointer' : 'default' }}>
      {meta.label}
    </span>
  );
}

/* ── entity kind badge ───────────────────────────────────────────────── */
function KindBadge({ kind }) {
  const meta = ENTITY_KIND[kind];
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
      <EntityGlyph kind={kind} size={11} />
      <SMono size={9} ls={1} color={`${meta.color}cc`}>{meta.label}</SMono>
    </span>
  );
}

/* ── comparison hint (lower-is-better stats) ─────────────────────────── */
function CompHint({ st }) {
  const lower = st.comp === 'AtMost';
  return <SMono size={8.5} ls={0.8} color="rgba(240,240,240,0.34)" style={{ textTransform: 'none' }}>
    {lower ? 'lower is better' : (st.agg === 'max' ? 'peak' : st.agg === 'min' ? 'best' : 'total')}
  </SMono>;
}

Object.assign(window, {
  StatBoard, SMono, fmtCount, fmtDamage, fmtTime, fmtValue, unitLabel,
  MiniBar, EntityGlyph, UnderlineTabs, CategoryTag, KindBadge, CompHint,
});
