/* chal-shared.jsx — shared mechanics + chrome for the Challenges directions.

   Depends on (loaded earlier in the HTML):
     tooltip-shared.jsx  → MOD_TYPE_LABEL, MOD_TYPE_ACCENT, statSign, statColor
     tooltip-final.jsx   → FinalItemTooltip
     inv-data.jsx        → ITEMS, MODS, RARITY
     inv-shared.jsx      → INV, hexA, rarityColor, ItemGlyph, Diamond, MonoLabel,
                            SectionRule, RarityDot, toTooltipItem
     chal-data.jsx       → CHALLENGES, PLAYER_PROGRESS, CHALLENGE_TYPES,
                            CHALLENGE_TYPE_ACCENT

   The headline feature: a reward (item OR item-mod) is presented as an
   interactive, rarity-aware affordance that reveals the SAME tooltip you'd
   see inspecting that thing in the inventory. Items reuse FinalItemTooltip
   verbatim; mods get a matching RewardModTooltip built from the same tokens.
*/

/* ─── reward resolution ─────────────────────────────────────────────── */
function resolveReward(ch) {
  if (ch.rewardItemId != null) {
    const item = ITEMS.find((i) => i.id === ch.rewardItemId);
    if (!item) return null;
    return {
      kind: 'item', item, name: item.name, cat: item.cat,
      rarity: item.rarity, accent: rarityColor(item.rarity),
      sub: (RARITY[item.rarity]?.label || '') + ' · ' + (window.ITEM_CATEGORIES?.[item.cat] || 'Item'),
    };
  }
  if (ch.rewardItemModId != null) {
    const mod = MODS[ch.rewardItemModId];
    if (!mod) return null;
    return {
      kind: 'mod', mod, name: mod.name, modType: mod.itemModTypeId,
      accent: MOD_TYPE_ACCENT[mod.itemModTypeId],
      sub: 'Item Mod · ' + MOD_TYPE_LABEL[mod.itemModTypeId],
    };
  }
  return null;
}

/* merge challenge + progress + reward + type into a render-ready view model */
function useChallenges() {
  return React.useMemo(() => CHALLENGES.map((ch) => {
    const p = PLAYER_PROGRESS[ch.id] || { progress: 0, completed: false };
    const percent = Math.min(100, (p.progress / Math.max(1, ch.targetCount)) * 100);
    const state = p.completed ? 'done' : p.progress > 0 ? 'active' : 'locked';
    return {
      ...ch, ...p, percent, state,
      reward: resolveReward(ch),
      type: CHALLENGE_TYPES[ch.typeId],
      typeAccent: CHALLENGE_TYPE_ACCENT[ch.typeId],
    };
  }), []);
}

function challengeSummary(list) {
  const total = list.length;
  const done = list.filter((c) => c.state === 'done').length;
  const active = list.filter((c) => c.state === 'active').length;
  return { total, done, active };
}

/* ─── reward hover layer (zoom-safe, scoped to a frame) ─────────────────
   useRewardLayer() gives a frame ref + a `bind(reward)` that wires hover
   handlers onto any affordance. <RewardLayer/> renders the anchored tooltip. */
function useRewardLayer() {
  const frameRef = React.useRef(null);
  const [hovered, setHovered] = React.useState(null);
  const bind = (reward) => ({
    onMouseEnter: (e) => { if (reward) setHovered({ reward, el: e.currentTarget }); },
    onMouseLeave: () => setHovered(null),
  });
  return { frameRef, hovered, bind };
}

function RewardLayer({ layer }) {
  if (!layer.hovered) return null;
  return <AnchoredRewardTooltip frameEl={layer.frameRef.current} anchorEl={layer.hovered.el} reward={layer.hovered.reward} />;
}

function AnchoredRewardTooltip({ frameEl, anchorEl, reward }) {
  const ref = React.useRef(null);
  const [pos, setPos] = React.useState(null);
  React.useLayoutEffect(() => {
    if (!frameEl || !anchorEl) { setPos(null); return; }
    const fr = frameEl.getBoundingClientRect();
    const ar = anchorEl.getBoundingClientRect();
    const scale = fr.width / frameEl.offsetWidth || 1;
    const left0 = (ar.left - fr.left) / scale;
    const top0 = (ar.top - fr.top) / scale;
    const aw = ar.width / scale, ah = ar.height / scale;
    const fw = frameEl.offsetWidth, fh = frameEl.offsetHeight;
    const ttW = 280;
    const ttH = ref.current ? ref.current.offsetHeight : 300;
    const anchorCenterX = left0 + aw / 2;
    let x = anchorCenterX > fw / 2 ? left0 - ttW - 12 : left0 + aw + 12;
    x = Math.max(8, Math.min(x, fw - ttW - 8));
    let y = top0 + ah / 2 - 44;
    y = Math.max(8, Math.min(y, Math.max(8, fh - ttH - 8)));
    setPos({ x, y });
  }, [frameEl, anchorEl, reward]);
  return (
    <div ref={ref} style={{
      position: 'absolute', left: pos ? pos.x : -9999, top: pos ? pos.y : -9999,
      zIndex: 80, pointerEvents: 'none', opacity: pos ? 1 : 0,
    }}>
      {reward.kind === 'item'
        ? <FinalItemTooltip item={toTooltipItem(reward.item)} />
        : <RewardModTooltip mod={reward.mod} />}
    </div>
  );
}

/* ─── reward mod tooltip (matches FinalItemTooltip chrome) ─────────────── */
function ttShell(accent) {
  return {
    width: 280, background: 'rgba(20,21,27,0.96)',
    border: '1px solid rgba(255,255,255,0.14)', borderLeft: `3px solid ${accent}`,
    borderRadius: 3,
    boxShadow: `0 12px 28px rgba(0,0,0,0.55), 0 0 0 1px rgba(0,0,0,0.4), -4px 0 16px ${accent}22`,
    color: '#f0f0f0', overflow: 'hidden', backdropFilter: 'blur(6px)',
  };
}
function TTSection({ label, children, last }) {
  return (
    <div style={{ marginBottom: last ? 0 : 12 }}>
      <div style={{
        fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.8, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.4)', marginBottom: 7, display: 'flex', alignItems: 'center', gap: 8,
      }}>
        {label}
        <div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.06)' }} />
      </div>
      {children}
    </div>
  );
}

function RewardModTooltip({ mod }) {
  const accent = MOD_TYPE_ACCENT[mod.itemModTypeId];
  return (
    <div style={ttShell(accent)}>
      <div style={{ padding: '14px 16px 12px', borderBottom: '1px solid rgba(240,240,240,0.08)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
          <div style={{ width: 5, height: 5, transform: 'rotate(45deg)', background: accent, boxShadow: `0 0 6px ${accent}aa` }} />
          <div style={{ fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.8, textTransform: 'uppercase', color: accent }}>
            {MOD_TYPE_LABEL[mod.itemModTypeId]} Mod
          </div>
          <div style={{ marginLeft: 'auto' }}>
            <span style={{
              fontFamily: INV.mono, fontSize: 8.5, letterSpacing: 1.2, textTransform: 'uppercase',
              color: mod.removable ? 'rgba(240,240,240,0.45)' : INV.gold,
              border: `1px solid ${mod.removable ? 'rgba(255,255,255,0.14)' : hexA(INV.gold, 0.4)}`,
              borderRadius: 2, padding: '2px 7px',
            }}>{mod.removable ? 'Removable' : 'Permanent'}</span>
          </div>
        </div>
        <div style={{ fontSize: 18, fontWeight: 400, color: '#f0f0f0', letterSpacing: -0.2, lineHeight: 1.15 }}>{mod.name}</div>
      </div>
      <div style={{ padding: '12px 16px 14px' }}>
        {mod.attributes?.length > 0 && (
          <TTSection label="Effects">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 4, columnGap: 12 }}>
              {mod.attributes.map((a) => (
                <React.Fragment key={a.name}>
                  <div style={{ fontSize: 12, color: 'rgba(240,240,240,0.78)' }}>{a.name}</div>
                  <div style={{ fontFamily: INV.mono, fontSize: 11.5, color: statColor(a.value), letterSpacing: 0.3, textAlign: 'right' }}>
                    {statSign(a.value, a.suffix || '')}
                  </div>
                </React.Fragment>
              ))}
            </div>
          </TTSection>
        )}
        {mod.description && (
          <TTSection label="Description" last>
            <div style={{ fontSize: 11.5, fontStyle: 'italic', color: 'rgba(240,240,240,0.6)', lineHeight: 1.55 }}>{mod.description}</div>
          </TTSection>
        )}
      </div>
    </div>
  );
}

/* ─── progress + status chrome ──────────────────────────────────────── */
function ProgressBar({ percent, accent, done, height = 5, showCap = true }) {
  const col = done ? INV.success : accent;
  return (
    <div style={{
      position: 'relative', height, borderRadius: height / 2, overflow: 'hidden',
      background: 'rgba(255,255,255,0.07)',
    }}>
      <div style={{
        position: 'absolute', inset: 0, width: `${percent}%`,
        background: done
          ? `linear-gradient(90deg, ${hexA(col, 0.85)}, ${hexA(col, 0.55)})`
          : `linear-gradient(90deg, ${hexA(col, 0.85)}, ${hexA(col, 0.45)})`,
        boxShadow: `0 0 8px ${hexA(col, 0.5)}`, transition: 'width 300ms ease',
      }} />
      {showCap && !done && percent > 3 && percent < 99 && (
        <div style={{
          position: 'absolute', top: -1, bottom: -1, left: `calc(${percent}% - 1px)`,
          width: 2, background: hexA(col, 0.95), boxShadow: `0 0 6px ${col}`,
        }} />
      )}
    </div>
  );
}

function ProgressCount({ progress, target, unit, accent, done }) {
  return (
    <span style={{ fontFamily: INV.mono, fontSize: 11, letterSpacing: 0.3, color: done ? INV.success : INV.t3 }}>
      <span style={{ color: done ? INV.success : INV.text }}>{progress}</span>
      <span style={{ opacity: 0.6 }}> / {target}</span>
      {unit && <span style={{ color: INV.t4 }}> {unit}</span>}
    </span>
  );
}

/* status node — a diamond that fills as: locked (hollow) → active (accent) →
   done (green check). Sits at the start of a row/card. */
function StatusNode({ state, accent, size = 22 }) {
  const col = state === 'done' ? INV.success : state === 'active' ? accent : INV.t4;
  return (
    <div style={{
      width: size, height: size, flexShrink: 0, position: 'relative',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }}>
      <div style={{
        position: 'absolute', inset: 0, transform: 'rotate(45deg)',
        border: `1px solid ${state === 'locked' ? INV.b2 : hexA(col, 0.7)}`,
        background: state === 'done' ? hexA(col, 0.16) : state === 'active' ? hexA(col, 0.1) : 'transparent',
        boxShadow: state === 'locked' ? 'none' : `0 0 8px ${hexA(col, 0.45)}`,
        borderRadius: 2,
      }} />
      {state === 'done' && (
        <svg width={size * 0.5} height={size * 0.5} viewBox="0 0 16 16" fill="none" stroke={col} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ position: 'relative' }}>
          <path d="M3 8.5l3.5 3.5L13 4.5" />
        </svg>
      )}
      {state !== 'done' && (
        <div style={{
          width: 5, height: 5, transform: 'rotate(45deg)', position: 'relative',
          background: state === 'active' ? col : 'transparent',
          border: state === 'locked' ? `1px solid ${INV.t4}` : 'none',
        }} />
      )}
    </div>
  );
}

function StatusPill({ state, accent }) {
  const map = {
    done:   { label: 'Complete', col: INV.success },
    active: { label: 'In progress', col: accent },
    locked: { label: 'Not started', col: INV.t4 },
  };
  const { label, col } = map[state];
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <span style={{
        width: 6, height: 6, borderRadius: '50%', background: state === 'locked' ? 'transparent' : col,
        border: state === 'locked' ? `1px solid ${col}` : 'none',
        boxShadow: state === 'locked' ? 'none' : `0 0 6px ${hexA(col, 0.8)}`,
      }} />
      <MonoLabel color={col} style={{ fontSize: 9 }}>{label}</MonoLabel>
    </span>
  );
}

/* type tag + glyph */
function ChalGlyph({ typeId, color, size = 16 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="none" stroke={color} strokeWidth="1.2" strokeLinejoin="round" strokeLinecap="round">
      <path d={CHALLENGE_TYPES[typeId].glyph} />
    </svg>
  );
}
function TypeTag({ typeId }) {
  const acc = CHALLENGE_TYPE_ACCENT[typeId];
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <Diamond size={5} color={acc} />
      <MonoLabel color={acc} style={{ fontSize: 9 }}>{CHALLENGE_TYPES[typeId].label}</MonoLabel>
    </span>
  );
}

/* small reward glyph used inside tiles/chips: item → category glyph in rarity
   color; mod → diamond in mod-type color. */
function RewardGlyph({ reward, size = 22 }) {
  if (reward.kind === 'item') return <ItemGlyph cat={reward.cat} color={reward.accent} size={size} />;
  return <div style={{ width: size * 0.62, height: size * 0.62, transform: 'rotate(45deg)', border: `1.4px solid ${reward.accent}`, boxShadow: `0 0 6px ${hexA(reward.accent, 0.6)}` }} />;
}

/* ─── frame + header ────────────────────────────────────────────────── */
function ChalFrame({ frameRef, children }) {
  return (
    <div ref={frameRef} style={{
      width: '100%', height: '100%', background: INV.grad,
      fontFamily: INV.sans, color: INV.text, overflow: 'hidden',
      display: 'flex', flexDirection: 'column', position: 'relative',
    }}>{children}</div>
  );
}

function ChalHeader({ list, right }) {
  const s = challengeSummary(list);
  const pct = Math.round((s.done / Math.max(1, s.total)) * 100);
  return (
    <div style={{
      display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between',
      gap: 24, padding: '26px 32px 18px',
    }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
          <Diamond size={7} color={INV.accent} />
          <MonoLabel color={INV.accentDim}>Quests</MonoLabel>
        </div>
        <div style={{ fontSize: 30, fontWeight: 400, letterSpacing: -0.4, lineHeight: 1 }}>Challenges</div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 22 }}>
        {right}
        <div style={{ textAlign: 'right' }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, justifyContent: 'flex-end' }}>
            <span style={{ fontFamily: INV.mono, fontSize: 22, color: INV.success }}>{s.done}</span>
            <span style={{ fontFamily: INV.mono, fontSize: 13, color: INV.t4 }}>/ {s.total}</span>
          </div>
          <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>completed · {pct}%</MonoLabel>
        </div>
      </div>
    </div>
  );
}

/* segmented filter (All / Active / Completed) used by a couple directions */
function ChalFilter({ value, onChange, counts }) {
  const opts = [
    { k: 'all', label: 'All', n: counts.all },
    { k: 'active', label: 'Active', n: counts.active },
    { k: 'done', label: 'Completed', n: counts.done },
  ];
  return (
    <div style={{ display: 'flex', border: `1px solid ${INV.b1}`, borderRadius: 2, overflow: 'hidden' }}>
      {opts.map((o, i) => (
        <button key={o.k} onClick={() => onChange(o.k)}
          style={{
            padding: '5px 13px', fontFamily: INV.sans, fontSize: 11.5, cursor: 'pointer',
            border: 'none', borderLeft: i ? `1px solid ${INV.b1}` : 'none',
            background: value === o.k ? hexA(INV.accent, 0.16) : 'transparent',
            color: value === o.k ? INV.text : INV.t3, transition: 'all 120ms',
            display: 'flex', alignItems: 'center', gap: 6,
          }}>
          {o.label}
          <span style={{ fontFamily: INV.mono, fontSize: 9.5, opacity: 0.6 }}>{o.n}</span>
        </button>
      ))}
    </div>
  );
}

function filterChallenges(list, f) {
  if (f === 'active') return list.filter((c) => c.state === 'active' || c.state === 'locked');
  if (f === 'done') return list.filter((c) => c.state === 'done');
  return list;
}

Object.assign(window, {
  resolveReward, useChallenges, challengeSummary, useRewardLayer, RewardLayer,
  AnchoredRewardTooltip, RewardModTooltip, ttShell, TTSection,
  ProgressBar, ProgressCount, StatusNode, StatusPill, ChalGlyph, TypeTag, RewardGlyph,
  ChalFrame, ChalHeader, ChalFilter, filterChallenges,
});
