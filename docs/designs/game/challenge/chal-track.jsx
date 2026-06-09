/* chal-track.jsx — Direction C · "Tracks"

   Challenges organised into tracks by type, with a summary rail on the left.
   Rewards are inline chips (rarity-tinted pills) that open the tooltip on
   hover — compact, so a long list of challenges stays dense and scannable.
   The rail surfaces overall progress, a per-type breakdown, and a "Next up"
   card spotlighting the reward you're closest to earning.
*/

function RewardChip({ reward, bind, glow, size = 'sm' }) {
  const [h, setH] = React.useState(false);
  if (!reward) return <span style={{ fontSize: 12, color: INV.t4, fontStyle: 'italic' }}>—</span>;
  const b = bind(reward);
  const pad = size === 'lg' ? '6px 12px' : '4px 10px';
  const fs = size === 'lg' ? 13 : 12;
  return (
    <span
      onMouseEnter={(e) => { setH(true); b.onMouseEnter(e); }}
      onMouseLeave={(e) => { setH(false); b.onMouseLeave(e); }}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 7, padding: pad, cursor: 'help',
        background: hexA(reward.accent, h ? 0.16 : 0.08), borderRadius: 2,
        border: `1px solid ${hexA(reward.accent, h ? 0.6 : 0.32)}`,
        boxShadow: glow && h ? `0 0 12px ${hexA(reward.accent, 0.4)}` : 'none',
        transition: 'all 120ms', maxWidth: '100%',
      }}>
      <RewardGlyph reward={reward} size={size === 'lg' ? 15 : 13} />
      <span style={{ fontSize: fs, color: reward.accent, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', letterSpacing: 0.1 }}>{reward.name}</span>
    </span>
  );
}

function TrackRow({ c, layer, tweaks }) {
  const done = c.state === 'done';
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 16, padding: tweaks.density === 'compact' ? '8px 0' : '11px 0',
      borderBottom: `1px solid ${INV.b1}`, opacity: done ? 0.78 : 1,
    }}>
      <StatusNode state={c.state} accent={c.typeAccent} size={20} />
      <div style={{ width: 188, flexShrink: 0, minWidth: 0 }}>
        <div style={{ fontSize: 14, color: done ? INV.t2 : INV.text, letterSpacing: -0.1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.name}</div>
      </div>
      <div style={{ flex: 1, minWidth: 80, display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ flex: 1 }}><ProgressBar percent={c.percent} accent={c.typeAccent} done={done} height={4} /></div>
        <div style={{ width: 116, flexShrink: 0, textAlign: 'right' }}>
          <ProgressCount progress={c.progress} target={c.targetCount} unit={c.unit} done={done} />
        </div>
      </div>
      <div style={{ width: 178, flexShrink: 0, display: 'flex', justifyContent: 'flex-end' }}>
        <RewardChip reward={c.reward} bind={layer.bind} glow={tweaks.rarityGlow} />
      </div>
    </div>
  );
}

function TrackGroup({ typeId, items, layer, tweaks }) {
  const acc = CHALLENGE_TYPE_ACCENT[typeId];
  const done = items.filter((c) => c.state === 'done').length;
  return (
    <div style={{ marginBottom: 22 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
        <ChalGlyph typeId={typeId} color={acc} size={15} />
        <MonoLabel color={acc} style={{ fontSize: 10 }}>{CHALLENGE_TYPES[typeId].label}</MonoLabel>
        <div style={{ flex: 1, height: 1, background: INV.b1 }} />
        <MonoLabel color={INV.t4} style={{ fontSize: 9 }}>{done}/{items.length}</MonoLabel>
      </div>
      <div>
        {items.map((c) => <TrackRow key={c.id} c={c} layer={layer} tweaks={tweaks} />)}
      </div>
    </div>
  );
}

function TrackRail({ all, layer }) {
  const s = challengeSummary(all);
  const pct = Math.round((s.done / Math.max(1, s.total)) * 100);
  const next = all.filter((c) => c.state === 'active').sort((a, b) => b.percent - a.percent)[0];
  const byType = Object.keys(CHALLENGE_TYPES).map(Number).map((tid) => {
    const items = all.filter((c) => c.typeId === tid);
    return { tid, done: items.filter((c) => c.state === 'done').length, total: items.length };
  });

  return (
    <div style={{
      width: 250, flexShrink: 0, borderRight: `1px solid ${INV.b1}`,
      padding: '4px 24px 24px 0', display: 'flex', flexDirection: 'column', gap: 22,
    }}>
      <div>
        <MonoLabel color={INV.t4}>Overall</MonoLabel>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 7, margin: '8px 0 10px' }}>
          <span style={{ fontFamily: INV.mono, fontSize: 34, color: INV.success, lineHeight: 1 }}>{s.done}</span>
          <span style={{ fontFamily: INV.mono, fontSize: 15, color: INV.t4 }}>/ {s.total}</span>
        </div>
        <ProgressBar percent={pct} accent={INV.accent} done={false} height={6} />
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 7 }}>
          <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{s.active} active</MonoLabel>
          <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>{pct}% done</MonoLabel>
        </div>
      </div>

      {next && next.reward && (
        <div>
          <MonoLabel color={INV.t4}>Next up</MonoLabel>
          <div style={{
            marginTop: 8, padding: '13px 14px', borderRadius: 3,
            background: hexA(next.reward.accent, 0.05), border: `1px solid ${hexA(next.reward.accent, 0.28)}`,
          }}>
            <div style={{ fontSize: 13.5, color: INV.text, marginBottom: 3 }}>{next.name}</div>
            <div style={{ marginBottom: 10 }}>
              <ProgressCount progress={next.progress} target={next.targetCount} unit={next.unit} done={false} />
            </div>
            <MonoLabel color={hexA(next.reward.accent, 0.7)} style={{ fontSize: 8 }}>Unlocks</MonoLabel>
            <div style={{ marginTop: 6 }}>
              <RewardChip reward={next.reward} bind={layer.bind} glow size="lg" />
            </div>
          </div>
        </div>
      )}

      <div>
        <MonoLabel color={INV.t4}>By track</MonoLabel>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 9, marginTop: 10 }}>
          {byType.map(({ tid, done, total }) => (
            <div key={tid} style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
              <ChalGlyph typeId={tid} color={CHALLENGE_TYPE_ACCENT[tid]} size={13} />
              <span style={{ fontSize: 12, color: INV.t2, flex: 1 }}>{CHALLENGE_TYPES[tid].label}</span>
              <span style={{ fontFamily: INV.mono, fontSize: 10.5, color: done === total ? INV.success : INV.t3 }}>{done}/{total}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ChallengeTracks({ tweaks }) {
  const all = useChallenges();
  const layer = useRewardLayer();
  const typeIds = Object.keys(CHALLENGE_TYPES).map(Number).filter((tid) => all.some((c) => c.typeId === tid));
  const sortInGroup = (a, b) => {
    const rank = { active: 0, locked: 1, done: 2 };
    return rank[a.state] - rank[b.state] || b.percent - a.percent;
  };

  return (
    <ChalFrame frameRef={layer.frameRef}>
      <ChalHeader list={all} />
      <div style={{ flex: 1, minHeight: 0, display: 'flex', padding: '0 32px 8px' }}>
        <TrackRail all={all} layer={layer} />
        <div style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: '4px 0 24px 26px' }}>
          {typeIds.map((tid) => (
            <TrackGroup key={tid} typeId={tid} layer={layer} tweaks={tweaks}
              items={all.filter((c) => c.typeId === tid).slice().sort(sortInGroup)} />
          ))}
        </div>
      </div>
      <RewardLayer layer={layer} />
    </ChalFrame>
  );
}

Object.assign(window, { ChallengeTracks, RewardChip, TrackRow, TrackGroup, TrackRail });
