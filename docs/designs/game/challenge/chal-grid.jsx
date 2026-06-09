/* chal-grid.jsx — Direction B · "Reward-forward cards"

   A grid of cards that put the PRIZE up front. Each card ends in a reward
   strip: a rarity-bordered icon tile (reads exactly like an inventory slot —
   glow + category/mod glyph) beside the reward name. Hovering the strip opens
   the full tooltip. You can scan rewards by their rarity color + icon before
   reading a word.
*/

function RewardStrip({ reward, bind, tweaks }) {
  const [h, setH] = React.useState(false);
  if (!reward) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 12px', border: `1px dashed ${INV.b2}`, borderRadius: 3 }}>
        <div style={{ width: tweaks.iconSize, height: tweaks.iconSize, flexShrink: 0 }} />
        <span style={{ fontSize: 12, color: INV.t4, fontStyle: 'italic' }}>No reward</span>
      </div>
    );
  }
  const b = bind(reward);
  const sz = tweaks.iconSize;
  const r = reward.kind === 'item' ? RARITY[reward.rarity] : null;
  const glowAmt = tweaks.rarityGlow ? (r ? 5 + r.glow * 16 : 9) : 0;
  return (
    <div
      onMouseEnter={(e) => { setH(true); b.onMouseEnter(e); }}
      onMouseLeave={(e) => { setH(false); b.onMouseLeave(e); }}
      style={{
        display: 'flex', alignItems: 'center', gap: 13, padding: '10px 12px', cursor: 'help',
        background: hexA(reward.accent, h ? 0.1 : 0.05), borderRadius: 3,
        border: `1px solid ${hexA(reward.accent, h ? 0.6 : 0.28)}`,
        transition: 'all 130ms',
      }}>
      <div style={{
        width: sz, height: sz, flexShrink: 0, borderRadius: 3,
        background: hexA(reward.accent, 0.08),
        border: `1px solid ${hexA(reward.accent, 0.55)}`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        boxShadow: glowAmt ? `0 0 ${glowAmt}px ${hexA(reward.accent, 0.5)}` : 'none',
        transition: 'box-shadow 130ms',
      }}>
        <RewardGlyph reward={reward} size={Math.round(sz * 0.5)} />
      </div>
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{ marginBottom: 3 }}>
          <MonoLabel color={hexA(reward.accent, 0.7)} style={{ fontSize: 8 }}>Reward</MonoLabel>
        </div>
        <div style={{
          fontSize: 14, color: reward.accent, letterSpacing: 0.1,
          whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
          textShadow: tweaks.rarityGlow && h ? `0 0 12px ${hexA(reward.accent, 0.6)}` : 'none',
        }}>{reward.name}</div>
        <div style={{ marginTop: 2 }}>
          <MonoLabel color={INV.t4} style={{ fontSize: 8 }}>{reward.sub}</MonoLabel>
        </div>
      </div>
    </div>
  );
}

function ChallengeGridCard({ c, layer, tweaks }) {
  const done = c.state === 'done';
  return (
    <div style={{
      display: 'flex', flexDirection: 'column', gap: 11,
      background: done ? hexA(INV.success, 0.04) : INV.panel,
      border: `1px solid ${done ? hexA(INV.success, 0.3) : INV.b1}`,
      borderRadius: 4, padding: '16px 18px',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <TypeTag typeId={c.typeId} />
        <StatusPill state={c.state} accent={c.typeAccent} />
      </div>

      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <StatusNode state={c.state} accent={c.typeAccent} size={20} />
          <span style={{ fontSize: 17, color: done ? INV.t2 : INV.text, letterSpacing: -0.2 }}>{c.name}</span>
        </div>
        <div style={{ fontSize: 12, color: INV.t3, marginTop: 6, lineHeight: 1.5, minHeight: 34 }}>{c.description}</div>
      </div>

      <div>
        <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 6 }}>
          <ProgressCount progress={c.progress} target={c.targetCount} unit={c.unit} done={done} />
          {done && <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{c.completedAt}</MonoLabel>}
        </div>
        <ProgressBar percent={c.percent} accent={c.typeAccent} done={done} />
      </div>

      <div style={{ marginTop: 'auto', paddingTop: 3 }}>
        <RewardStrip reward={c.reward} bind={layer.bind} tweaks={tweaks} />
      </div>
    </div>
  );
}

function ChallengeGrid({ tweaks }) {
  const all = useChallenges();
  const layer = useRewardLayer();
  const [filter, setFilter] = React.useState('all');
  const counts = challengeSummary(all);
  const list = filterChallenges(all, filter);

  return (
    <ChalFrame frameRef={layer.frameRef}>
      <ChalHeader list={all} right={
        <ChalFilter value={filter} onChange={setFilter} counts={{ all: counts.total, active: counts.active, done: counts.done }} />
      } />
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '4px 32px 28px' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          {list.map((c) => <ChallengeGridCard key={c.id} c={c} layer={layer} tweaks={tweaks} />)}
        </div>
      </div>
      <RewardLayer layer={layer} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeGrid, ChallengeGridCard, RewardStrip });
