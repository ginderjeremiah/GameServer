/* Shared model for the Attribute-page explorations.
   One source of truth so all four directions behave identically:
     · ATTRS      — the 6 core attributes (mirrors EAttribute 0..5)
     · DERIVED    — stats computed from core values (real formulas where they
                    exist in battle-attributes.ts; the rest are illustrative)
     · PRESETS    — quick build shapes (tank / glass-cannon / evasion / even)
     · useBuild() — allocation state: base + committed + pending, point budget,
                    inc/dec/set, save/discard, limited respec, apply preset
     · useLerp()  — smooth numeric interpolation for the radar morph
*/

/* ── core attributes ─────────────────────────────────────────────────── */
const ATTRS = [
  { id: 0, key: 'STR', name: 'Strength',  color: '#e8b6a6', base: 12,
    blurb: 'Raw physical power.',  desc: 'Increases max health and the damage of your critical hits.' },
  { id: 1, key: 'END', name: 'Endurance', color: '#bde0b4', base: 10,
    blurb: 'Toughness and grit.',  desc: 'Increases max health, defense, and your chance to block.' },
  { id: 2, key: 'INT', name: 'Intellect', color: '#c4b6ec', base: 8,
    blurb: 'Arcane aptitude.',     desc: 'Amplifies the power of your active skills.' },
  { id: 3, key: 'AGI', name: 'Agility',   color: '#a1c2f7', base: 11,
    blurb: 'Speed and reflexes.',  desc: 'Improves defense, cooldown recovery, and dodge chance.' },
  { id: 4, key: 'DEX', name: 'Dexterity', color: '#f0d28a', base: 9,
    blurb: 'Precision and aim.',   desc: 'Speeds cooldown recovery and raises critical hit chance.' },
  { id: 5, key: 'LUK', name: 'Luck',      color: '#8fded0', base: 7,
    blurb: 'Fortune favors you.',  desc: 'Raises critical and dodge chance, and improves rare drops.' },
];

const POINT_BUDGET = 18;          // unspent points available this session
const RESPEC_COST = '250 g';      // limited / costs something

/* ── derived stats — keep core allocation the focus, these are secondary ─ */
const DERIVED = [
  { key: 'health',   name: 'Max Health',   unit: '',   group: 'Survivability', real: true,
    formula: '50 + 20·END + 5·STR' },
  { key: 'defense',  name: 'Defense',      unit: '',   group: 'Survivability', real: true,
    formula: '2 + END + 0.5·AGI' },
  { key: 'block',    name: 'Block Chance', unit: '%',  group: 'Survivability', real: false,
    formula: '0.5·END' },
  { key: 'dodge',    name: 'Dodge Chance', unit: '%',  group: 'Survivability', real: false,
    formula: '0.3·AGI + 0.2·LUK' },
  { key: 'critC',    name: 'Crit Chance',  unit: '%',  group: 'Offense', real: false,
    formula: '0.5·DEX + 0.2·LUK' },
  { key: 'critD',    name: 'Crit Damage',  unit: '%',  group: 'Offense', real: false,
    formula: '150 + 2·STR' },
  { key: 'skill',    name: 'Skill Power',  unit: '×',  group: 'Offense', real: false,
    formula: '1 + 0.08·INT' },
  { key: 'cdr',      name: 'Cooldown Rec.', unit: '/s', group: 'Utility', real: true,
    formula: '0.4·AGI + 0.1·DEX' },
];
const DERIVED_GROUPS = ['Survivability', 'Offense', 'Utility'];

function derive(v) {
  const [STR, END, INT, AGI, DEX, LUK] = v;
  return {
    health: 50 + 20 * END + 5 * STR,
    defense: round1(2 + END + 0.5 * AGI),
    block: round1(0.5 * END),
    dodge: round1(0.3 * AGI + 0.2 * LUK),
    critC: round1(0.5 * DEX + 0.2 * LUK),
    critD: 150 + 2 * STR,
    skill: round2(1 + 0.08 * INT),
    cdr: round1(0.4 * AGI + 0.1 * DEX),
  };
}

/* ── preset build shapes (weights across STR,END,INT,AGI,DEX,LUK) ─────── */
const PRESETS = [
  { key: 'balanced',  name: 'Balanced',     weights: [1, 1, 1, 1, 1, 1] },
  { key: 'juggernaut',name: 'Juggernaut',   weights: [2, 3, 0, 1, 0, 0] },
  { key: 'cannon',    name: 'Glass Cannon', weights: [2, 0, 2, 0, 3, 1] },
  { key: 'duelist',   name: 'Duelist',      weights: [0, 1, 0, 3, 2, 2] },
];

/* ── helpers ─────────────────────────────────────────────────────────── */
function round1(n) { return Math.round(n * 10) / 10; }
function round2(n) { return Math.round(n * 100) / 100; }
function fmt(n) { return Number.isInteger(n) ? n.toString() : n.toFixed(n < 10 ? (Number.isInteger(n * 10) ? 1 : 2) : 1); }

/* distribute `n` whole points across positive weights, largest-remainder. */
function distribute(n, weights) {
  const total = weights.reduce((a, b) => a + b, 0);
  if (total === 0 || n <= 0) return weights.map(() => 0);
  const raw = weights.map((w) => (w / total) * n);
  const out = raw.map((x) => Math.floor(x));
  let left = n - out.reduce((a, b) => a + b, 0);
  const order = raw
    .map((x, i) => ({ i, frac: x - Math.floor(x), w: weights[i] }))
    .filter((o) => o.w > 0)
    .sort((a, b) => b.frac - a.frac);
  let k = 0;
  while (left > 0 && order.length) { out[order[k % order.length].i] += 1; left--; k++; }
  return out;
}

/* ── allocation state ────────────────────────────────────────────────── */
function useBuild() {
  const base = ATTRS.map((a) => a.base);
  const [committed, setCommitted] = React.useState(() => ATTRS.map(() => 0)); // saved spend
  const [draft, setDraft] = React.useState(() => ATTRS.map(() => 0));         // pending spend
  const [budget, setBudget] = React.useState(POINT_BUDGET);                   // unspent pool
  const [flash, setFlash] = React.useState(false);

  const spent = draft.reduce((a, b) => a + b, 0);
  const remaining = budget - spent;
  const values = base.map((b, i) => b + draft[i]);
  const savedValues = base.map((b, i) => b + committed[i]);
  const dirty = draft.some((d, i) => d !== committed[i]);

  const inc = (i, by = 1) => setDraft((d) => {
    const room = budget - d.reduce((a, b) => a + b, 0);
    const add = Math.min(by, room);
    if (add <= 0) return d;
    const n = [...d]; n[i] += add; setFlash(false); return n;
  });
  // can only un-spend points added this session (committed points need a respec)
  const dec = (i, by = 1) => setDraft((d) => {
    if (d[i] <= committed[i]) return d;
    const n = [...d]; n[i] = Math.max(committed[i], d[i] - by); setFlash(false); return n;
  });
  const setSpend = (i, target) => setDraft((d) => {
    const others = d.reduce((a, b, j) => a + (j === i ? 0 : b), 0);
    const max = Math.min(target, budget - others);
    const n = [...d]; n[i] = Math.max(committed[i], Math.round(max)); setFlash(false); return n;
  });
  const applyPreset = (weights) => setDraft(() => {
    const floor = [...committed];
    const extra = distribute(budget - floor.reduce((a, b) => a + b, 0), weights);
    setFlash(false);
    return floor.map((c, i) => c + extra[i]);
  });
  const save = () => { setCommitted([...draft]); setFlash(true); setTimeout(() => setFlash(false), 1800); };
  const discard = () => { setDraft([...committed]); setFlash(false); };
  const respec = () => { setBudget((b) => b + committed.reduce((a, c) => a + c, 0)); setCommitted(ATTRS.map(() => 0)); setDraft(ATTRS.map(() => 0)); setFlash(false); };

  return {
    base, draft, committed, budget, spent, remaining, values, savedValues, dirty, flash,
    derived: derive(values), savedDerived: derive(savedValues),
    inc, dec, setSpend, applyPreset, save, discard, respec,
  };
}

/* smooth interpolation toward a target vector (for the radar morph) */
function useLerp(target, speed = 0.18) {
  const [disp, setDisp] = React.useState(target);
  const dispRef = React.useRef(target);
  const targetRef = React.useRef(target);
  targetRef.current = target;
  React.useEffect(() => {
    let raf;
    const tick = () => {
      const cur = dispRef.current;
      const tgt = targetRef.current;
      let moved = false;
      const next = cur.map((c, i) => {
        const d = tgt[i] - c;
        if (Math.abs(d) < 0.01) return tgt[i];
        moved = true;
        return c + d * speed;
      });
      dispRef.current = next;
      setDisp(next);
      if (moved) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [target.join(',')]);
  return disp;
}

/* ── small shared atoms ──────────────────────────────────────────────── */

/* preset chips row */
function PresetRow({ onApply, accent, compact }) {
  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      {PRESETS.map((p) => <PresetChip key={p.key} preset={p} onApply={onApply} accent={accent} compact={compact} />)}
    </div>
  );
}
function PresetChip({ preset, onApply, accent, compact }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button
      onClick={() => onApply(preset.weights)}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        fontFamily: 'Geist Mono, monospace', fontSize: compact ? 9.5 : 10.5, letterSpacing: 0.6,
        textTransform: 'uppercase', padding: compact ? '5px 9px' : '6px 12px', borderRadius: 3,
        cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 130ms',
        background: hover ? `${accent}1f` : 'rgba(255,255,255,0.03)',
        border: `1px solid ${hover ? accent : 'rgba(255,255,255,0.14)'}`,
        color: hover ? '#f0f0f0' : 'rgba(240,240,240,0.7)',
      }}>{preset.name}</button>
  );
}

/* +/- stepper */
function Stepper({ onDec, onInc, canDec, canInc, accent }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
      <StepBtn dir="-" onClick={onDec} disabled={!canDec} accent={accent} />
      <StepBtn dir="+" onClick={onInc} disabled={!canInc} accent={accent} />
    </div>
  );
}
function StepBtn({ dir, onClick, disabled, accent }) {
  const [hover, setHover] = React.useState(false);
  const plus = dir === '+';
  return (
    <button
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      disabled={disabled}
      style={{
        width: 26, height: 26, borderRadius: 3, padding: 0, cursor: disabled ? 'default' : 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: disabled ? 'rgba(255,255,255,0.02)'
          : plus ? (hover ? `${accent}26` : `${accent}14`) : (hover ? 'rgba(255,255,255,0.07)' : 'rgba(255,255,255,0.03)'),
        border: `1px solid ${disabled ? 'rgba(255,255,255,0.07)'
          : plus ? (hover ? accent : `${accent}66`) : 'rgba(255,255,255,0.16)'}`,
        color: disabled ? 'rgba(240,240,240,0.22)' : plus ? '#c0d8ff' : 'rgba(240,240,240,0.8)',
        transition: 'all 120ms',
      }}>
      <svg width="11" height="11" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round">
        {plus ? <path d="M6 2v8M2 6h8" /> : <path d="M2 6h8" />}
      </svg>
    </button>
  );
}

/* points-remaining budget readout */
function BudgetMeter({ remaining, budget, accent, label = 'Points to spend' }) {
  const pct = budget > 0 ? (remaining / budget) * 100 : 0;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 7, minWidth: 210 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12, whiteSpace: 'nowrap' }}>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 1.4,
          textTransform: 'uppercase', color: 'rgba(240,240,240,0.5)' }}>{label}</span>
        <span style={{ fontFamily: 'Geist, sans-serif', fontSize: 18, fontWeight: 600, whiteSpace: 'nowrap',
          color: remaining > 0 ? accent : 'rgba(240,240,240,0.4)', letterSpacing: -0.3,
          textShadow: remaining > 0 ? `0 0 12px ${accent}66` : 'none' }}>{remaining}<span style={{
            fontSize: 11, color: 'rgba(240,240,240,0.4)', fontWeight: 400 }}>&nbsp;/&nbsp;{budget}</span></span>
      </div>
      <div style={{ height: 4, borderRadius: 2, background: 'rgba(255,255,255,0.07)', overflow: 'hidden' }}>
        <div style={{ height: '100%', width: `${pct}%`, background: accent,
          boxShadow: `0 0 8px ${accent}`, transition: 'width 220ms cubic-bezier(.4,0,.2,1)' }} />
      </div>
    </div>
  );
}

/* delta chip e.g. 12 → 15 */
function Delta({ from, to, accent, size = 13 }) {
  if (from === to) return <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: size,
    color: 'rgba(240,240,240,0.45)' }}>{fmt(to)}</span>;
  const up = to > from;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontFamily: 'Geist Mono, monospace', fontSize: size }}>
      <span style={{ color: 'rgba(240,240,240,0.4)' }}>{fmt(from)}</span>
      <svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="rgba(240,240,240,0.4)" strokeWidth="1.4">
        <path d="M2 5h6M5.5 2.5L8 5l-2.5 2.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      <span style={{ color: up ? accent : '#e8b6a6', fontWeight: 500 }}>{fmt(to)}</span>
    </span>
  );
}

/* commit / discard / respec footer (shared across directions) */
function CommitBar({ build, accent, respecNote = true }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, fontFamily: 'Geist Mono, monospace',
        fontSize: 11, letterSpacing: 0.3, color: 'rgba(240,240,240,0.5)' }}>
        {build.flash ? (
          <span style={{ color: '#bde0b4', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <svg width="12" height="12" viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.6">
              <path d="M3 7.3L6 10.2L11 4" strokeLinecap="round" strokeLinejoin="round" /></svg>
            Attributes saved
          </span>
        ) : build.dirty ? (
          <span style={{ color: 'rgba(240,240,240,0.78)' }}>{build.spent - build.committed.reduce((a,b)=>a+b,0)} pending</span>
        ) : <span>No changes</span>}
        {respecNote && (
          <button onClick={build.respec} title={`Refund all points · ${RESPEC_COST}`} style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 0.5, textTransform: 'uppercase',
            background: 'transparent', border: '1px solid rgba(232,138,120,0.3)', color: 'rgba(232,182,166,0.85)',
            borderRadius: 3, padding: '4px 9px', cursor: 'pointer' }}>Respec · {RESPEC_COST}</button>
        )}
      </div>
      <div style={{ display: 'flex', gap: 9 }}>
        <BarBtn onClick={build.discard} disabled={!build.dirty} accent={accent}>Discard</BarBtn>
        <BarBtn onClick={build.save} disabled={!build.dirty} accent={accent} primary>Confirm</BarBtn>
      </div>
    </div>
  );
}
function BarBtn({ children, onClick, disabled, primary, accent }) {
  const [hover, setHover] = React.useState(false);
  return (
    <button onClick={disabled ? undefined : onClick}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)} disabled={disabled}
      style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, letterSpacing: 0.6, textTransform: 'uppercase',
        padding: '8px 18px', borderRadius: 3, cursor: disabled ? 'not-allowed' : 'pointer', transition: 'all 140ms',
        background: disabled ? 'transparent' : primary ? `${accent}1f` : hover ? 'rgba(255,255,255,0.05)' : 'transparent',
        border: `1px solid ${disabled ? 'rgba(255,255,255,0.08)' : primary ? accent : hover ? 'rgba(255,255,255,0.3)' : 'rgba(255,255,255,0.16)'}`,
        color: disabled ? 'rgba(240,240,240,0.3)' : primary ? '#c0d8ff' : 'rgba(240,240,240,0.85)',
        boxShadow: hover && !disabled && primary ? `0 0 12px ${accent}55` : 'none' }}>{children}</button>
  );
}

/* board frame — consistent dark shell + titled header per direction */
function Board({ label, sub, accent, children, pad = 28 }) {
  return (
    <div style={{ width: '100%', height: '100%', background: 'linear-gradient(160deg, #16171e 0%, #0d0e12 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif', color: '#f0f0f0', display: 'flex', flexDirection: 'column',
      overflow: 'hidden' }}>
      <div style={{ padding: `20px ${pad}px 0`, flexShrink: 0 }}>
        <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 2, textTransform: 'uppercase',
          color: `${accent}b3`, marginBottom: 6 }}>Character · Attributes</div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 12 }}>
          <h1 style={{ margin: 0, fontSize: 23, fontWeight: 500, letterSpacing: -0.3 }}>{label}</h1>
          {sub && <span style={{ fontSize: 12.5, color: 'rgba(240,240,240,0.45)' }}>{sub}</span>}
        </div>
      </div>
      <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', padding: pad, paddingTop: 18 }}>
        {children}
      </div>
    </div>
  );
}

Object.assign(window, {
  ATTRS, DERIVED, DERIVED_GROUPS, PRESETS, POINT_BUDGET, RESPEC_COST,
  derive, distribute, fmt, useBuild, useLerp,
  PresetRow, Stepper, BudgetMeter, Delta, CommitBar, Board,
});
