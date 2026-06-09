/* chal-ledger.jsx — Direction A · "Ledger"

   A refined, scannable list — closest to the original screen's shape but in
   the game's visual language. The reward is a highlighted, rarity-aware text
   LINK (dotted underline, brightens on hover) that reveals the full item / mod
   tooltip. This is the literal read of the brief: name → link → tooltip.
*/

function RewardLink({ reward, bind, glow, showSub }) {
  const [h, setH] = React.useState(false);
  if (!reward) return <span style={{ fontSize: 13, color: INV.t4, fontStyle: 'italic' }}>—</span>;
  const b = bind(reward);
  return (
    <span
      onMouseEnter={(e) => { setH(true); b.onMouseEnter(e); }}
      onMouseLeave={(e) => { setH(false); b.onMouseLeave(e); }}
      style={{ display: 'inline-flex', alignItems: 'center', gap: 8, cursor: 'help', justifyContent: 'flex-end' }}>
      <span style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 16, flexShrink: 0 }}>
        <RewardGlyph reward={reward} size={15} />
      </span>
      <span style={{ minWidth: 0 }}>
        <span style={{
          fontSize: 13.5, color: reward.accent, letterSpacing: 0.1,
          borderBottom: `1px dashed ${hexA(reward.accent, h ? 0.95 : 0.45)}`,
          paddingBottom: 1, textShadow: glow && h ? `0 0 12px ${hexA(reward.accent, 0.7)}` : 'none',
          transition: 'all 120ms', whiteSpace: 'nowrap',
        }}>{reward.name}</span>
        {showSub && (
          <span style={{ display: 'block', marginTop: 3 }}>
            <MonoLabel color={INV.t4} style={{ fontSize: 8 }}>{reward.sub}</MonoLabel>
          </span>
        )}
      </span>
    </span>
  );
}

function LedgerRow({ c, layer, tweaks }) {
  const [hover, setHover] = React.useState(false);
  const done = c.state === 'done';
  const pad = tweaks.density === 'compact' ? '11px 0' : '16px 0';
  return (
    <div
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 18, padding: pad,
        borderBottom: `1px solid ${INV.b1}`, opacity: done ? 0.78 : 1,
        transition: 'opacity 120ms',
      }}>
      <StatusNode state={c.state} accent={c.typeAccent} size={24} />

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: tweaks.density === 'compact' ? 2 : 4 }}>
          <TypeTag typeId={c.typeId} />
          {done && <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>· {c.completedAt}</MonoLabel>}
        </div>
        <div style={{ fontSize: 15.5, color: done ? INV.t2 : INV.text, letterSpacing: -0.1, textDecoration: done ? 'none' : 'none' }}>{c.name}</div>
        {tweaks.density !== 'compact' && (
          <div style={{ fontSize: 12, color: INV.t3, marginTop: 2, maxWidth: 360 }}>{c.description}</div>
        )}
      </div>

      <div style={{ width: 176, flexShrink: 0 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 6 }}>
          <ProgressCount progress={c.progress} target={c.targetCount} unit={c.unit} done={done} />
        </div>
        <ProgressBar percent={c.percent} accent={c.typeAccent} done={done} />
      </div>

      <div style={{ width: 188, flexShrink: 0, textAlign: 'right' }}>
        <div style={{ marginBottom: 5 }}>
          <MonoLabel color={hexA(c.reward ? c.reward.accent : INV.t4, 0.7)} style={{ fontSize: 8.5 }}>
            {done ? 'Earned' : 'Reward'}
          </MonoLabel>
        </div>
        <RewardLink reward={c.reward} bind={layer.bind} glow={tweaks.rarityGlow} showSub={tweaks.rewardSub} />
      </div>
    </div>
  );
}

function ChallengeLedger({ tweaks }) {
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
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '0 32px 24px' }}>
        <div style={{ borderTop: `1px solid ${INV.b1}` }}>
          {list.map((c) => <LedgerRow key={c.id} c={c} layer={layer} tweaks={tweaks} />)}
        </div>
        <div style={{ marginTop: 14, display: 'flex', justifyContent: 'center' }}>
          <MonoLabel color={INV.t4} style={{ fontSize: 9 }}>Hover a reward to inspect the item or mod</MonoLabel>
        </div>
      </div>
      <RewardLayer layer={layer} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeLedger, RewardLink, LedgerRow });
