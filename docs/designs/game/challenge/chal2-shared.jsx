/* chal2-shared.jsx — shared mechanics + chrome for the TYPE-DRIVEN directions.

   Depends on (loaded earlier): tooltip-shared, tooltip-final, inv-data,
   inv-shared, chal2-data.

   Two headline behaviours, shared by all three directions:
   1. A reward is SEALED until its challenge is completed. While sealed it shows
      a teaser — rarity tier + item category / mod type — but never the name.
      On completion it REVEALS: the name becomes a rarity-coloured link that
      opens the same tooltip you'd see inspecting it in your inventory.
   2. ChallengeType is the organising axis: every direction groups by type,
      using a shared glyph + accent vocabulary defined here.
*/

/* ─── formatting ────────────────────────────────────────────────────── */
function fmtTime(sec) {
  if (!sec || sec <= 0) return '—';
  const m = Math.floor(sec / 60), s = Math.round(sec % 60);
  return m > 0 ? `${m}:${String(s).padStart(2, '0')}` : `${s}s`;
}
function fmtNum(n) {
  if (n >= 1000) return n.toLocaleString();
  return String(n);
}

/* ─── reward resolution (+ reveal gating) ───────────────────────────── */
function resolveReward2(ch, revealed) {
  if (ch.rewardItemId != null) {
    const item = ITEMS.find((i) => i.id === ch.rewardItemId);
    if (!item) return null;
    const rmeta = RARITY[item.rarity];
    return {
      kind: 'item', revealed, item, name: item.name, cat: item.cat,
      rarity: item.rarity, rarityLabel: rmeta.label, glow: rmeta.glow,
      accent: rarityColor(item.rarity),
      catLabel: (window.ITEM_CATEGORIES?.[item.cat] || 'Item'),
      sub: rmeta.label + ' · ' + (window.ITEM_CATEGORIES?.[item.cat] || 'Item'),
    };
  }
  if (ch.rewardItemModId != null) {
    const mod = MODS[ch.rewardItemModId];
    if (!mod) return null;
    const rar = MOD_RARITY[mod.id] || 'common';
    const rmeta = RARITY[rar];
    const modMeta = { ...mod, rarity: rar };
    return {
      kind: 'mod', revealed, mod: modMeta, name: mod.name, modType: mod.itemModTypeId,
      rarity: rar, rarityLabel: rmeta.label, glow: rmeta.glow,
      accent: rarityColor(rar),
      catLabel: MOD_TYPE_LABEL[mod.itemModTypeId],
      sub: rmeta.label + ' · ' + MOD_TYPE_LABEL[mod.itemModTypeId],
    };
  }
  return null;
}

/* progress math handling atLeast vs atMost (time trial). */
function progressInfo(ch, meta, p) {
  const atMost = meta.comparison === 'atMost';
  if (atMost) {
    const best = p.progress; // seconds; 0 = no data
    const hasData = best > 0;
    const percent = hasData ? Math.min(100, (ch.goal / best) * 100) : 0;
    return { atMost: true, best, target: ch.goal, hasData, percent };
  }
  const percent = Math.min(100, (p.progress / Math.max(1, ch.goal)) * 100);
  return { atMost: false, value: p.progress, goal: ch.goal, percent };
}

function useChallenges2() {
  return React.useMemo(() => CHALLENGES.map((ch) => {
    const p = PLAYER_CHALLENGES[ch.id] || { progress: 0, completed: false };
    const meta = CHALLENGE_TYPE_META[ch.typeId];
    const prog = progressInfo(ch, meta, p);
    const completed = p.completed;
    const state = completed ? 'done' : prog.percent > 0 ? 'active' : 'locked';
    const target = ch.targetEntityId != null ? (ENTITY_NAMES[meta.entity]?.[ch.targetEntityId] || null) : null;
    return {
      ...ch, ...p, meta, prog, completed, state, target,
      typeAccent: meta.accent,
      reward: resolveReward2(ch, completed),
    };
  }), []);
}

function groupByType(list) {
  return Object.keys(CHALLENGE_TYPE_META).map(Number)
    .map((tid) => ({ tid, meta: CHALLENGE_TYPE_META[tid], items: list.filter((c) => c.typeId === tid) }))
    .filter((g) => g.items.length > 0);
}
function typeStats(items) {
  return { total: items.length, done: items.filter((c) => c.state === 'done').length };
}

/* ─── type glyphs ───────────────────────────────────────────────────── */
const TYPE_GLYPH_PATH = {
  1: 'M3.5 3.5l9 9M12.5 3.5l-9 9',
  2: 'M2.5 11.5h11M3.5 11.5l.6-5.5 2.4 3L8 5l1.5 4 2.4-3 .6 5.5',
  3: 'M4 2.5v11M4 3.5h7l-1.8 2.2L11 8H4',
  5: 'M3.5 8.5l4.5-4 4.5 4M3.5 12.5l4.5-4 4.5 4',
  6: 'M8 2.2v2.4M8 11.4v2.4M2.2 8h2.4M11.4 8h2.4M4.2 4.2l1.7 1.7M10.1 10.1l1.7 1.7M11.8 4.2L10.1 5.9M5.9 10.1l-1.7 1.7',
  7: 'M8 2.4l5 1.8v3.6c0 3-2.4 5-5 5.8-2.6-.8-5-2.8-5-5.8V4.2zM5.8 7.9l1.7 1.7L10.4 6',
  8: 'M8 1.8l1.6 4.6 4.6 1.6-4.6 1.6L8 14.2l-1.6-4.6L1.8 8l4.6-1.6z',
};
function TypeGlyph({ typeId, color, size = 16, strokeWidth = 1.3 }) {
  if (typeId === 4) {
    return (
      <svg width={size} height={size} viewBox="0 0 16 16" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
        <circle cx="8" cy="9.2" r="4.3" />
        <path d="M6.4 2.6h3.2M8 2.6v1.9M8 9.2V6.6M8 9.2l2.1 1.5" />
      </svg>
    );
  }
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
      <path d={TYPE_GLYPH_PATH[typeId]} />
    </svg>
  );
}

/* ─── progress chrome ───────────────────────────────────────────────── */
function Bar({ percent, accent, done, height = 5, track = 'rgba(255,255,255,0.07)' }) {
  const col = done ? INV.success : accent;
  return (
    <div style={{ position: 'relative', height, borderRadius: height / 2, overflow: 'hidden', background: track }}>
      <div style={{
        position: 'absolute', inset: 0, width: `${percent}%`,
        background: `linear-gradient(90deg, ${hexA(col, 0.85)}, ${hexA(col, 0.45)})`,
        boxShadow: `0 0 8px ${hexA(col, 0.5)}`, transition: 'width 320ms ease',
      }} />
      {!done && percent > 3 && percent < 99 && (
        <div style={{ position: 'absolute', top: -1, bottom: -1, left: `calc(${percent}% - 1px)`, width: 2, background: hexA(col, 0.95), boxShadow: `0 0 6px ${col}` }} />
      )}
    </div>
  );
}

/* Progress readout — branches on comparison. Time trials show best vs target
   ("beat this") with a proximity meter, never a misleading 0→goal fill. */
function ProgressReadout({ c, showBar = true, barHeight = 5 }) {
  const { prog, state } = c;
  const done = state === 'done';
  if (prog.atMost) {
    return (
      <div>
        <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: showBar ? 6 : 0, gap: 10 }}>
          <span style={{ display: 'inline-flex', alignItems: 'baseline', gap: 6 }}>
            <span style={{ fontFamily: INV.mono, fontSize: 13, color: done ? INV.success : prog.hasData ? INV.text : INV.t4 }}>
              {prog.hasData ? fmtTime(prog.best) : 'no time yet'}
            </span>
            {prog.hasData && <span style={{ fontFamily: INV.mono, fontSize: 9.5, color: INV.t4 }}>best</span>}
          </span>
          <span style={{ display: 'inline-flex', alignItems: 'baseline', gap: 5 }}>
            <span style={{ fontFamily: INV.mono, fontSize: 9, letterSpacing: 1, color: INV.t4 }}>BEAT</span>
            <span style={{ fontFamily: INV.mono, fontSize: 12, color: c.typeAccent }}>≤ {fmtTime(prog.target)}</span>
          </span>
        </div>
        {showBar && <Bar percent={prog.percent} accent={c.typeAccent} done={done} height={barHeight} />}
      </div>
    );
  }
  const unit = c.meta.unit;
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: showBar ? 6 : 0 }}>
        <span style={{ fontFamily: INV.mono, fontSize: 12, letterSpacing: 0.3 }}>
          <span style={{ color: done ? INV.success : INV.text }}>{fmtNum(prog.value)}</span>
          <span style={{ color: INV.t4 }}> / {fmtNum(prog.goal)}</span>
          <span style={{ color: INV.t4 }}> {unit}</span>
        </span>
        {!done && <span style={{ fontFamily: INV.mono, fontSize: 10.5, color: INV.t3 }}>{Math.round(prog.percent)}%</span>}
      </div>
      {showBar && <Bar percent={prog.percent} accent={c.typeAccent} done={done} height={barHeight} />}
    </div>
  );
}

function StatusNode({ state, accent, size = 22 }) {
  const col = state === 'done' ? INV.success : state === 'active' ? accent : INV.t4;
  return (
    <div style={{ width: size, height: size, flexShrink: 0, position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <div style={{
        position: 'absolute', inset: 0, transform: 'rotate(45deg)', borderRadius: 2,
        border: `1px solid ${state === 'locked' ? INV.b2 : hexA(col, 0.7)}`,
        background: state === 'done' ? hexA(col, 0.16) : state === 'active' ? hexA(col, 0.1) : 'transparent',
        boxShadow: state === 'locked' ? 'none' : `0 0 8px ${hexA(col, 0.45)}`,
      }} />
      {state === 'done'
        ? <svg width={size * 0.5} height={size * 0.5} viewBox="0 0 16 16" fill="none" stroke={col} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ position: 'relative' }}><path d="M3 8.5l3.5 3.5L13 4.5" /></svg>
        : <div style={{ width: 5, height: 5, transform: 'rotate(45deg)', position: 'relative', background: state === 'active' ? col : 'transparent', border: state === 'locked' ? `1px solid ${INV.t4}` : 'none' }} />}
    </div>
  );
}

/* ─── type header (used by every direction) ─────────────────────────── */
function TypeHeader({ tid, meta, items, size = 'md' }) {
  const { total, done } = typeStats(items);
  const acc = meta.accent;
  const big = size === 'lg';
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
      <div style={{
        width: big ? 34 : 28, height: big ? 34 : 28, flexShrink: 0, borderRadius: 3,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: hexA(acc, 0.1), border: `1px solid ${hexA(acc, 0.4)}`,
      }}>
        <TypeGlyph typeId={tid} color={acc} size={big ? 18 : 15} />
      </div>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: big ? 17 : 14.5, color: INV.text, letterSpacing: -0.2, lineHeight: 1.1, whiteSpace: 'nowrap' }}>{meta.label}</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 3 }}>
          <span style={{ fontFamily: INV.mono, fontSize: 9.5, color: done === total ? INV.success : hexA(acc, 0.8) }}>{done}/{total}</span>
          <span style={{ fontFamily: INV.mono, fontSize: 9, color: INV.t4, letterSpacing: 0.5 }}>· {meta.unit}</span>
        </div>
      </div>
    </div>
  );
}

/* ─── reward affordance: sealed teaser ↔ revealed link/tile/chip ──────── */
function SealGlyphBox({ reward, mystery, size, glowOn }) {
  // mystery: 'category' (rarity+icon) | 'rarity' (rarity, lock) | 'sealed' (neutral lock)
  const showRarity = mystery !== 'sealed';
  const showIcon = mystery === 'category';
  const acc = showRarity ? reward.accent : INV.t3;
  const glowAmt = glowOn && showRarity ? 4 + reward.glow * 12 : 0;
  return (
    <div style={{
      width: size, height: size, flexShrink: 0, borderRadius: 3, position: 'relative',
      background: `repeating-linear-gradient(45deg, ${hexA(acc, 0.05)}, ${hexA(acc, 0.05)} 4px, transparent 4px, transparent 8px)`,
      border: `1px dashed ${hexA(acc, 0.5)}`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      boxShadow: glowAmt ? `0 0 ${glowAmt}px ${hexA(acc, 0.4)}` : 'none',
    }}>
      {showIcon
        ? <div style={{ opacity: 0.6 }}>{reward.kind === 'item'
            ? <ItemGlyph cat={reward.cat} color={acc} size={Math.round(size * 0.46)} />
            : <div style={{ width: size * 0.32, height: size * 0.32, transform: 'rotate(45deg)', border: `1.4px solid ${acc}` }} />}</div>
        : <svg width={size * 0.42} height={size * 0.42} viewBox="0 0 16 16" fill="none" stroke={acc} strokeWidth="1.3"><rect x="3.5" y="7" width="9" height="6.5" rx="1" /><path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" /></svg>}
      <div style={{ position: 'absolute', right: -4, bottom: -4, width: 13, height: 13, borderRadius: '50%', background: INV.surface, border: `1px solid ${hexA(acc, 0.6)}`, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <span style={{ fontFamily: INV.mono, fontSize: 8, color: acc, lineHeight: 1 }}>?</span>
      </div>
    </div>
  );
}

/* one affordance, three shapes. Binds hover only when revealed. */
function Reward({ reward, layer, mystery = 'category', glow = true, variant = 'link' }) {
  const [h, setH] = React.useState(false);
  if (!reward) return <span style={{ fontSize: 12.5, color: INV.t4, fontStyle: 'italic' }}>No reward</span>;
  const revealed = reward.revealed;
  const b = revealed && layer ? layer.bind(reward) : {};
  const acc = revealed ? reward.accent : (mystery === 'sealed' ? INV.t3 : reward.accent);
  const cursor = revealed ? 'help' : 'default';
  const onEnter = (e) => { setH(true); if (b.onMouseEnter) b.onMouseEnter(e); };
  const onLeave = (e) => { setH(false); if (b.onMouseLeave) b.onMouseLeave(e); };

  // ── TILE (boards) ──
  if (variant === 'tile') {
    const sz = 46;
    return (
      <div onMouseEnter={onEnter} onMouseLeave={onLeave}
        style={{
          display: 'flex', alignItems: 'center', gap: 12, padding: '10px 12px', cursor, borderRadius: 3,
          background: hexA(acc, revealed && h ? 0.1 : 0.05),
          border: `1px ${revealed ? 'solid' : 'dashed'} ${hexA(acc, revealed ? (h ? 0.6 : 0.3) : 0.3)}`,
          transition: 'all 130ms',
        }}>
        {revealed
          ? <div style={{ width: sz, height: sz, flexShrink: 0, borderRadius: 3, background: hexA(acc, 0.08), border: `1px solid ${hexA(acc, 0.55)}`, display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: glow ? `0 0 ${4 + reward.glow * 14}px ${hexA(acc, 0.5)}` : 'none' }}>
              {reward.kind === 'item' ? <ItemGlyph cat={reward.cat} color={acc} size={Math.round(sz * 0.5)} /> : <div style={{ width: sz * 0.32, height: sz * 0.32, transform: 'rotate(45deg)', border: `1.5px solid ${acc}`, boxShadow: `0 0 6px ${hexA(acc, 0.6)}` }} />}
            </div>
          : <SealGlyphBox reward={reward} mystery={mystery} size={sz} glowOn={glow} />}
        <div style={{ minWidth: 0, flex: 1 }}>
          <MonoLabel color={hexA(acc, 0.7)} style={{ fontSize: 8 }}>{revealed ? (reward.kind === 'item' ? 'Item unlocked' : 'Mod unlocked') : 'Sealed reward'}</MonoLabel>
          <div style={{ fontSize: 14, color: acc, marginTop: 3, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', textShadow: revealed && h ? `0 0 12px ${hexA(acc, 0.6)}` : 'none', borderBottom: revealed ? `1px dashed ${hexA(acc, h ? 0.9 : 0.4)}` : 'none', display: 'inline-block', paddingBottom: 1 }}>
            {revealed ? reward.name : '???'}
          </div>
          <div style={{ marginTop: 3 }}>
            <MonoLabel color={INV.t4} style={{ fontSize: 8 }}>{mystery === 'sealed' && !revealed ? 'Unlock to reveal' : reward.sub}</MonoLabel>
          </div>
        </div>
      </div>
    );
  }

  // ── CHIP (dense rows) ──
  if (variant === 'chip') {
    return (
      <span onMouseEnter={onEnter} onMouseLeave={onLeave}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 7, padding: '4px 10px 4px 6px', cursor, maxWidth: '100%',
          background: hexA(acc, revealed && h ? 0.16 : 0.07), borderRadius: 2,
          border: `1px ${revealed ? 'solid' : 'dashed'} ${hexA(acc, revealed ? (h ? 0.6 : 0.32) : 0.32)}`,
          boxShadow: revealed && glow && h ? `0 0 12px ${hexA(acc, 0.4)}` : 'none', transition: 'all 120ms',
        }}>
        {revealed
          ? (reward.kind === 'item' ? <ItemGlyph cat={reward.cat} color={acc} size={14} /> : <div style={{ width: 9, height: 9, transform: 'rotate(45deg)', border: `1.3px solid ${acc}` }} />)
          : <span style={{ display: 'inline-flex' }}><SealGlyphBox reward={reward} mystery={mystery} size={16} glowOn={false} /></span>}
        <span style={{ fontSize: 12.5, color: acc, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', letterSpacing: 0.1 }}>
          {revealed ? reward.name : (mystery === 'sealed' ? 'Sealed' : reward.rarityLabel + ' ' + (reward.kind === 'item' ? reward.catLabel : 'Mod'))}
        </span>
      </span>
    );
  }

  // ── LINK (rows / lists) — default ──
  return (
    <span onMouseEnter={onEnter} onMouseLeave={onLeave}
      style={{ display: 'inline-flex', alignItems: 'center', gap: 9, cursor, justifyContent: 'flex-end' }}>
      <span style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: revealed ? 16 : 22, flexShrink: 0 }}>
        {revealed
          ? (reward.kind === 'item' ? <ItemGlyph cat={reward.cat} color={acc} size={15} /> : <div style={{ width: 10, height: 10, transform: 'rotate(45deg)', border: `1.4px solid ${acc}`, boxShadow: glow ? `0 0 6px ${hexA(acc, 0.6)}` : 'none' }} />)
          : <SealGlyphBox reward={reward} mystery={mystery} size={22} glowOn={glow} />}
      </span>
      <span style={{ minWidth: 0, textAlign: 'left' }}>
        <span style={{
          fontSize: 13.5, color: acc, letterSpacing: 0.1, whiteSpace: 'nowrap',
          borderBottom: revealed ? `1px dashed ${hexA(acc, h ? 0.95 : 0.45)}` : 'none', paddingBottom: 1,
          textShadow: revealed && glow && h ? `0 0 12px ${hexA(acc, 0.7)}` : 'none', transition: 'all 120ms',
        }}>
          {revealed ? reward.name : (mystery === 'sealed' ? 'Sealed reward' : '???')}
        </span>
        <span style={{ display: 'block', marginTop: 2 }}>
          <MonoLabel color={INV.t4} style={{ fontSize: 8 }}>{mystery === 'sealed' && !revealed ? 'Unlock to reveal' : reward.sub}</MonoLabel>
        </span>
      </span>
    </span>
  );
}

/* ─── hover tooltip layer (zoom-safe, scoped to a frame) ─────────────── */
function useRewardLayer() {
  const frameRef = React.useRef(null);
  const [hovered, setHovered] = React.useState(null);
  const bind = (reward) => ({
    onMouseEnter: (e) => { if (reward && reward.revealed) setHovered({ reward, el: e.currentTarget }); },
    onMouseLeave: () => setHovered(null),
  });
  return { frameRef, hovered, bind };
}
function RewardLayer({ layer }) {
  if (!layer.hovered) return null;
  return <AnchoredTooltip frameEl={layer.frameRef.current} anchorEl={layer.hovered.el} reward={layer.hovered.reward} />;
}
function AnchoredTooltip({ frameEl, anchorEl, reward }) {
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
    let x = (left0 + aw / 2) > fw / 2 ? left0 - ttW - 12 : left0 + aw + 12;
    x = Math.max(8, Math.min(x, fw - ttW - 8));
    let y = Math.max(8, Math.min(top0 + ah / 2 - 44, Math.max(8, fh - ttH - 8)));
    setPos({ x, y });
  }, [frameEl, anchorEl, reward]);
  return (
    <div ref={ref} style={{ position: 'absolute', left: pos ? pos.x : -9999, top: pos ? pos.y : -9999, zIndex: 80, pointerEvents: 'none', opacity: pos ? 1 : 0 }}>
      {reward.kind === 'item' ? <FinalItemTooltip item={toTooltipItem(reward.item)} /> : <RewardModTooltip mod={reward.mod} />}
    </div>
  );
}

function RewardModTooltip({ mod }) {
  const accent = rarityColor(mod.rarity);
  const rmeta = RARITY[mod.rarity];
  return (
    <div style={{ width: 280, background: 'rgba(20,21,27,0.96)', border: '1px solid rgba(255,255,255,0.14)', borderLeft: `3px solid ${accent}`, borderRadius: 3, boxShadow: `0 12px 28px rgba(0,0,0,0.55), 0 0 0 1px rgba(0,0,0,0.4), -4px 0 16px ${accent}22`, color: '#f0f0f0', overflow: 'hidden', backdropFilter: 'blur(6px)' }}>
      <div style={{ padding: '14px 16px 12px', borderBottom: '1px solid rgba(240,240,240,0.08)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 5 }}>
          <div style={{ width: 5, height: 5, transform: 'rotate(45deg)', background: accent, boxShadow: `0 0 6px ${accent}aa` }} />
          <div style={{ fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.8, textTransform: 'uppercase', color: MOD_TYPE_ACCENT[mod.itemModTypeId] }}>{MOD_TYPE_LABEL[mod.itemModTypeId]}</div>
        </div>
        <div style={{ fontSize: 18, fontWeight: 400, color: '#f0f0f0', letterSpacing: -0.2, lineHeight: 1.15 }}>{mod.name}</div>
      </div>
      <div style={{ padding: '12px 16px 14px' }}>
        {mod.attributes?.length > 0 && (
          <div style={{ marginBottom: mod.description ? 12 : 0 }}>
            <div style={{ fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.8, textTransform: 'uppercase', color: 'rgba(240,240,240,0.4)', marginBottom: 7, display: 'flex', alignItems: 'center', gap: 8 }}>Effects<div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.06)' }} /></div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 4, columnGap: 12 }}>
              {mod.attributes.map((a) => (
                <React.Fragment key={a.name}>
                  <div style={{ fontSize: 12, color: 'rgba(240,240,240,0.78)' }}>{a.name}</div>
                  <div style={{ fontFamily: INV.mono, fontSize: 11.5, color: statColor(a.value), letterSpacing: 0.3, textAlign: 'right' }}>{statSign(a.value, a.suffix || '')}</div>
                </React.Fragment>
              ))}
            </div>
          </div>
        )}
        {mod.description && (
          <div>
            <div style={{ fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.8, textTransform: 'uppercase', color: 'rgba(240,240,240,0.4)', marginBottom: 7, display: 'flex', alignItems: 'center', gap: 8 }}>Description<div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.06)' }} /></div>
            <div style={{ fontSize: 11.5, fontStyle: 'italic', color: 'rgba(240,240,240,0.6)', lineHeight: 1.55 }}>{mod.description}</div>
          </div>
        )}
      </div>
    </div>
  );
}

/* ─── summary helpers ───────────────────────────────────────────────── */
function overallSummary(list) {
  const total = list.length;
  const done = list.filter((c) => c.state === 'done').length;
  const active = list.filter((c) => c.state === 'active').length;
  return { total, done, active, pct: Math.round((done / Math.max(1, total)) * 100) };
}
function nextUp(list) {
  return list.filter((c) => c.state === 'active').sort((a, b) => b.prog.percent - a.prog.percent)[0] || null;
}
function recentlyUnlocked(list) {
  return list.filter((c) => c.state === 'done' && c.reward);
}

/* ─── frame + header ────────────────────────────────────────────────── */
function ChalFrame({ frameRef, children }) {
  return (
    <div ref={frameRef} style={{ width: '100%', height: '100%', background: INV.grad, fontFamily: INV.sans, color: INV.text, overflow: 'hidden', display: 'flex', flexDirection: 'column', position: 'relative' }}>{children}</div>
  );
}
function ChalHeader({ list, right, sub }) {
  const s = overallSummary(list);
  return (
    <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 24, padding: '24px 30px 16px' }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 7 }}>
          <Diamond size={7} color={INV.accent} />
          <MonoLabel color={INV.accentDim}>Quests</MonoLabel>
        </div>
        <div style={{ fontSize: 28, fontWeight: 400, letterSpacing: -0.4, lineHeight: 1 }}>Challenges</div>
        {sub && <div style={{ fontSize: 12, color: INV.t3, marginTop: 6 }}>{sub}</div>}
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
        {right}
        <div style={{ textAlign: 'right' }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, justifyContent: 'flex-end' }}>
            <span style={{ fontFamily: INV.mono, fontSize: 22, color: INV.success }}>{s.done}</span>
            <span style={{ fontFamily: INV.mono, fontSize: 13, color: INV.t4 }}>/ {s.total}</span>
          </div>
          <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>unlocked · {s.pct}%</MonoLabel>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, {
  fmtTime, fmtNum, resolveReward2, progressInfo, useChallenges2, groupByType, typeStats,
  TypeGlyph, Bar, ProgressReadout, StatusNode, TypeHeader,
  SealGlyphBox, Reward, useRewardLayer, RewardLayer, AnchoredTooltip, RewardModTooltip,
  overallSummary, nextUp, recentlyUnlocked, ChalFrame, ChalHeader,
});
