/* sb-attr-inspector.jsx — Direction D · "Inspector"
   A focused deep-dive. Left: a compact list of every attribute (mini stacked
   bar + total) you can select. Right: the selected attribute blown up — the
   big number, a full-width stacked bar, the source breakdown, and a literal
   "apply order" trace showing additive accumulation then the multiplicative
   step, the way the engine resolves it. */

function AttrInspector({ accent = '#a1c2f7', showInactive = true, label = 'Inspector', sub = 'select an attribute to trace its resolution', kicker }) {
  const computed = React.useMemo(() => computeAttributes(), []);
  const attrs = ATTR_META.filter((a) => showInactive || (!a.unused && !a.obsolete));
  const [sel, setSel] = React.useState(A.HP);
  const meta = ATTR_BY_KEY[sel];
  const c = computed[sel];
  const { groups, mults } = groupBySource(c);

  return (
    <SBoard label={label} sub={sub} kicker={kicker} accent={accent} scroll={false}>
      <div style={{ flex: 1, minHeight: 0, display: 'grid', gridTemplateColumns: '264px 1fr', gap: 22 }}>
        {/* left list */}
        <div style={{ overflow: 'auto', paddingRight: 4, borderRight: '1px solid rgba(255,255,255,0.06)' }}>
          {['core', 'derived'].map((grp) => (
            <div key={grp} style={{ marginBottom: 12 }}>
              <Mono style={{ display: 'block', margin: '4px 0 6px' }}>{grp === 'core' ? 'Core' : 'Derived'}</Mono>
              {attrs.filter((a) => a.group === grp).map((a) => {
                const on = a.key === sel;
                const dim = a.unused || a.obsolete;
                return (
                  <button key={a.key} onClick={() => setSel(a.key)}
                    style={{ width: '100%', textAlign: 'left', display: 'grid', gridTemplateColumns: '1fr 54px',
                      alignItems: 'center', gap: 8, padding: '7px 9px', marginBottom: 2, borderRadius: 4, cursor: 'pointer',
                      background: on ? `${accent}1a` : 'transparent', border: `1px solid ${on ? accent + '66' : 'transparent'}`,
                      transition: 'background 120ms', opacity: dim && !on ? 0.6 : 1 }}>
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontSize: 12.5, color: on ? '#fff' : 'rgba(240,240,240,0.85)', marginBottom: 4, whiteSpace: 'nowrap' }}>{a.name}</div>
                      <StackBar computed={computed[a.key]} height={6} radius={1} />
                    </div>
                    <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12, textAlign: 'right',
                      color: on ? '#fff' : 'rgba(240,240,240,0.6)' }}>{fmtNum(computed[a.key].total, a.dec)}</span>
                  </button>
                );
              })}
            </div>
          ))}
        </div>

        {/* right detail */}
        <div style={{ overflow: 'auto', paddingRight: 6 }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, marginBottom: 6 }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 9, marginBottom: 6 }}>
                <h2 style={{ margin: 0, fontSize: 24, fontWeight: 500, letterSpacing: -0.4 }}>{meta.name}</h2>
                <KindTag meta={meta} />
              </div>
              <p style={{ margin: 0, fontSize: 12.5, lineHeight: 1.5, color: 'rgba(240,240,240,0.55)', maxWidth: 460 }}>{meta.desc}</p>
            </div>
            <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
              <span style={{ fontFamily: 'Geist, sans-serif', fontSize: 40, fontWeight: 600, letterSpacing: -1.2, color: '#fff',
                textShadow: `0 0 22px ${accent}33` }}>{fmtNum(c.total, meta.dec)}</span>
              <span style={{ fontSize: 16, color: 'rgba(240,240,240,0.4)', marginLeft: 2 }}>{meta.unit === '×' ? '×' : meta.unit}</span>
            </div>
          </div>

          <div style={{ margin: '14px 0 6px' }}><StackBar computed={c} height={18} /></div>
          <SourceLegend only={groups.map((g) => g.source)} style={{ marginBottom: 20 }} />

          {c.lines.length === 0 ? (
            <Mono size={11} ls={0.5} color="rgba(240,240,240,0.4)" style={{ textTransform: 'none' }}>
              No contributors. This attribute is not currently produced by any base, allocation, equipment, or derivation.
            </Mono>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 26 }}>
              {/* by source */}
              <div>
                <Mono style={{ display: 'block', marginBottom: 10 }}>By source</Mono>
                {groups.map((g) => (
                  <div key={g.source} style={{ marginBottom: 12 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 5 }}>
                      <Swatch color={SOURCES[g.source].color} size={9} />
                      <span style={{ fontSize: 12, color: '#f0f0f0' }}>{SOURCES[g.source].label}</span>
                      <span style={{ flex: 1, height: 1, background: 'rgba(255,255,255,0.06)' }} />
                      <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12, color: 'rgba(240,240,240,0.9)' }}>{fmtSigned(g.total, 1)}</span>
                    </div>
                    {g.lines.map((ln, i) => (
                      <div key={i} style={{ display: 'flex', justifyContent: 'space-between', gap: 10, padding: '2px 0 2px 17px' }}>
                        <span style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.62)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                          {ln.source === 'derived' ? `${ATTR_BY_KEY[ln.from].name} (${ln.amount}× of ${fmtNum(ln.derivedValue, 1)})`
                            : ln.source === 'points' ? 'Allocated stat points'
                            : ln.source === 'base' ? 'Engine base value'
                            : `${ln.from}${ln.source === 'mod' ? ' · ' + ln.modType : ''}`}
                        </span>
                        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5, color: ln.applied < 0 ? '#e8b6a6' : 'rgba(240,240,240,0.85)' }}>
                          {fmtSigned(ln.applied, Math.abs(ln.applied) < 10 && !Number.isInteger(ln.applied) ? 1 : 0)}</span>
                      </div>
                    ))}
                  </div>
                ))}
              </div>

              {/* apply-order trace */}
              <div>
                <Mono style={{ display: 'block', marginBottom: 10 }}>Apply order</Mono>
                <div style={{ background: 'rgba(0,0,0,0.25)', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 5, padding: '12px 14px' }}>
                  <TraceLine label="Start" value={0} dec={meta.dec} faint />
                  {c.lines.filter((l) => !l.multiplied).map((ln, i) => (
                    <TraceLine key={i} color={SOURCES[ln.source].color}
                      label={ln.source === 'derived' ? ATTR_BY_KEY[ln.from].name : ln.source === 'points' ? 'Stat points' : ln.source === 'base' ? 'Base' : ln.from}
                      op={fmtSigned(ln.applied, Math.abs(ln.applied) < 10 && !Number.isInteger(ln.applied) ? 1 : 0)}
                      value={ln.running} dec={meta.dec} />
                  ))}
                  {mults.length > 0 && (
                    <>
                      <div style={{ borderTop: '1px dashed rgba(255,255,255,0.14)', margin: '7px 0 6px' }} />
                      <TraceLine label="Additive subtotal" value={c.additiveSubtotal} dec={meta.dec} strong />
                      {mults.map((ln, i) => (
                        <TraceLine key={i} color={SOURCES.item.color} label={`${ln.from} (mult)`}
                          op={`×${ln.factor}`} value={ln.running} dec={meta.dec} />
                      ))}
                    </>
                  )}
                  <div style={{ borderTop: '1px solid rgba(255,255,255,0.12)', margin: '7px 0 6px' }} />
                  <TraceLine label="Final" value={c.total} dec={meta.dec} accent={accent} strong />
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </SBoard>
  );
}

function TraceLine({ label, op, value, dec, color, faint, strong, accent }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '9px 1fr auto auto', alignItems: 'center', gap: 9, padding: '3px 0' }}>
      {color ? <Swatch color={color} size={8} /> : <span />}
      <span style={{ fontSize: 11.5, color: faint ? 'rgba(240,240,240,0.4)' : strong ? '#f0f0f0' : 'rgba(240,240,240,0.7)',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{label}</span>
      <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: color || 'rgba(240,240,240,0.5)', minWidth: 46, textAlign: 'right' }}>{op || ''}</span>
      <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5, minWidth: 60, textAlign: 'right',
        color: accent || (strong ? '#fff' : 'rgba(240,240,240,0.55)'), fontWeight: strong ? 600 : 400 }}>{fmtNum(value, dec)}</span>
    </div>
  );
}

Object.assign(window, { AttrInspector });
