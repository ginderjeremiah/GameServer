/* chal2-boards.jsx — Direction B · "Collection Boards"

   Each ChallengeType is its own panel ("board") arranged in a grid — like a
   set of collections you're filling out. A board shows the type identity, a
   completion meter, then its challenges with reward TILES (sealed → revealed).
   Reads as "X of Y unlocked in this category", which suits the unlock fiction.
*/

function BoardChallenge({ c, layer, tweaks, last }) {
  const done = c.state === 'done';
  return (
    <div style={{ padding: '11px 0', borderTop: last ? 'none' : `1px solid ${INV.b1}` }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 9, marginBottom: 8 }}>
        <StatusNode state={c.state} accent={c.typeAccent} size={18} />
        <span style={{ fontSize: 13.5, color: done ? INV.t2 : INV.text, letterSpacing: -0.1 }}>{c.name}</span>
        {c.target && <span style={{ fontFamily: INV.mono, fontSize: 8.5, letterSpacing: 0.5, color: hexA(c.typeAccent, 0.85), border: `1px solid ${hexA(c.typeAccent, 0.3)}`, borderRadius: 2, padding: '1px 5px' }}>{c.target}</span>}
        <div style={{ flex: 1 }} />
        <div style={{ width: 140, flexShrink: 0 }}><ProgressReadout c={c} showBar barHeight={4} /></div>
      </div>
      <Reward reward={c.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="tile" />
    </div>
  );
}

function Board({ tid, meta, items, layer, tweaks }) {
  const { total, done } = typeStats(items);
  const pct = Math.round((done / Math.max(1, total)) * 100);
  const sorted = sortChallenges(items, tweaks.sort);
  const acc = meta.accent;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', background: INV.panel, border: `1px solid ${INV.b1}`, borderTop: `2px solid ${hexA(acc, 0.6)}`, borderRadius: 4, padding: '16px 18px 14px' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12, marginBottom: 12 }}>
        <TypeHeader tid={tid} meta={meta} items={items} size="lg" />
        <div style={{ textAlign: 'right', flexShrink: 0 }}>
          <div style={{ fontFamily: INV.mono, fontSize: 11, color: done === total ? INV.success : hexA(acc, 0.9) }}>{pct}%</div>
        </div>
      </div>
      <div style={{ marginBottom: 6 }}>
        <Bar percent={pct} accent={acc} done={done === total} height={4} />
      </div>
      <div style={{ fontSize: 11.5, color: INV.t3, marginBottom: 6, lineHeight: 1.45 }}>{meta.blurb}</div>
      <div>
        {sorted.map((c, i) => <BoardChallenge key={c.id} c={c} layer={layer} tweaks={tweaks} last={i === 0} />)}
      </div>
    </div>
  );
}

function ChallengeBoards({ tweaks }) {
  const all = useChallenges2();
  const layer = useRewardLayer();
  const groups = groupByType(all);
  const cols = tweaks.density === 'compact' ? 3 : 2;
  return (
    <ChalFrame frameRef={layer.frameRef}>
      <ChalHeader list={all} sub="Each challenge type is a collection — fill it out to unlock its rewards." />
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '6px 30px 28px' }}>
        <div style={{ display: 'grid', gridTemplateColumns: `repeat(${cols}, 1fr)`, gap: 14, alignItems: 'start' }}>
          {groups.map((g) => <Board key={g.tid} tid={g.tid} meta={g.meta} items={g.items} layer={layer} tweaks={tweaks} />)}
        </div>
      </div>
      <RewardLayer layer={layer} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeBoards, Board, BoardChallenge });
