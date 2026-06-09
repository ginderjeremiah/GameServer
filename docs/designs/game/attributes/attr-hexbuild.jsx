/* Converged attribute page — Direction A as the default, with an IN-PAGE
   toggle that densifies it into a Direction-D theorycraft view.

   · Guided (default): the hexagon is the hero (left), attribute steppers on
     the right with plain-language "feeds" tags. No derived-stat panel — so it
     can't get cluttered as more derived stats are added.
   · Theorycraft: same page, densified — allocation table on the left, the
     hexagon shrinks to a summary on the right above a grouped, SCROLLABLE
     derived-stats panel that scales to any number of stats.

   The radar, the +/- steppers, the budget, presets and commit bar are shared
   across both modes so the toggle reads as one page changing density. */

const FEEDS_MAP = {
  STR: ['health', 'critD'],
  END: ['health', 'defense', 'block'],
  INT: ['skill'],
  AGI: ['defense', 'dodge', 'cdr'],
  DEX: ['cdr', 'critC'],
  LUK: ['critC', 'dodge'],
};
const HEX_MAX = 28;

/* short stat labels + per-point marginal yield (auto-derives from the
   formulas, so it stays correct if the formulas change later) */
const SHORT_STAT = { health: 'HP', defense: 'Def', block: 'Block', dodge: 'Dodge', critC: 'Crit', critD: 'Crit Dmg', skill: 'Skill', cdr: 'CDR' };
function perPoint(i) {
  const base = ATTRS.map((a) => a.base);
  const bumped = [...base]; bumped[i] += 1;
  const d0 = derive(base), d1 = derive(bumped);
  return DERIVED.filter((s) => d1[s.key] !== d0[s.key])
    .map((s) => ({ key: s.key, short: SHORT_STAT[s.key], unit: s.unit, delta: round1(d1[s.key] - d0[s.key]) }));
}

/* ── reusable hexagon radar ───────────────────────────────────────────── */
function Radar({ build, accent, size, interactive = true }) {
  const disp = useLerp(build.values.map((v) => Math.min(v, HEX_MAX)));
  const savedDisp = build.savedValues.map((v) => Math.min(v, HEX_MAX));
  const C = size / 2, R = size / 2 - (size > 340 ? 60 : 42);
  const ang = (i) => (-90 + i * 60) * Math.PI / 180;
  const pt = (i, r) => [C + Math.cos(ang(i)) * r, C + Math.sin(ang(i)) * r];
  const ring = (f) => ATTRS.map((_, i) => pt(i, R * f).join(',')).join(' ');
  const shape = (vals) => vals.map((v, i) => pt(i, (v / HEX_MAX) * R).join(',')).join(' ');
  const big = size > 340;

  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ overflow: 'visible', flexShrink: 0 }}>
      {[0.25, 0.5, 0.75, 1].map((f) => (
        <polygon key={f} points={ring(f)} fill="none" stroke="rgba(255,255,255,0.07)" strokeWidth="1" />
      ))}
      {ATTRS.map((_, i) => { const [x, y] = pt(i, R); return <line key={i} x1={C} y1={C} x2={x} y2={y} stroke="rgba(255,255,255,0.07)" strokeWidth="1" />; })}
      <polygon points={shape(savedDisp)} fill="rgba(240,240,240,0.04)" stroke="rgba(240,240,240,0.32)" strokeWidth="1.2" strokeDasharray="3 3" />
      <polygon points={shape(disp)} fill={`${accent}22`} stroke={accent} strokeWidth={big ? 1.8 : 1.5} style={{ filter: `drop-shadow(0 0 8px ${accent}66)` }} />
      {disp.map((v, i) => {
        const [x, y] = pt(i, (v / HEX_MAX) * R);
        const canInc = interactive && build.remaining > 0;
        return (
          <g key={i} style={{ cursor: canInc ? 'pointer' : 'default' }} onClick={() => canInc && build.inc(i)}>
            <circle cx={x} cy={y} r="11" fill="transparent" />
            <circle cx={x} cy={y} r={big ? 4.5 : 3.5} fill={ATTRS[i].color} stroke="#0d0e12" strokeWidth="1.5" style={{ filter: `drop-shadow(0 0 5px ${ATTRS[i].color})` }} />
          </g>
        );
      })}
      {ATTRS.map((a, i) => {
        const [lx, ly] = pt(i, R + (big ? 30 : 22));
        return (
          <g key={i}>
            <text x={lx} y={ly - (big ? 4 : 3)} textAnchor="middle" fontFamily="Geist Mono, monospace" fontSize={big ? 11 : 9} letterSpacing="1" fill={a.color}>{a.key}</text>
            <text x={lx} y={ly + (big ? 11 : 9)} textAnchor="middle" fontFamily="Geist, sans-serif" fontSize={big ? 13 : 11} fontWeight="600" fill="#f0f0f0">{build.values[i]}</text>
          </g>
        );
      })}
    </svg>
  );
}

/* ── in-page mode toggle ──────────────────────────────────────────────── */
function ModeToggle({ mode, setMode, accent }) {
  const opts = [
    { key: 'guided', label: 'Guided' },
    { key: 'theory', label: 'Theorycraft' },
  ];
  return (
    <div style={{ display: 'inline-flex', padding: 3, borderRadius: 5, gap: 3,
      background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.1)' }}>
      {opts.map((o) => {
        const on = mode === o.key;
        return (
          <button key={o.key} onClick={() => setMode(o.key)} style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 10.5, letterSpacing: 0.8, textTransform: 'uppercase',
            padding: '6px 14px', borderRadius: 3, cursor: 'pointer', border: 'none',
            background: on ? `${accent}1f` : 'transparent',
            boxShadow: on ? `inset 0 0 0 1px ${accent}88` : 'none',
            color: on ? '#f0f0f0' : 'rgba(240,240,240,0.55)' }}>{o.label}</button>
        );
      })}
    </div>
  );
}

/* ── guided attribute row (with no-number "feeds" guidance) ───────────── */
function GuidedRow({ i, a, b, accent }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '9px 13px', borderRadius: 4,
      background: 'rgba(255,255,255,0.02)', border: '1px solid rgba(255,255,255,0.06)' }}>
      <span style={{ width: 8, height: 8, borderRadius: '50%', background: a.color, boxShadow: `0 0 7px ${a.color}99`, flexShrink: 0 }} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: a.color, letterSpacing: 0.5 }}>{a.key}</span>
          <span style={{ fontSize: 13.5, color: '#f0f0f0' }}>{a.name}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4, flexWrap: 'wrap' }}>
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 8, letterSpacing: 1, textTransform: 'uppercase', color: 'rgba(240,240,240,0.32)' }}>Feeds</span>
          {FEEDS_MAP[a.key].map((k) => {
            const d = DERIVED.find((x) => x.key === k);
            const changed = b.savedDerived[k] !== b.derived[k];
            return (
              <span key={k} style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 0.2,
                padding: '1px 6px', borderRadius: 2, whiteSpace: 'nowrap',
                background: changed ? `${accent}1a` : 'rgba(255,255,255,0.03)',
                border: `1px solid ${changed ? `${accent}55` : 'rgba(255,255,255,0.09)'}`,
                color: changed ? '#c0d8ff' : 'rgba(240,240,240,0.5)', transition: 'all 160ms' }}>{d.name}</span>
            );
          })}
        </div>
      </div>
      <Delta from={b.savedValues[i]} to={b.values[i]} accent={accent} size={13} />
      <Stepper accent={accent} onDec={() => b.dec(i)} onInc={() => b.inc(i)}
        canDec={b.draft[i] > b.committed[i]} canInc={b.remaining > 0} />
    </div>
  );
}

/* ── theorycraft allocation row (dense, mono, fills column height) ─────── */
function TheoryRow({ i, a, b, accent }) {
  const value = b.values[i], saved = b.savedValues[i], changed = value !== saved;
  const yields = perPoint(i);
  return (
    <div style={{ flex: 1, minHeight: 0, display: 'grid', gridTemplateColumns: '1.7fr 1.3fr 0.8fr auto', gap: 14,
      alignItems: 'center', padding: '0 16px', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
      {/* attribute + per-point marginal yield */}
      <div style={{ minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ width: 7, height: 7, borderRadius: '50%', background: a.color, boxShadow: `0 0 6px ${a.color}99`, flexShrink: 0 }} />
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: a.color }}>{a.key}</span>
          <span style={{ fontSize: 13.5, color: '#f0f0f0' }}>{a.name}</span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9, marginTop: 6, marginLeft: 15, flexWrap: 'wrap' }}>
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 7.5, letterSpacing: 0.8, textTransform: 'uppercase', color: 'rgba(240,240,240,0.3)' }}>per pt</span>
          {yields.map((y) => (
            <span key={y.key} style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, color: 'rgba(240,240,240,0.42)', whiteSpace: 'nowrap' }}>
              <span style={{ color: a.color }}>+{fmt(y.delta)}{y.unit}</span> {y.short}
            </span>
          ))}
        </div>
      </div>
      {/* allocation bar */}
      <div style={{ position: 'relative', height: 8 }}>
        <div style={{ position: 'absolute', inset: 0, borderRadius: 4, background: 'rgba(255,255,255,0.06)' }} />
        <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, borderRadius: 4, width: `${(Math.min(saved, value) / HEX_MAX) * 100}%`, background: 'rgba(240,240,240,0.26)' }} />
        {changed && <div style={{ position: 'absolute', top: 0, bottom: 0, borderRadius: 4, left: `${(Math.min(saved, value) / HEX_MAX) * 100}%`, width: `${(Math.abs(value - saved) / HEX_MAX) * 100}%`, background: value > saved ? accent : 'rgba(232,138,120,0.6)', boxShadow: value > saved ? `0 0 7px ${accent}88` : 'none' }} />}
      </div>
      {/* total */}
      <div style={{ textAlign: 'right' }}>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 16, fontWeight: 500, color: changed ? accent : '#f0f0f0' }}>{value}</span>
        {changed && <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, marginLeft: 4, color: value > saved ? 'rgba(189,224,180,0.85)' : '#e8b6a6' }}>{value > saved ? '+' : ''}{value - saved}</span>}
      </div>
      {/* stepper */}
      <Stepper accent={accent} onDec={() => b.dec(i)} onInc={() => b.inc(i)} canDec={b.draft[i] > b.committed[i]} canInc={b.remaining > 0} />
    </div>
  );
}

/* ── scalable derived-stats panel (theorycraft only) ──────────────────── */
function DerivedPanel({ build, accent }) {
  return (
    <div style={{ flex: 1, minHeight: 0, border: '1px solid rgba(255,255,255,0.08)', borderRadius: 4,
      background: 'rgba(0,0,0,0.25)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.6, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.42)', padding: '11px 14px 9px', borderBottom: '1px solid rgba(255,255,255,0.07)', flexShrink: 0 }}>
        Derived Stats <span style={{ color: 'rgba(240,240,240,0.25)' }}>· {DERIVED.length}</span>
      </div>
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '4px 0' }}>
        {DERIVED_GROUPS.map((g) => (
          <div key={g} style={{ marginBottom: 2 }}>
            <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.2, textTransform: 'uppercase', color: `${accent}99`, padding: '7px 14px 4px' }}>{g}</div>
            {DERIVED.filter((d) => d.group === g).map((d) => {
              const from = build.savedDerived[d.key], to = build.derived[d.key], changed = from !== to;
              return (
                <div key={d.key} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 14px', background: changed ? `${accent}0d` : 'transparent' }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11.5, color: 'rgba(240,240,240,0.66)' }}>{d.name}</div>
                    <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 8.5, color: 'rgba(240,240,240,0.3)', marginTop: 1 }}>{d.formula}</div>
                  </div>
                  {changed && <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, color: to > from ? 'rgba(189,224,180,0.9)' : '#e8b6a6' }}>{to > from ? '+' : ''}{fmt(round1(to - from))}</span>}
                  <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 13, minWidth: 50, textAlign: 'right', color: changed ? accent : '#f0f0f0', fontWeight: 500 }}>{fmt(to)}{d.unit}</span>
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}

/* ── the page ─────────────────────────────────────────────────────────── */
function HexBuild({ accent, defaultMode = 'guided' }) {
  const b = useBuild();
  const [mode, setMode] = React.useState(() => {
    try { return localStorage.getItem('ttf.attr.mode') || defaultMode; } catch (e) { return defaultMode; }
  });
  React.useEffect(() => { try { localStorage.setItem('ttf.attr.mode', mode); } catch (e) {} }, [mode]);
  const theory = mode === 'theory';

  return (
    <div style={{ width: '100%', height: '100%', background: 'linear-gradient(160deg, #16171e 0%, #0d0e12 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif', color: '#f0f0f0', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      {/* header */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16, padding: '20px 28px 16px', flexShrink: 0 }}>
        <div>
          <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 2, textTransform: 'uppercase', color: `${accent}b3`, marginBottom: 6 }}>Character</div>
          <h1 style={{ margin: 0, fontSize: 24, fontWeight: 500, letterSpacing: -0.3 }}>Attributes</h1>
        </div>
        <ModeToggle mode={mode} setMode={setMode} accent={accent} />
      </div>

      {/* budget */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, padding: '0 28px 16px', flexShrink: 0 }}>
        <BudgetMeter remaining={b.remaining} budget={b.budget} accent={accent} />
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 0.5, color: 'rgba(240,240,240,0.34)' }}>
          {theory ? 'Marginal yield shown per attribute' : 'Click an axis or use − / + to spend'}
        </span>
      </div>

      {/* main */}
      <div style={{ flex: 1, minHeight: 0, padding: '0 28px', display: 'flex', gap: theory ? 18 : 28,
        alignItems: theory ? 'stretch' : 'center' }}>
        {theory ? (
          <React.Fragment>
            {/* left: allocation table */}
            <div style={{ flex: '1.3 1 0', minWidth: 0, alignSelf: 'stretch', display: 'flex', flexDirection: 'column',
              border: '1px solid rgba(255,255,255,0.08)', borderRadius: 4, background: 'rgba(0,0,0,0.25)', overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1.7fr 1.3fr 0.8fr auto', gap: 14, padding: '10px 16px',
                borderBottom: '1px solid rgba(255,255,255,0.1)', background: 'rgba(255,255,255,0.02)' }}>
                {['Attribute', 'Allocation', 'Total', ''].map((c, i) => (
                  <span key={i} style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.3, textTransform: 'uppercase', color: 'rgba(240,240,240,0.42)', textAlign: i === 2 ? 'right' : 'left' }}>{c}</span>
                ))}
              </div>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                {ATTRS.map((a, i) => <TheoryRow key={a.id} i={i} a={a} b={b} accent={accent} />)}
              </div>
            </div>
            {/* right: compact radar + scalable derived panel */}
            <div style={{ flex: '1 1 0', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 14 }}>
              <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: '6px 0 2px', flexShrink: 0 }}>
                <Radar build={b} accent={accent} size={244} />
              </div>
              <DerivedPanel build={b} accent={accent} />
            </div>
          </React.Fragment>
        ) : (
          <React.Fragment>
            {/* hero radar */}
            <div style={{ flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Radar build={b} accent={accent} size={430} />
            </div>
            {/* attribute steppers */}
            <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
              {ATTRS.map((a, i) => <GuidedRow key={a.id} i={i} a={a} b={b} accent={accent} />)}
            </div>
          </React.Fragment>
        )}
      </div>

      {/* commit */}
      <div style={{ padding: '16px 28px 20px', marginTop: 14, flexShrink: 0 }}>
        <CommitBar build={b} accent={accent} />
      </div>
    </div>
  );
}

window.HexBuild = HexBuild;
