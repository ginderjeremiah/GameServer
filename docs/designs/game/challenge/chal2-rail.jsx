/* chal2-rail.jsx — Direction C · "Type Rail"

   ChallengeType is a navigable left rail: the 8 types listed with per-type
   completion and a mini meter, plus an "All" overview. Selecting a type filters
   the detail pane to just that type's challenges (shown as generous cards with
   reward tiles). A summary header carries overall progress + "next up". This is
   the most type-forward layout — type literally is the navigation.
*/

function RailItem({ tid, meta, items, active, onClick }) {
  const { total, done } = typeStats(items);
  const complete = done === total;
  const acc = meta.accent;
  return (
    <button onClick={onClick}
      style={{
        display: 'flex', alignItems: 'center', gap: 11, width: '100%', textAlign: 'left', cursor: 'pointer',
        padding: '9px 11px', borderRadius: 3, border: `1px solid ${active ? hexA(acc, 0.5) : 'transparent'}`,
        background: active ? hexA(acc, 0.12) : 'transparent', transition: 'all 120ms',
      }}>
      <div style={{ width: 26, height: 26, flexShrink: 0, borderRadius: 3, display: 'flex', alignItems: 'center', justifyContent: 'center', background: hexA(acc, active ? 0.16 : 0.08), border: `1px solid ${hexA(acc, active ? 0.5 : 0.28)}` }}>
        <TypeGlyph typeId={tid} color={acc} size={14} />
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontSize: 13, color: active ? INV.text : INV.t2, letterSpacing: -0.1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{meta.label}</div>
        <div style={{ marginTop: 5, height: 3, borderRadius: 2, background: 'rgba(255,255,255,0.07)', overflow: 'hidden' }}>
          <div style={{ height: '100%', width: `${(done / Math.max(1, total)) * 100}%`, background: complete ? INV.success : hexA(acc, 0.8) }} />
        </div>
      </div>
      <span style={{ fontFamily: INV.mono, fontSize: 9.5, color: complete ? INV.success : INV.t3, flexShrink: 0 }}>{done}/{total}</span>
    </button>
  );
}

function DetailCard({ c, layer, tweaks }) {
  const done = c.state === 'done';
  return (
    <div style={{ background: done ? hexA(INV.success, 0.04) : INV.panel, border: `1px solid ${done ? hexA(INV.success, 0.28) : INV.b1}`, borderRadius: 4, padding: '15px 17px', display: 'flex', flexDirection: 'column', gap: 11 }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <StatusNode state={c.state} accent={c.typeAccent} size={20} />
          <span style={{ fontSize: 16, color: done ? INV.t2 : INV.text, letterSpacing: -0.2 }}>{c.name}</span>
          {c.target && <span style={{ fontFamily: INV.mono, fontSize: 9, letterSpacing: 0.5, color: hexA(c.typeAccent, 0.85), border: `1px solid ${hexA(c.typeAccent, 0.3)}`, borderRadius: 2, padding: '1px 6px' }}>{c.target}</span>}
          <div style={{ flex: 1 }} />
          {done && <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{c.completedAt}</MonoLabel>}
        </div>
        <div style={{ fontSize: 12, color: INV.t3, marginTop: 6, lineHeight: 1.5 }}>{c.desc}</div>
      </div>
      <ProgressReadout c={c} />
      <Reward reward={c.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="tile" />
    </div>
  );
}

function OverviewPane({ all, layer, tweaks, onPick }) {
  const s = overallSummary(all);
  const next = nextUp(all);
  const groups = groupByType(all);
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div style={{ display: 'flex', gap: 14 }}>
        <div style={{ flex: 1, background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, padding: '16px 18px' }}>
          <MonoLabel color={INV.t4}>Overall progress</MonoLabel>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, margin: '8px 0 10px' }}>
            <span style={{ fontFamily: INV.mono, fontSize: 30, color: INV.success, lineHeight: 1 }}>{s.done}</span>
            <span style={{ fontFamily: INV.mono, fontSize: 14, color: INV.t4 }}>/ {s.total} unlocked</span>
          </div>
          <Bar percent={s.pct} accent={INV.accent} done={false} height={6} />
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 7 }}>
            <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{s.active} in progress</MonoLabel>
            <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{s.pct}% complete</MonoLabel>
          </div>
        </div>
        {next && next.reward && (
          <div style={{ width: 280, flexShrink: 0, background: hexA(next.reward.accent, 0.05), border: `1px solid ${hexA(next.reward.accent, 0.28)}`, borderRadius: 4, padding: '16px 18px' }}>
            <MonoLabel color={hexA(next.reward.accent, 0.75)}>Next up · closest</MonoLabel>
            <div style={{ fontSize: 15, color: INV.text, margin: '8px 0 4px' }}>{next.name}</div>
            <div style={{ marginBottom: 11 }}><ProgressReadout c={next} showBar={false} /></div>
            <Reward reward={next.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="chip" />
          </div>
        )}
      </div>
      <div>
        <div style={{ marginBottom: 10 }}><MonoLabel color={INV.t4}>All types</MonoLabel></div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          {groups.map((g) => {
            const { total, done } = typeStats(g.items);
            const next2 = g.items.filter((c) => c.state !== 'done').sort((a, b) => b.prog.percent - a.prog.percent)[0];
            return (
              <button key={g.tid} onClick={() => onPick(g.tid)} style={{ textAlign: 'left', cursor: 'pointer', background: INV.panel, border: `1px solid ${INV.b1}`, borderLeft: `2px solid ${hexA(g.meta.accent, 0.6)}`, borderRadius: 4, padding: '13px 15px' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <TypeHeader tid={g.tid} meta={g.meta} items={g.items} />
                </div>
                <div style={{ marginTop: 11 }}>
                  {next2 ? <Reward reward={next2.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="chip" /> : <MonoLabel color={INV.success} style={{ fontSize: 9 }}>All unlocked ✓</MonoLabel>}
                </div>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}

function ChallengeRail({ tweaks }) {
  const all = useChallenges2();
  const layer = useRewardLayer();
  const [sel, setSel] = React.useState('all');
  const groups = groupByType(all);
  const selGroup = sel === 'all' ? null : groups.find((g) => g.tid === sel);
  const detail = selGroup ? sortChallenges(selGroup.items, tweaks.sort) : [];

  return (
    <ChalFrame frameRef={layer.frameRef}>
      <ChalHeader list={all} />
      <div style={{ flex: 1, minHeight: 0, display: 'flex', padding: '0 30px 8px' }}>
        <div style={{ width: 224, flexShrink: 0, borderRight: `1px solid ${INV.b1}`, paddingRight: 18, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 4 }}>
          <button onClick={() => setSel('all')}
            style={{ display: 'flex', alignItems: 'center', gap: 11, width: '100%', textAlign: 'left', cursor: 'pointer', padding: '9px 11px', borderRadius: 3, border: `1px solid ${sel === 'all' ? INV.b2 : 'transparent'}`, background: sel === 'all' ? 'rgba(255,255,255,0.06)' : 'transparent', marginBottom: 4 }}>
            <div style={{ width: 26, height: 26, flexShrink: 0, borderRadius: 3, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(255,255,255,0.06)', border: `1px solid ${INV.b2}` }}>
              <Diamond size={6} color={INV.accent} />
            </div>
            <span style={{ fontSize: 13, color: sel === 'all' ? INV.text : INV.t2 }}>Overview</span>
          </button>
          {groups.map((g) => <RailItem key={g.tid} tid={g.tid} meta={g.meta} items={g.items} active={sel === g.tid} onClick={() => setSel(g.tid)} />)}
        </div>
        <div style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: '4px 0 24px 22px' }}>
          {sel === 'all'
            ? <OverviewPane all={all} layer={layer} tweaks={tweaks} onPick={setSel} />
            : (
              <div>
                <div style={{ marginBottom: 14 }}>
                  <TypeHeader tid={selGroup.tid} meta={selGroup.meta} items={selGroup.items} size="lg" />
                  <div style={{ fontSize: 12, color: INV.t3, marginTop: 8 }}>{selGroup.meta.blurb}</div>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: tweaks.density === 'compact' ? '1fr 1fr' : '1fr', gap: 12 }}>
                  {detail.map((c) => <DetailCard key={c.id} c={c} layer={layer} tweaks={tweaks} />)}
                </div>
              </div>
            )}
        </div>
      </div>
      <RewardLayer layer={layer} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeRail, RailItem, DetailCard, OverviewPane });
