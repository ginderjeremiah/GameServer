/* sb-attr-grouped.jsx — Direction C · "Grouped + Expand"
   Best of both depths. Each attribute is a row showing its total, a compact
   stacked bar, and one chip per contributing source with that source's
   subtotal. Click a row to expand: the individual items / mods / derived
   terms that make up each source group are revealed. Calm by default,
   exhaustive on demand. */

function AttrGrouped({ accent = '#a1c2f7', showInactive = true }) {
  const computed = React.useMemo(() => computeAttributes(), []);
  const [open, setOpen] = React.useState(() => new Set([A.HP, A.DEF]));
  const attrs = ATTR_META.filter((a) => showInactive || (!a.unused && !a.obsolete));
  const globalMax = Math.max(...attrs.map((a) => computed[a.key].total));

  const toggle = (k) => setOpen((s) => { const n = new Set(s); n.has(k) ? n.delete(k) : n.add(k); return n; });

  return (
    <SBoard label="By source" sub="grouped subtotals — click any row to itemize" accent={accent}>
      <SourceLegend style={{ marginBottom: 16 }} />
      <div style={{ display: 'flex', flexDirection: 'column' }}>
        {attrs.map((a, i) => (
          <GroupRow key={a.key} meta={a} computed={computed[a.key]} globalMax={globalMax}
            open={open.has(a.key)} onToggle={() => toggle(a.key)} first={i === 0} accent={accent} />
        ))}
      </div>
    </SBoard>
  );
}

function GroupRow({ meta, computed, globalMax, open, onToggle, first, accent }) {
  const { groups, mults } = groupBySource(computed);
  const dim = meta.unused || meta.obsolete;
  const [hover, setHover] = React.useState(false);

  return (
    <div style={{ borderTop: first ? 'none' : '1px solid rgba(255,255,255,0.06)' }}>
      {/* summary row */}
      <div onClick={onToggle} onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
        style={{ display: 'grid', gridTemplateColumns: '16px 150px 1fr 230px 84px', alignItems: 'center', gap: 14,
          padding: '11px 6px', cursor: 'pointer', borderRadius: 4,
          background: hover ? 'rgba(255,255,255,0.025)' : 'transparent', opacity: dim ? 0.66 : 1 }}>
        {/* caret */}
        <svg width="11" height="11" viewBox="0 0 12 12" fill="none" stroke="rgba(240,240,240,0.5)" strokeWidth="1.6"
          style={{ transform: open ? 'rotate(90deg)' : 'none', transition: 'transform 150ms' }}>
          <path d="M4 2.5L8 6l-4 3.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {/* name */}
        <div style={{ minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
            <span style={{ fontSize: 14, fontWeight: 500, color: '#f0f0f0', whiteSpace: 'nowrap' }}>{meta.name}</span>
            <KindTag meta={meta} />
          </div>
        </div>
        {/* bar */}
        <StackBar computed={computed} scaleMax={globalMax} height={13} />
        {/* source chips */}
        <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
          {groups.map((g) => (
            <span key={g.source} style={{ display: 'inline-flex', alignItems: 'center', gap: 4,
              border: `1px solid ${SOURCES[g.source].color}30`, borderRadius: 2, padding: '2px 6px' }}>
              <Swatch color={SOURCES[g.source].color} size={7} />
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, color: 'rgba(240,240,240,0.78)' }}>
                {fmtSigned(g.total, Math.abs(g.total) < 10 && !Number.isInteger(g.total) ? 1 : 0)}
              </span>
            </span>
          ))}
          {mults.map((m, i) => (
            <span key={i} style={{ display: 'inline-flex', alignItems: 'center', gap: 4,
              border: `1px solid ${SOURCES.item.color}45`, borderRadius: 2, padding: '2px 6px',
              background: `repeating-linear-gradient(135deg, ${SOURCES.item.color}22 0 3px, transparent 3px 6px)` }}>
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, color: SOURCES.item.color }}>×{m.factor}</span>
            </span>
          ))}
        </div>
        {/* total */}
        <div style={{ textAlign: 'right' }}>
          <span style={{ fontFamily: 'Geist, sans-serif', fontSize: 17, fontWeight: 600, letterSpacing: -0.3, color: '#f0f0f0' }}>
            {fmtNum(computed.total, meta.dec)}</span>
          {meta.unit && <span style={{ fontSize: 10, color: 'rgba(240,240,240,0.4)' }}>{meta.unit === '×' ? '×' : meta.unit}</span>}
        </div>
      </div>

      {/* expanded itemization */}
      {open && (
        <div style={{ padding: '2px 6px 14px 30px', display: 'flex', flexDirection: 'column', gap: 10 }}>
          {computed.lines.length === 0 && (
            <Mono size={10} ls={0.5} color="rgba(240,240,240,0.35)" style={{ textTransform: 'none' }}>
              No contributors — not currently produced by any source.
            </Mono>
          )}
          {groups.map((g) => (
            <div key={g.source}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 2 }}>
                <Swatch color={SOURCES[g.source].color} size={8} />
                <Mono size={9} ls={1.2} color={SOURCES[g.source].color}>{SOURCES[g.source].label}</Mono>
                <span style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.05)' }} />
                <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: 'rgba(240,240,240,0.6)' }}>{fmtSigned(g.total, 1)}</span>
              </div>
              {g.lines.map((ln, i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 10, padding: '3px 0 3px 16px' }}>
                  <span style={{ display: 'flex', alignItems: 'baseline', gap: 8, minWidth: 0 }}>
                    <span style={{ fontSize: 12, color: 'rgba(240,240,240,0.82)' }}>
                      {ln.source === 'derived' ? ATTR_BY_KEY[ln.from].name
                        : ln.source === 'points' ? 'Allocated stat points'
                        : ln.source === 'base' ? 'Engine base value' : ln.from}
                    </span>
                    {ln.source === 'derived' && <Mono size={9} ls={0.3} color="rgba(240,240,240,0.4)">{ln.amount}× of {fmtNum(ln.derivedValue, 1)}</Mono>}
                    {ln.source === 'mod' && <Mono size={9} ls={0.5} color={`${SOURCES.mod.color}99`}>{ln.modType} · {ln.host}</Mono>}
                    {ln.source === 'item' && ln.slot && <Mono size={9} ls={0.5} color={`${SOURCES.item.color}99`}>{ln.slot}</Mono>}
                  </span>
                  <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12,
                    color: ln.applied < 0 ? '#e8b6a6' : 'rgba(240,240,240,0.9)' }}>{fmtSigned(ln.applied, Math.abs(ln.applied) < 10 && !Number.isInteger(ln.applied) ? 1 : 0)}</span>
                </div>
              ))}
            </div>
          ))}
          {mults.map((ln, i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ width: 8, height: 8, borderRadius: 1,
                background: `repeating-linear-gradient(135deg, ${SOURCES.item.color}cc 0 2px, ${SOURCES.item.color}44 2px 4px)` }} />
              <Mono size={9} ls={1.2} color={SOURCES.item.color}>Multiplier · {ln.from}</Mono>
              <span style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.05)' }} />
              <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: SOURCES.item.color }}>×{ln.factor} → {fmtSigned(ln.applied, 1)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

Object.assign(window, { AttrGrouped });
