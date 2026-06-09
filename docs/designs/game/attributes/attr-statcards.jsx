/* Direction C · Stat Cards
   The accessible default. One card per attribute, each explaining in plain
   language what it does and which stats it feeds. The value reads as
   "12 → 15" so before→after is obvious, and a card lights up when you've
   invested in it. Steppers keep allocation simple and tactile. */

const FEEDS = {
  STR: ['health', 'critD'],
  END: ['health', 'defense', 'block'],
  INT: ['skill'],
  AGI: ['defense', 'dodge', 'cdr'],
  DEX: ['cdr', 'critC'],
  LUK: ['critC', 'dodge'],
};

function StatCards({ accent, detail }) {
  const b = useBuild();
  return (
    <Board label="Stat Cards" sub="what each point buys you" accent={accent}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20, marginBottom: 18 }}>
        <BudgetMeter remaining={b.remaining} budget={b.budget} accent={accent} />
        <PresetRow onApply={b.applyPreset} accent={accent} />
      </div>

      <div style={{ flex: 1, minHeight: 0, display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)', gridAutoRows: '1fr', gap: 14 }}>
        {ATTRS.map((a, i) => <AttrCard key={a.id} i={i} a={a} b={b} accent={accent} detail={detail} />)}
      </div>

      <div style={{ marginTop: 16, paddingTop: 14, borderTop: '1px solid rgba(255,255,255,0.07)' }}>
        <CommitBar build={b} accent={accent} />
      </div>
    </Board>
  );
}

function AttrCard({ i, a, b, accent, detail }) {
  const value = b.values[i], saved = b.savedValues[i];
  const boosted = b.draft[i] > b.committed[i];
  const frac = Math.min(value / AXIS_C_MAX, 1);
  return (
    <div style={{ position: 'relative', display: 'flex', flexDirection: 'column',
      padding: '15px 16px', borderRadius: 5, overflow: 'hidden',
      background: boosted ? `${a.color}0f` : 'rgba(255,255,255,0.02)',
      border: `1px solid ${boosted ? `${a.color}66` : 'rgba(255,255,255,0.08)'}`,
      boxShadow: boosted ? `0 0 18px ${a.color}1f, inset 0 0 18px ${a.color}0a` : 'none',
      transition: 'all 180ms ease' }}>
      {/* top accent edge */}
      <div style={{ position: 'absolute', top: 0, left: 0, right: 0, height: 2,
        background: a.color, opacity: boosted ? 0.9 : 0.32, boxShadow: boosted ? `0 0 8px ${a.color}` : 'none',
        transition: 'opacity 180ms' }} />

      {/* header */}
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 8 }}>
        <div>
          <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 1.2,
            color: a.color, marginBottom: 2 }}>{a.key}</div>
          <div style={{ fontSize: 16, fontWeight: 500, color: '#f0f0f0', letterSpacing: -0.2 }}>{a.name}</div>
        </div>
        <Stepper accent={accent}
          onDec={() => b.dec(i)} onInc={() => b.inc(i)}
          canDec={b.draft[i] > b.committed[i]} canInc={b.remaining > 0} />
      </div>

      {/* value */}
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 9, margin: '12px 0 2px' }}>
        <span style={{ fontSize: 34, fontWeight: 600, letterSpacing: -1,
          color: boosted ? a.color : '#f0f0f0', lineHeight: 1,
          textShadow: boosted ? `0 0 16px ${a.color}55` : 'none', transition: 'color 180ms' }}>{value}</span>
        {value !== saved && (
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 12, color: 'rgba(240,240,240,0.5)' }}>
            from {saved} <span style={{ color: value > saved ? accent : '#e8b6a6' }}>
              ({value > saved ? '+' : ''}{value - saved})</span>
          </span>
        )}
      </div>

      {/* value bar */}
      <div style={{ height: 3, borderRadius: 2, background: 'rgba(255,255,255,0.06)', overflow: 'hidden', margin: '6px 0 12px' }}>
        <div style={{ height: '100%', width: `${frac * 100}%`, background: a.color,
          boxShadow: `0 0 6px ${a.color}99`, transition: 'width 200ms' }} />
      </div>

      {/* description */}
      <div style={{ fontSize: 12.5, lineHeight: 1.45, color: 'rgba(240,240,240,0.55)', flex: 1 }}>{a.desc}</div>

      {/* feeds */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 12, flexWrap: 'wrap' }}>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 8.5, letterSpacing: 1,
          textTransform: 'uppercase', color: 'rgba(240,240,240,0.35)' }}>Feeds</span>
        {FEEDS[a.key].map((k) => {
          const d = DERIVED.find((x) => x.key === k);
          const from = b.savedDerived[k], to = b.derived[k], changed = from !== to;
          return (
            <span key={k} style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 0.3,
              padding: '2px 7px', borderRadius: 2, whiteSpace: 'nowrap',
              background: changed ? `${accent}1a` : 'rgba(255,255,255,0.03)',
              border: `1px solid ${changed ? `${accent}55` : 'rgba(255,255,255,0.1)'}`,
              color: changed ? '#c0d8ff' : 'rgba(240,240,240,0.6)', transition: 'all 160ms' }}>
              {d.name}{detail ? ` ${fmt(to)}${d.unit}` : ''}
            </span>
          );
        })}
      </div>
    </div>
  );
}

const AXIS_C_MAX = 30;
window.StatCards = StatCards;
