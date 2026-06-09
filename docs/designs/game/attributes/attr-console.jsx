/* Direction D · Console Readout
   A dense, in-game terminal. Core allocation on the left as a tight mono
   table with inline meters + a delta column; derived stats on the right,
   grouped and live. Built for players who want every number on screen at
   once — the theorycrafter end of the spectrum, but still core-focused. */

const CON_MAX = 28;

function ConsoleReadout({ accent, detail }) {
  const b = useBuild();
  return (
    <Board label="Console Readout" sub="everything on one panel" accent={accent} pad={24}>
      {/* budget pips + presets */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, marginBottom: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, letterSpacing: 1.4,
            textTransform: 'uppercase', color: 'rgba(240,240,240,0.5)' }}>Pool</span>
          <div style={{ display: 'flex', gap: 3 }}>
            {Array.from({ length: b.budget }).map((_, k) => (
              <span key={k} style={{ width: 7, height: 14, borderRadius: 1,
                background: k < b.remaining ? accent : 'rgba(255,255,255,0.08)',
                boxShadow: k < b.remaining ? `0 0 6px ${accent}aa` : 'none', transition: 'all 160ms' }} />
            ))}
          </div>
          <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 13, color: accent, fontWeight: 500 }}>
            {b.remaining}<span style={{ color: 'rgba(240,240,240,0.4)' }}>/{b.budget}</span></span>
        </div>
        <PresetRow onApply={b.applyPreset} accent={accent} compact />
      </div>

      <div style={{ flex: 1, minHeight: 0, display: 'flex', gap: 18 }}>
        {/* core table */}
        <div style={{ flex: '1.25 1 0', minWidth: 0, border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 4, background: 'rgba(0,0,0,0.25)', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <ConHeader cols={['Attribute', 'Allocation', 'Total', '']} />
          <div style={{ flex: 1 }}>
            {ATTRS.map((a, i) => <ConRow key={a.id} i={i} a={a} b={b} accent={accent} />)}
          </div>
        </div>

        {/* derived */}
        <div style={{ flex: '1 1 0', minWidth: 0, border: '1px solid rgba(255,255,255,0.08)',
          borderRadius: 4, background: 'rgba(0,0,0,0.25)', overflow: 'auto', padding: '4px 0' }}>
          <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.6,
            textTransform: 'uppercase', color: 'rgba(240,240,240,0.4)', padding: '12px 16px 8px' }}>
            Derived Stats <span style={{ color: 'rgba(240,240,240,0.25)' }}>· result</span>
          </div>
          {DERIVED_GROUPS.map((g) => (
            <div key={g} style={{ marginBottom: 4 }}>
              <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.2,
                textTransform: 'uppercase', color: `${accent}99`, padding: '6px 16px 4px' }}>{g}</div>
              {DERIVED.filter((d) => d.group === g).map((d) => {
                const from = b.savedDerived[d.key], to = b.derived[d.key], changed = from !== to;
                return (
                  <div key={d.key} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 16px',
                    background: changed ? `${accent}0d` : 'transparent' }}>
                    <span style={{ flex: 1, fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                      color: 'rgba(240,240,240,0.62)' }}>{d.name}
                      {detail && <span style={{ color: 'rgba(240,240,240,0.28)', marginLeft: 6 }}>{d.formula}</span>}</span>
                    {changed && <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10,
                      color: to > from ? 'rgba(189,224,180,0.9)' : '#e8b6a6' }}>
                      {to > from ? '+' : ''}{fmt(round1(to - from))}</span>}
                    <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 13, minWidth: 52, textAlign: 'right',
                      color: changed ? accent : '#f0f0f0', fontWeight: 500 }}>{fmt(to)}{d.unit}</span>
                  </div>
                );
              })}
            </div>
          ))}
        </div>
      </div>

      <div style={{ marginTop: 14 }}><CommitBar build={b} accent={accent} /></div>
    </Board>
  );
}

function ConHeader({ cols }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1.6fr 1fr auto', gap: 10, alignItems: 'center',
      padding: '10px 16px', borderBottom: '1px solid rgba(255,255,255,0.1)', background: 'rgba(255,255,255,0.02)' }}>
      {cols.map((c, i) => (
        <span key={i} style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9, letterSpacing: 1.3,
          textTransform: 'uppercase', color: 'rgba(240,240,240,0.42)',
          textAlign: i === 2 ? 'right' : 'left' }}>{c}</span>
      ))}
    </div>
  );
}

function ConRow({ i, a, b, accent }) {
  const value = b.values[i], saved = b.savedValues[i];
  const changed = value !== saved;
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1.6fr 1fr auto', gap: 10, alignItems: 'center',
      padding: '10px 16px', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
      {/* attribute */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
        <span style={{ width: 7, height: 7, borderRadius: '50%', background: a.color, boxShadow: `0 0 6px ${a.color}99`, flexShrink: 0 }} />
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: a.color }}>{a.key}</span>
        <span style={{ fontSize: 12.5, color: 'rgba(240,240,240,0.55)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{a.name}</span>
      </div>
      {/* allocation bar */}
      <div style={{ position: 'relative', height: 7 }}>
        <div style={{ position: 'absolute', inset: 0, borderRadius: 4, background: 'rgba(255,255,255,0.06)' }} />
        <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, borderRadius: 4,
          width: `${(Math.min(saved, value) / CON_MAX) * 100}%`, background: 'rgba(240,240,240,0.26)' }} />
        {changed && <div style={{ position: 'absolute', top: 0, bottom: 0, borderRadius: 4,
          left: `${(Math.min(saved, value) / CON_MAX) * 100}%`, width: `${(Math.abs(value - saved) / CON_MAX) * 100}%`,
          background: value > saved ? accent : 'rgba(232,138,120,0.6)', boxShadow: value > saved ? `0 0 7px ${accent}88` : 'none' }} />}
      </div>
      {/* total */}
      <div style={{ textAlign: 'right' }}>
        <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 15, fontWeight: 500,
          color: changed ? accent : '#f0f0f0' }}>{value}</span>
        {changed && <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, marginLeft: 4,
          color: value > saved ? 'rgba(189,224,180,0.85)' : '#e8b6a6' }}>{value > saved ? '+' : ''}{value - saved}</span>}
      </div>
      {/* stepper */}
      <Stepper accent={accent} onDec={() => b.dec(i)} onInc={() => b.inc(i)}
        canDec={b.draft[i] > b.committed[i]} canInc={b.remaining > 0} />
    </div>
  );
}

window.ConsoleReadout = ConsoleReadout;
