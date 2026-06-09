/* Direction A · Hexagon Forge
   The 6 core attributes map perfectly to a hexagon, so the radar IS the
   interface: allocate points and the build-shape grows. A faint dashed
   outline holds your *saved* shape so every pending point reads as
   before→after. Click a vertex to add a point; per-axis steppers for
   precision. Derived stats sit secondary below. */

const AXIS_MAX = 28;

function HexForge({ accent, detail }) {
  const b = useBuild();
  const disp = useLerp(b.values.map((v) => Math.min(v, AXIS_MAX)));
  const savedDisp = b.savedValues.map((v) => Math.min(v, AXIS_MAX));

  const SIZE = 430, C = SIZE / 2, R = SIZE / 2 - 64;
  const ang = (i) => (-90 + i * 60) * Math.PI / 180;
  const pt = (i, r) => [C + Math.cos(ang(i)) * r, C + Math.sin(ang(i)) * r];
  const ringPath = (frac) => ATTRS.map((_, i) => pt(i, R * frac).join(',')).join(' ');
  const shapePath = (vals) => vals.map((v, i) => pt(i, (v / AXIS_MAX) * R).join(',')).join(' ');

  return (
    <Board label="Hexagon Forge" sub="your build, as a shape" accent={accent}>
      {/* budget + presets */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, marginBottom: 10 }}>
        <BudgetMeter remaining={b.remaining} budget={b.budget} accent={accent} />
        <PresetRow onApply={b.applyPreset} accent={accent} />
      </div>

      <div style={{ flex: 1, minHeight: 0, display: 'flex', gap: 26, alignItems: 'center' }}>
        {/* radar */}
        <div style={{ flexShrink: 0, position: 'relative', width: SIZE, height: SIZE }}>
          <svg width={SIZE} height={SIZE} viewBox={`0 0 ${SIZE} ${SIZE}`} style={{ overflow: 'visible' }}>
            {/* rings */}
            {[0.25, 0.5, 0.75, 1].map((f) => (
              <polygon key={f} points={ringPath(f)} fill="none"
                stroke="rgba(255,255,255,0.07)" strokeWidth="1" />
            ))}
            {/* spokes */}
            {ATTRS.map((_, i) => {
              const [x, y] = pt(i, R);
              return <line key={i} x1={C} y1={C} x2={x} y2={y} stroke="rgba(255,255,255,0.07)" strokeWidth="1" />;
            })}
            {/* saved (ghost) shape */}
            <polygon points={shapePath(savedDisp)} fill="rgba(240,240,240,0.04)"
              stroke="rgba(240,240,240,0.32)" strokeWidth="1.2" strokeDasharray="3 3" />
            {/* draft shape */}
            <polygon points={shapePath(disp)} fill={`${accent}22`}
              stroke={accent} strokeWidth="1.8" style={{ filter: `drop-shadow(0 0 8px ${accent}66)` }} />
            {/* vertices (click to add a point) */}
            {disp.map((v, i) => {
              const [x, y] = pt(i, (v / AXIS_MAX) * R);
              const canInc = b.remaining > 0;
              return (
                <g key={i} style={{ cursor: canInc ? 'pointer' : 'default' }}
                  onClick={() => canInc && b.inc(i)}>
                  <circle cx={x} cy={y} r="11" fill="transparent" />
                  <circle cx={x} cy={y} r="4.5" fill={ATTRS[i].color}
                    stroke="#0d0e12" strokeWidth="1.5"
                    style={{ filter: `drop-shadow(0 0 5px ${ATTRS[i].color})` }} />
                </g>
              );
            })}
            {/* axis labels */}
            {ATTRS.map((a, i) => {
              const [lx, ly] = pt(i, R + 30);
              return (
                <g key={i}>
                  <text x={lx} y={ly - 4} textAnchor="middle" fontFamily="Geist Mono, monospace"
                    fontSize="11" letterSpacing="1" fill={a.color}>{a.key}</text>
                  <text x={lx} y={ly + 11} textAnchor="middle" fontFamily="Geist, sans-serif"
                    fontSize="13" fontWeight="600" fill="#f0f0f0">{b.values[i]}</text>
                </g>
              );
            })}
          </svg>
        </div>

        {/* attribute steppers */}
        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 7 }}>
          {ATTRS.map((a, i) => (
            <div key={a.id} style={{ display: 'flex', alignItems: 'center', gap: 12,
              padding: '8px 12px', borderRadius: 4, background: 'rgba(255,255,255,0.02)',
              border: '1px solid rgba(255,255,255,0.06)' }}>
              <span style={{ width: 8, height: 8, borderRadius: '50%', background: a.color,
                boxShadow: `0 0 7px ${a.color}99`, flexShrink: 0 }} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
                  <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: a.color, letterSpacing: 0.5 }}>{a.key}</span>
                  <span style={{ fontSize: 13.5, color: '#f0f0f0' }}>{a.name}</span>
                </div>
                <div style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.42)', marginTop: 1 }}>{a.blurb}</div>
              </div>
              <Delta from={b.savedValues[i]} to={b.values[i]} accent={accent} size={13} />
              <Stepper accent={accent}
                onDec={() => b.dec(i)} onInc={() => b.inc(i)}
                canDec={b.draft[i] > b.committed[i]} canInc={b.remaining > 0} />
            </div>
          ))}
        </div>
      </div>

      {/* derived strip (secondary) */}
      <DerivedStrip build={b} accent={accent} detail={detail} />

      <div style={{ marginTop: 14 }}>
        <CommitBar build={b} accent={accent} />
      </div>
    </Board>
  );
}

/* compact horizontal derived readout — secondary to core allocation */
function DerivedStrip({ build, accent, detail }) {
  const show = ['health', 'defense', 'critC', 'dodge', 'cdr'];
  return (
    <div style={{ marginTop: 16, paddingTop: 14, borderTop: '1px solid rgba(255,255,255,0.07)' }}>
      <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.6,
        textTransform: 'uppercase', color: 'rgba(240,240,240,0.38)', marginBottom: 10 }}>Resulting Stats</div>
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
        {show.map((k) => {
          const d = DERIVED.find((x) => x.key === k);
          const from = build.savedDerived[k], to = build.derived[k];
          const changed = from !== to;
          return (
            <div key={k} style={{ flex: '1 1 0', minWidth: 110, padding: '9px 12px', borderRadius: 4,
              background: changed ? `${accent}10` : 'rgba(255,255,255,0.02)',
              border: `1px solid ${changed ? `${accent}44` : 'rgba(255,255,255,0.06)'}`, transition: 'all 160ms' }}>
              <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 8.5, letterSpacing: 0.8,
                textTransform: 'uppercase', color: 'rgba(240,240,240,0.45)', marginBottom: 4 }}>{d.name}</div>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 4 }}>
                <span style={{ fontSize: 17, fontWeight: 600, letterSpacing: -0.3,
                  color: changed ? accent : '#f0f0f0' }}>{fmt(to)}{d.unit}</span>
                {changed && <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10,
                  color: to > from ? 'rgba(189,224,180,0.9)' : '#e8b6a6' }}>
                  {to > from ? '+' : ''}{fmt(round1(to - from))}</span>}
              </div>
              {detail && <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9,
                color: 'rgba(240,240,240,0.32)', marginTop: 4 }}>{d.formula}</div>}
            </div>
          );
        })}
      </div>
    </div>
  );
}

window.HexForge = HexForge;
window.DerivedStrip = DerivedStrip;
