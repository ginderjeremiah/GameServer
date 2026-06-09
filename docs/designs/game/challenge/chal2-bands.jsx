/* chal2-bands.jsx — Direction A · "Type Bands"

   ChallengeType drives the page as a vertical stack of titled bands. Each band
   is one type (glyph + accent + completion), and its challenges are rows with
   progress + the sealed/revealed reward on the right. Closest to a familiar
   list, but the type is now the spine the whole page hangs on.
*/

function BandRow({ c, layer, tweaks }) {
  const done = c.state === 'done';
  const pad = tweaks.density === 'compact' ? '10px 0' : '14px 0';
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 16, padding: pad, borderTop: `1px solid ${INV.b1}`, opacity: done ? 0.82 : 1 }}>
      <StatusNode state={c.state} accent={c.typeAccent} size={22} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <span style={{ fontSize: 14.5, color: done ? INV.t2 : INV.text, letterSpacing: -0.1 }}>{c.name}</span>
          {c.target && <span style={{ fontFamily: INV.mono, fontSize: 9, letterSpacing: 0.5, color: hexA(c.typeAccent, 0.85), border: `1px solid ${hexA(c.typeAccent, 0.3)}`, borderRadius: 2, padding: '1px 6px' }}>{c.target}</span>}
          {done && <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>· {c.completedAt}</MonoLabel>}
        </div>
        {tweaks.density !== 'compact' && <div style={{ fontSize: 12, color: INV.t3, marginTop: 3, maxWidth: 380 }}>{c.desc}</div>}
      </div>
      <div style={{ width: 184, flexShrink: 0 }}>
        <ProgressReadout c={c} />
      </div>
      <div style={{ width: 196, flexShrink: 0, display: 'flex', justifyContent: 'flex-end' }}>
        <Reward reward={c.reward} layer={layer} mystery={tweaks.mystery} glow={tweaks.glow} variant="link" />
      </div>
    </div>
  );
}

function Band({ tid, meta, items, layer, tweaks }) {
  const sorted = sortChallenges(items, tweaks.sort);
  return (
    <div style={{ marginBottom: 26 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 10 }}>
        <TypeHeader tid={tid} meta={meta} items={items} />
        <div style={{ flex: 1, height: 1, background: `linear-gradient(90deg, ${hexA(meta.accent, 0.3)}, transparent)` }} />
      </div>
      <div style={{ paddingLeft: 2 }}>
        {sorted.map((c) => <BandRow key={c.id} c={c} layer={layer} tweaks={tweaks} />)}
      </div>
    </div>
  );
}

function sortChallenges(items, sort) {
  const rank = { active: 0, locked: 1, done: 2 };
  const arr = items.slice();
  if (sort === 'progress') return arr.sort((a, b) => rank[a.state] - rank[b.state] || b.prog.percent - a.prog.percent);
  if (sort === 'rarity') return arr.sort((a, b) => (b.reward ? RARITY[b.reward.rarity].level : 0) - (a.reward ? RARITY[a.reward.rarity].level : 0));
  return arr.sort((a, b) => a.id - b.id);
}

function ChallengeBands({ tweaks }) {
  const all = useChallenges2();
  const layer = useRewardLayer();
  const groups = groupByType(all);
  return (
    <ChalFrame frameRef={layer.frameRef}>
      <ChalHeader list={all} sub="Organised by challenge type — hover an unlocked reward to inspect it." />
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '6px 30px 28px' }}>
        {groups.map((g) => <Band key={g.tid} tid={g.tid} meta={g.meta} items={g.items} layer={layer} tweaks={tweaks} />)}
      </div>
      <RewardLayer layer={layer} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeBands, Band, BandRow, sortChallenges });
