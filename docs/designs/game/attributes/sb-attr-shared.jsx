/* sb-attr-shared.jsx — shared chrome + atoms for the Attribute Breakdown
   directions. Standalone (does not depend on the attr-page's attr-shared).   */

/* board frame: consistent dark shell + titled header per direction */
function SBoard({ kicker = 'Character · Attribute Breakdown', label, sub, accent, children, pad = 28, scroll = true }) {
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

/* mono label */
function Mono({ children, size = 9.5, ls = 1.4, color = 'rgba(240,240,240,0.5)', style }) {
  return <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: size, letterSpacing: ls,
    textTransform: 'uppercase', color, ...style }}>{children}</span>;
}

/* small square source swatch */
function Swatch({ color, size = 9, style }) {
  return <span style={{ width: size, height: size, background: color, borderRadius: 1, flexShrink: 0,
    boxShadow: `0 0 5px ${color}66`, display: 'inline-block', ...style }} />;
}

/* legend across all sources */
function SourceLegend({ only, style }) {
  const keys = only || SOURCE_ORDER;
  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 16px', alignItems: 'center', ...style }}>
      {keys.map((k) => {
        const s = SOURCES[k];
        return (
          <span key={k} style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
            <Swatch color={s.color} />
            <Mono size={9.5} ls={1.2} color="rgba(240,240,240,0.62)">{s.label}</Mono>
          </span>
        );
      })}
      <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
        <span style={{ width: 10, height: 9, borderRadius: 1, flexShrink: 0,
          background: 'repeating-linear-gradient(135deg, rgba(240,240,240,0.5) 0 2px, transparent 2px 4px)',
          border: '1px solid rgba(240,240,240,0.35)' }} />
        <Mono size={9.5} ls={1.2} color="rgba(240,240,240,0.62)">Multiplier</Mono>
      </span>
    </div>
  );
}

/* a "kind" tag pill for an attribute (Core / Derived / Unused / Obsolete) */
function KindTag({ meta }) {
  let label = meta.group === 'core' ? 'Core' : 'Derived';
  let color = meta.group === 'core' ? '#a1c2f7' : '#f0d28a';
  if (meta.obsolete) { label = 'Obsolete'; color = 'rgba(240,240,240,0.4)'; }
  else if (meta.unused) { label = 'Inactive'; color = 'rgba(240,240,240,0.5)'; }
  return (
    <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 8.5, letterSpacing: 1, textTransform: 'uppercase',
      color, border: `1px solid ${color}40`, borderRadius: 2, padding: '2px 6px', lineHeight: 1, whiteSpace: 'nowrap' }}>
      {label}
    </span>
  );
}

/* horizontal stacked bar from a computed attribute (additive segments by
   source + an optional striped multiplier cap). `scaleMax` sets full width. */
function StackBar({ computed, scaleMax, height = 14, showMult = true, radius = 2 }) {
  const { groups, mults } = groupBySource(computed);
  const total = Math.max(computed.total, computed.additiveSubtotal);
  const max = scaleMax || total || 1;
  return (
    <div style={{ display: 'flex', width: '100%', height, borderRadius: radius, overflow: 'hidden',
      background: 'rgba(255,255,255,0.05)', boxShadow: 'inset 0 0 0 1px rgba(255,255,255,0.06)' }}>
      {groups.map((g) => {
        const w = Math.max(0, (g.total / max) * 100);
        if (w <= 0) return null;
        return <div key={g.source} title={`${SOURCES[g.source].label}: ${fmtSigned(g.total, 1)}`}
          style={{ width: `${w}%`, background: SOURCES[g.source].color, opacity: 0.92,
            borderRight: '1px solid rgba(13,14,18,0.55)' }} />;
      })}
      {showMult && mults.map((m, i) => {
        const w = Math.max(0, (m.applied / max) * 100);
        if (w <= 0) return null;
        return <div key={'m' + i} title={`×${m.factor} → ${fmtSigned(m.applied, 1)}`}
          style={{ width: `${w}%`, borderRight: '1px solid rgba(13,14,18,0.55)',
            background: `repeating-linear-gradient(135deg, ${SOURCES.item.color}cc 0 3px, ${SOURCES.item.color}55 3px 6px)` }} />;
      })}
    </div>
  );
}

/* a single source→amount ledger row */
function LedgerRow({ ln, meta, showRunning = true, dim }) {
  const s = SOURCES[ln.source];
  const isMult = ln.multiplied;
  let label = ln.from;
  if (ln.source === 'derived') label = `${ATTR_BY_KEY[ln.from].name}`;
  if (ln.source === 'points') label = 'Allocated stat points';
  if (ln.source === 'base') label = 'Engine base value';
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '14px 1fr auto auto', alignItems: 'center', gap: 10,
      padding: '5px 0', opacity: dim ? 0.55 : 1 }}>
      <Swatch color={s.color} />
      <span style={{ display: 'flex', alignItems: 'baseline', gap: 8, minWidth: 0 }}>
        <span style={{ fontSize: 12.5, color: 'rgba(240,240,240,0.9)', whiteSpace: 'nowrap', overflow: 'hidden',
          textOverflow: 'ellipsis' }}>{label}</span>
        {ln.source === 'derived' && (
          <Mono size={9.5} ls={0.4} color="rgba(240,240,240,0.4)">{ln.amount}× of {fmtNum(ln.derivedValue, 1)}</Mono>
        )}
        {(ln.source === 'item' || ln.source === 'mod') && ln.slot && (
          <Mono size={9} ls={0.6} color={`${s.color}99`}>{ln.source === 'mod' ? ln.modType : ln.slot}</Mono>
        )}
      </span>
      <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12.5,
        color: isMult ? SOURCES.item.color : (ln.applied < 0 ? '#e8b6a6' : 'rgba(240,240,240,0.92)') }}>
        {isMult ? `×${ln.factor}` : fmtSigned(ln.applied, meta.dec || (Math.abs(ln.applied) < 10 && !Number.isInteger(ln.applied) ? 1 : 0))}
      </span>
      {showRunning
        ? <Mono size={11} ls={0.3} color="rgba(240,240,240,0.4)" style={{ textTransform: 'none', minWidth: 52, textAlign: 'right' }}>
            = {fmtNum(ln.running, meta.dec)}
          </Mono>
        : <span style={{ width: 0 }} />}
    </div>
  );
}

Object.assign(window, { SBoard, Mono, Swatch, SourceLegend, KindTag, StackBar, LedgerRow });
