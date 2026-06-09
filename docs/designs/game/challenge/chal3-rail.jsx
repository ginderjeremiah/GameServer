/* chal3-rail.jsx — refined Direction C · "Type Rail" (v3)

   ChallengeType is the navigation. Left rail = the 8 types with ring meters;
   selecting one swaps the detail pane to a hero banner + that type's challenge
   cards, with an in-page sort control. "Overview" shows overall progress, a
   featured "next up" reward, and a per-type grid.
*/

function RailButton({ tid, meta, items, active, onClick }) {
  const { total, done } = typeStats(items);
  const pct = (done / Math.max(1, total)) * 100;
  const complete = done === total;
  const acc = meta.accent;
  return (
    <button onClick={onClick}
      style={{
        display: 'flex', alignItems: 'center', gap: 11, width: '100%', textAlign: 'left', cursor: 'pointer',
        padding: '8px 10px', borderRadius: 3, border: `1px solid ${active ? hexA(acc, 0.5) : 'transparent'}`,
        background: active ? hexA(acc, 0.12) : 'transparent', transition: 'all 120ms',
        boxShadow: active ? `inset 2px 0 0 ${acc}` : 'none',
      }}>
      <RingMeter pct={pct} accent={acc} size={30} stroke={3} done={complete}>
        <TypeGlyph typeId={tid} color={complete ? INV.success : acc} size={13} />
      </RingMeter>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontSize: 13, color: active ? INV.text : INV.t2, letterSpacing: -0.1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{meta.label}</div>
        <MonoLabel color={INV.t4} style={{ fontSize: 8 }}>{meta.unit}</MonoLabel>
      </div>
      <span style={{ fontFamily: INV.mono, fontSize: 10, color: complete ? INV.success : INV.t3, flexShrink: 0 }}>{done}/{total}</span>
    </button>
  );
}

function DetailCard({ c, layer, tweaks }) {
  const done = c.state === 'done';
  return (
    <div style={{
      background: done ? hexA(INV.success, 0.045) : INV.panel,
      border: `1px solid ${done ? hexA(INV.success, 0.28) : INV.b1}`,
      borderRadius: 4, padding: '15px 17px', display: 'flex', flexDirection: 'column', gap: 12,
    }}>
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
      <Reward3 reward={c.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="tile" />
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
        <div style={{ flex: 1, background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, padding: '17px 19px' }}>
          <MonoLabel color={INV.t4}>Overall progress</MonoLabel>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, margin: '9px 0 11px' }}>
            <span style={{ fontFamily: INV.mono, fontSize: 32, color: INV.success, lineHeight: 1 }}>{s.done}</span>
            <span style={{ fontFamily: INV.mono, fontSize: 14, color: INV.t4 }}>/ {s.total} rewards unlocked</span>
          </div>
          <Bar percent={s.pct} accent={INV.accent} done={false} height={6} />
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8 }}>
            <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{s.active} in progress</MonoLabel>
            <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{s.pct}% complete</MonoLabel>
          </div>
        </div>
        {next && next.reward && (
          <div style={{ width: 300, flexShrink: 0, position: 'relative', overflow: 'hidden', background: `linear-gradient(135deg, ${hexA(next.reward.accent, 0.12)}, transparent 70%)`, border: `1px solid ${hexA(next.reward.accent, 0.3)}`, borderRadius: 4, padding: '17px 19px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 9 }}>
              <span className="chal-dot" style={{ width: 6, height: 6, borderRadius: '50%', background: next.reward.accent, '--acc': next.reward.accent }} />
              <MonoLabel color={hexA(next.reward.accent, 0.85)}>Next up · closest to unlock</MonoLabel>
            </div>
            <div style={{ fontSize: 16, color: INV.text, marginBottom: 5, letterSpacing: -0.2 }}>{next.name}</div>
            <div style={{ marginBottom: 12 }}><ProgressReadout c={next} /></div>
            <Reward3 reward={next.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="tile" />
          </div>
        )}
      </div>
      <div>
        <div style={{ marginBottom: 11 }}><MonoLabel color={INV.t4}>All challenge types</MonoLabel></div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          {groups.map((g) => {
            const { total, done } = typeStats(g.items);
            const pct = (done / Math.max(1, total)) * 100;
            const complete = done === total;
            const next2 = sortChallenges3(g.items.filter((c) => c.state !== 'done'), 'progress')[0];
            return (
              <button key={g.tid} onClick={() => onPick(g.tid)}
                style={{ textAlign: 'left', cursor: 'pointer', background: INV.panel, border: `1px solid ${INV.b1}`, borderLeft: `2px solid ${hexA(g.meta.accent, 0.6)}`, borderRadius: 4, padding: '13px 15px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                  <RingMeter pct={pct} accent={g.meta.accent} size={34} stroke={3} done={complete}>
                    <TypeGlyph typeId={g.tid} color={complete ? INV.success : g.meta.accent} size={15} />
                  </RingMeter>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 14, color: INV.text, letterSpacing: -0.1 }}>{g.meta.label}</div>
                    <MonoLabel color={done === total ? INV.success : INV.t4} style={{ fontSize: 8.5 }}>{done}/{total} unlocked</MonoLabel>
                  </div>
                </div>
                <div style={{ marginTop: 11 }}>
                  {next2 ? <Reward3 reward={next2.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="chip" /> : <MonoLabel color={INV.success} style={{ fontSize: 9 }}>All unlocked ✓</MonoLabel>}
                </div>
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}

function ChallengeRail3({ tweaks }) {
  const all = useChallenges2();
  const layer = useRewardLayer3();
  const [sel, setSel] = React.useState('all');
  const [sort, setSort] = React.useState('progress');
  const groups = groupByType(all);
  const selGroup = sel === 'all' ? null : groups.find((g) => g.tid === sel);
  const detail = selGroup ? sortChallenges3(selGroup.items, sort) : [];
  const cols = tweaks.density === 'compact' ? '1fr 1fr' : '1fr';

  return (
    <ChalFrame frameRef={layer.frameRef}>
      <ChalHeader list={all} />
      <div style={{ flex: 1, minHeight: 0, display: 'flex', padding: '0 30px 8px' }}>
        <div style={{ width: 232, flexShrink: 0, borderRight: `1px solid ${INV.b1}`, paddingRight: 18, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 3 }}>
          <button onClick={() => setSel('all')}
            style={{ display: 'flex', alignItems: 'center', gap: 11, width: '100%', textAlign: 'left', cursor: 'pointer', padding: '8px 10px', borderRadius: 3, border: `1px solid ${sel === 'all' ? INV.b2 : 'transparent'}`, background: sel === 'all' ? 'rgba(255,255,255,0.06)' : 'transparent', marginBottom: 5 }}>
            <div style={{ width: 30, height: 30, flexShrink: 0, borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(255,255,255,0.05)', border: `1px solid ${INV.b2}` }}>
              <Diamond size={7} color={INV.accent} />
            </div>
            <span style={{ flex: 1, fontSize: 13, color: sel === 'all' ? INV.text : INV.t2 }}>Overview</span>
          </button>
          {groups.map((g) => <RailButton key={g.tid} tid={g.tid} meta={g.meta} items={g.items} active={sel === g.tid} onClick={() => setSel(g.tid)} />)}
        </div>
        <div style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: '2px 0 24px 22px' }}>
          {sel === 'all'
            ? <OverviewPane all={all} layer={layer} tweaks={tweaks} onPick={setSel} />
            : (
              <div>
                <TypeHero tid={selGroup.tid} meta={selGroup.meta} items={selGroup.items} />
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 13 }}>
                  <MonoLabel color={INV.t4} style={{ fontSize: 9 }}>{selGroup.items.length} challenges</MonoLabel>
                  <SortControl value={sort} onChange={setSort} />
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: cols, gap: 12 }}>
                  {detail.map((c) => <DetailCard key={c.id} c={c} layer={layer} tweaks={tweaks} />)}
                </div>
              </div>
            )}
        </div>
      </div>
      <RewardLayer3 layer={layer} mystery={tweaks.mystery} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeRail3, RailButton, DetailCard, OverviewPane });
