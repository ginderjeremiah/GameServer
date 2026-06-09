/* sb-attr-stack.jsx — Direction A · "Stacked Bars"
   The whole sheet at a glance: one stacked bar per attribute, segments colored
   by source, total on the right. Core attributes are given a touch more weight
   (larger type + taller bars) above the derived set. Hover any segment for the
   exact contribution. Reads from the shared engine so the bars sum to the total. */

function AttrStack({ accent = '#a1c2f7', showInactive = true, density = 'comfortable' }) {
  const computed = React.useMemo(() => computeAttributes(), []);
  const [hover, setHover] = React.useState(null); // {key, source}

  const core = ATTR_META.filter((a) => a.group === 'core');
  const derived = ATTR_META.filter((a) => a.group === 'derived' && (showInactive || (!a.unused && !a.obsolete)));

  // shared scale per group so bars are comparable within a group
  const coreMax = Math.max(...core.map((a) => computed[a.key].total));
  const derMax = Math.max(...derived.map((a) => computed[a.key].total));

  const compact = density === 'compact';

  return (
    <SBoard label="At a glance" sub="every attribute, decomposed by source" accent={accent}>
      <SourceLegend style={{ marginBottom: 18 }} />

      <Mono style={{ marginBottom: 9, display: 'block' }}>Core attributes</Mono>
      <div style={{ display: 'flex', flexDirection: 'column', gap: compact ? 9 : 13, marginBottom: 22 }}>
        {core.map((a) => (
          <StackRow key={a.key} meta={a} computed={computed[a.key]} scaleMax={coreMax}
            big hover={hover} setHover={setHover} barH={compact ? 16 : 20} />
        ))}
      </div>

      <Mono style={{ marginBottom: 9, display: 'block' }}>Derived attributes</Mono>
      <div style={{ display: 'flex', flexDirection: 'column', gap: compact ? 7 : 10 }}>
        {derived.map((a) => (
          <StackRow key={a.key} meta={a} computed={computed[a.key]} scaleMax={derMax}
            hover={hover} setHover={setHover} barH={compact ? 12 : 14} />
        ))}
      </div>
    </SBoard>
  );
}

function StackRow({ meta, computed, scaleMax, big, barH, hover, setHover }) {
  const { groups, mults } = groupBySource(computed);
  const max = scaleMax || computed.total || 1;
  const isHoverRow = hover && hover.key === meta.key;
  const dim = (meta.unused || meta.obsolete);

  return (
    <div style={{ display: 'grid', gridTemplateColumns: big ? '116px 1fr 96px' : '116px 1fr 88px',
      alignItems: 'center', gap: 16, opacity: dim ? 0.62 : 1 }}>
      {/* label */}
      <div style={{ minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          <span style={{ fontSize: big ? 14.5 : 12.5, fontWeight: 500, color: '#f0f0f0', whiteSpace: 'nowrap' }}>{meta.name}</span>
        </div>
        <Mono size={8.5} ls={1.2} color="rgba(240,240,240,0.38)" style={{ marginTop: 1, display: 'block' }}>{meta.short}</Mono>
      </div>

      {/* bar */}
      <div style={{ position: 'relative' }}>
        <div style={{ display: 'flex', width: '100%', height: barH, borderRadius: 2, overflow: 'hidden',
          background: 'rgba(255,255,255,0.05)', boxShadow: 'inset 0 0 0 1px rgba(255,255,255,0.06)' }}>
          {groups.map((g) => {
            const w = Math.max(0, (g.total / max) * 100);
            if (w <= 0.01) return null;
            const on = !hover || (hover.key === meta.key && hover.source === g.source);
            return (
              <div key={g.source}
                onMouseEnter={() => setHover({ key: meta.key, source: g.source })}
                onMouseLeave={() => setHover(null)}
                style={{ width: `${w}%`, background: SOURCES[g.source].color, opacity: on ? 0.95 : 0.4,
                  borderRight: '1px solid rgba(13,14,18,0.5)', transition: 'opacity 120ms', cursor: 'default' }} />
            );
          })}
          {mults.map((m, i) => {
            const w = Math.max(0, (m.applied / max) * 100);
            if (w <= 0.01) return null;
            return <div key={i} title={`×${m.factor}`} style={{ width: `${w}%`,
              background: `repeating-linear-gradient(135deg, ${SOURCES.item.color}dd 0 3px, ${SOURCES.item.color}55 3px 6px)` }} />;
          })}
        </div>
        {isHoverRow && hover.source && (
          <div style={{ position: 'absolute', top: 'calc(100% + 4px)', left: 0, zIndex: 3,
            background: '#1c1d24', border: `1px solid ${SOURCES[hover.source].color}55`, borderRadius: 3,
            padding: '5px 9px', whiteSpace: 'nowrap', boxShadow: '0 6px 20px rgba(0,0,0,0.5)' }}>
            <Mono size={9} ls={1} color={SOURCES[hover.source].color}>{SOURCES[hover.source].label}</Mono>
            <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12, color: '#f0f0f0', marginLeft: 8 }}>
              {fmtSigned(groups.find((g) => g.source === hover.source).total, 1)}
            </span>
          </div>
        )}
      </div>

      {/* total */}
      <div style={{ textAlign: 'right', display: 'flex', alignItems: 'baseline', justifyContent: 'flex-end', gap: 4 }}>
        <span style={{ fontFamily: 'Geist, sans-serif', fontSize: big ? 21 : 16, fontWeight: 600, letterSpacing: -0.4,
          color: dim ? 'rgba(240,240,240,0.55)' : '#f0f0f0' }}>{fmtNum(computed.total, meta.dec)}</span>
        {meta.unit && meta.unit !== '×' && <span style={{ fontSize: 11, color: 'rgba(240,240,240,0.4)' }}>{meta.unit}</span>}
        {meta.unit === '×' && <span style={{ fontSize: 11, color: 'rgba(240,240,240,0.4)' }}>×</span>}
      </div>
    </div>
  );
}

Object.assign(window, { AttrStack });
