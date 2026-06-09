/* chal3-shared.jsx — refined chrome for the Type-Rail direction (v3).

   Builds on chal2-data + chal2-shared (view-model, progress, type glyphs).
   Adds, per feedback:
   - Reward3: cleaner reward affordance. NO eyebrow label, NO link underline.
     Just the item name (revealed) or '???' (sealed), with rarity colour +
     glow. Both states are hoverable.
   - Sealed rewards get a MASKED tooltip: same structure as the real one, but
     name / stat names / values / descriptions are redacted — while the correct
     number of stat bonuses and mod slots stays visible.
   - SortControl: in-page sort (Progress / Rarity / Name).
   - "Exciting" primitives: RingMeter, TypeHero banner, animated seal shimmer.

   Globals consumed: INV, hexA, rarityColor, MonoLabel, Diamond, ItemGlyph
   (inv-shared); RARITY, ITEM_CATEGORIES, CATEGORY_ACCENT, MOD_TYPE_LABEL,
   MOD_TYPE_ACCENT, statColor, statSign (inv-data / tooltip-shared); ttFinalShell,
   FinalSection, FinalItemTooltip (tooltip-final); RewardModTooltip, TypeGlyph,
   StatusNode, ProgressReadout, Bar, typeStats, CHALLENGE_TYPE_META, fmtTime,
   fmtNum, toTooltipItem (chal2-shared / inv-shared).
*/

/* ─── sort ──────────────────────────────────────────────────────────── */
const SORT_OPTS = [
  { k: 'progress', label: 'Progress' },
  { k: 'rarity',   label: 'Rarity' },
  { k: 'name',     label: 'Name' },
];
function sortChallenges3(items, sort) {
  const rank = { active: 0, locked: 1, done: 2 };
  const arr = items.slice();
  if (sort === 'rarity') return arr.sort((a, b) => (b.reward ? RARITY[b.reward.rarity].level : 0) - (a.reward ? RARITY[a.reward.rarity].level : 0) || a.id - b.id);
  if (sort === 'name') return arr.sort((a, b) => a.name.localeCompare(b.name));
  // progress: in-progress (by closeness) → locked → done
  return arr.sort((a, b) => rank[a.state] - rank[b.state] || b.prog.percent - a.prog.percent || a.id - b.id);
}
function SortControl({ value, onChange }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
      <MonoLabel color={INV.t4} style={{ fontSize: 9 }}>Sort</MonoLabel>
      <div style={{ display: 'flex', border: `1px solid ${INV.b1}`, borderRadius: 2, overflow: 'hidden' }}>
        {SORT_OPTS.map((o, i) => (
          <button key={o.k} onClick={() => onChange(o.k)}
            style={{
              padding: '4px 11px', fontFamily: INV.sans, fontSize: 11, cursor: 'pointer',
              border: 'none', borderLeft: i ? `1px solid ${INV.b1}` : 'none',
              background: value === o.k ? hexA(INV.accent, 0.16) : 'transparent',
              color: value === o.k ? INV.text : INV.t3, transition: 'all 120ms',
            }}>{o.label}</button>
        ))}
      </div>
    </div>
  );
}

/* ─── ring meter (completion) ───────────────────────────────────────── */
function RingMeter({ pct, accent, size = 30, stroke = 3, done, children }) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const col = done ? INV.success : accent;
  return (
    <div style={{ position: 'relative', width: size, height: size, flexShrink: 0 }}>
      <svg width={size} height={size} style={{ transform: 'rotate(-90deg)' }}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="rgba(255,255,255,0.09)" strokeWidth={stroke} />
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={col} strokeWidth={stroke}
          strokeDasharray={c} strokeDashoffset={c * (1 - pct / 100)} strokeLinecap="round"
          style={{ transition: 'stroke-dashoffset 360ms ease', filter: `drop-shadow(0 0 3px ${hexA(col, 0.6)})` }} />
      </svg>
      <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>{children}</div>
    </div>
  );
}

/* ─── type hero banner (selected type) ──────────────────────────────── */
function TypeHero({ tid, meta, items }) {
  const { total, done } = typeStats(items);
  const pct = Math.round((done / Math.max(1, total)) * 100);
  const acc = meta.accent;
  const complete = done === total;
  return (
    <div style={{
      position: 'relative', overflow: 'hidden', borderRadius: 5, marginBottom: 18,
      background: `linear-gradient(135deg, ${hexA(acc, 0.14)}, ${hexA(acc, 0.03)} 60%, transparent)`,
      border: `1px solid ${hexA(acc, 0.28)}`, padding: '18px 22px',
    }}>
      <div style={{ position: 'absolute', right: -14, top: -22, opacity: 0.08, pointerEvents: 'none' }}>
        <TypeGlyph typeId={tid} color={acc} size={150} strokeWidth={0.7} />
      </div>
      <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 20 }}>
        <RingMeter pct={pct} accent={acc} size={58} stroke={4} done={complete}>
          <TypeGlyph typeId={tid} color={complete ? INV.success : acc} size={24} />
        </RingMeter>
        <div style={{ flex: 1, minWidth: 0 }}>
          <MonoLabel color={hexA(acc, 0.9)} style={{ fontSize: 9.5 }}>Challenge Type</MonoLabel>
          <div style={{ fontSize: 26, fontWeight: 400, letterSpacing: -0.4, lineHeight: 1.05, margin: '4px 0 5px' }}>{meta.label}</div>
          <div style={{ fontSize: 12.5, color: INV.t2 }}>{meta.blurb}</div>
        </div>
        <div style={{ textAlign: 'right', flexShrink: 0 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, justifyContent: 'flex-end' }}>
            <span style={{ fontFamily: INV.mono, fontSize: 26, color: complete ? INV.success : INV.text, lineHeight: 1 }}>{done}</span>
            <span style={{ fontFamily: INV.mono, fontSize: 14, color: INV.t4 }}>/ {total}</span>
          </div>
          <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>unlocked · {pct}%</MonoLabel>
        </div>
      </div>
      <div className="chal-rail-line" style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: 2, background: `linear-gradient(90deg, transparent, ${hexA(acc, 0.7)}, transparent)`, '--acc': acc }} />
    </div>
  );
}

/* ─── reward affordance (tile / chip) ───────────────────────────────── */
function SealIcon3({ reward, mystery, size, glow, animate }) {
  const showRarity = mystery !== 'sealed';
  const showCat = mystery === 'category';
  const acc = showRarity ? reward.accent : INV.t3;
  const ringGlow = glow && showRarity ? hexA(acc, 0.5) : 'transparent';
  return (
    <div className={animate ? 'chal-seal' : ''}
      style={{
        width: size, height: size, flexShrink: 0, borderRadius: 3, position: 'relative', overflow: 'hidden',
        background: `repeating-linear-gradient(45deg, ${hexA(acc, 0.06)}, ${hexA(acc, 0.06)} 4px, transparent 4px, transparent 8px)`,
        border: `1px dashed ${hexA(acc, 0.5)}`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        '--rcol': ringGlow,
      }}>
      {showCat
        ? <div style={{ opacity: 0.6 }}>{reward.kind === 'item'
            ? <ItemGlyph cat={reward.cat} color={acc} size={Math.round(size * 0.46)} />
            : <div style={{ width: size * 0.32, height: size * 0.32, transform: 'rotate(45deg)', border: `1.4px solid ${acc}` }} />}</div>
        : <svg width={size * 0.42} height={size * 0.42} viewBox="0 0 16 16" fill="none" stroke={acc} strokeWidth="1.3"><rect x="3.5" y="7" width="9" height="6.5" rx="1" /><path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" /></svg>}
      {animate && <span className="chal-sweep" />}
    </div>
  );
}

function RevealIcon3({ reward, size, glow }) {
  const acc = reward.accent;
  return (
    <div style={{
      width: size, height: size, flexShrink: 0, borderRadius: 3,
      background: hexA(acc, 0.08), border: `1px solid ${hexA(acc, 0.55)}`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      boxShadow: glow ? `0 0 ${4 + reward.glow * 16}px ${hexA(acc, 0.55)}` : 'none',
    }}>
      {reward.kind === 'item'
        ? <ItemGlyph cat={reward.cat} color={acc} size={Math.round(size * 0.5)} />
        : <div style={{ width: size * 0.32, height: size * 0.32, transform: 'rotate(45deg)', border: `1.5px solid ${acc}`, boxShadow: `0 0 6px ${hexA(acc, 0.6)}` }} />}
    </div>
  );
}

function Reward3({ reward, layer, mystery = 'rarity', glow = true, variant = 'tile' }) {
  const [h, setH] = React.useState(false);
  if (!reward) return <span style={{ fontSize: 12.5, color: INV.t4, fontStyle: 'italic' }}>No reward</span>;
  const revealed = reward.revealed;
  const acc = revealed ? reward.accent : (mystery === 'sealed' ? INV.t3 : reward.accent);
  const b = layer ? layer.bind(reward) : {};
  const onEnter = (e) => { setH(true); if (b.onMouseEnter) b.onMouseEnter(e); };
  const onLeave = (e) => { setH(false); if (b.onMouseLeave) b.onMouseLeave(e); };

  if (variant === 'chip') {
    return (
      <span onMouseEnter={onEnter} onMouseLeave={onLeave}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 8, padding: '4px 11px 4px 5px', cursor: 'help', maxWidth: '100%',
          background: hexA(acc, h ? 0.16 : 0.08), borderRadius: 2,
          border: `1px ${revealed ? 'solid' : 'dashed'} ${hexA(acc, h ? 0.6 : 0.34)}`,
          boxShadow: glow && h ? `0 0 12px ${hexA(acc, 0.4)}` : 'none', transition: 'all 120ms',
        }}>
        {revealed ? <RevealIcon3 reward={reward} size={18} glow={glow} /> : <SealIcon3 reward={reward} mystery={mystery} size={18} glow={glow} animate={false} />}
        <span style={{ fontSize: 12.5, color: acc, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', letterSpacing: 0.1 }}>
          {revealed ? reward.name : '???'}
        </span>
      </span>
    );
  }

  // tile
  const sz = 46;
  return (
    <div onMouseEnter={onEnter} onMouseLeave={onLeave}
      style={{
        display: 'flex', alignItems: 'center', gap: 13, padding: '10px 12px', cursor: 'help', borderRadius: 3,
        background: hexA(acc, h ? 0.1 : 0.05),
        border: `1px ${revealed ? 'solid' : 'dashed'} ${hexA(acc, h ? 0.6 : 0.3)}`,
        transition: 'all 130ms',
      }}>
      {revealed ? <RevealIcon3 reward={reward} size={sz} glow={glow} /> : <SealIcon3 reward={reward} mystery={mystery} size={sz} glow={glow} animate />}
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{ fontSize: 15, color: acc, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', letterSpacing: revealed ? -0.1 : 1, textShadow: revealed && glow && h ? `0 0 12px ${hexA(acc, 0.6)}` : 'none' }}>
          {revealed ? reward.name : '???'}
        </div>
        <div style={{ marginTop: 4 }}>
          <MonoLabel color={INV.t4} style={{ fontSize: 8 }}>{mystery === 'sealed' && !revealed ? 'Sealed reward' : reward.sub}</MonoLabel>
        </div>
      </div>
      <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke={hexA(acc, h ? 0.9 : 0.4)} strokeWidth="1.4" style={{ flexShrink: 0, transition: 'stroke 120ms' }}>
        <circle cx="8" cy="8" r="6" /><path d="M8 5.2v.2M8 7.4v3.4" strokeLinecap="round" />
      </svg>
    </div>
  );
}

/* ─── hover layer (handles sealed + revealed) ───────────────────────── */
function useRewardLayer3() {
  const frameRef = React.useRef(null);
  const [hovered, setHovered] = React.useState(null);
  const bind = (reward) => ({
    onMouseEnter: (e) => { if (reward) setHovered({ reward, el: e.currentTarget }); },
    onMouseLeave: () => setHovered(null),
  });
  return { frameRef, hovered, bind };
}
function RewardLayer3({ layer, mystery }) {
  if (!layer.hovered) return null;
  return <AnchoredTooltip3 frameEl={layer.frameRef.current} anchorEl={layer.hovered.el} reward={layer.hovered.reward} mystery={mystery} />;
}
function AnchoredTooltip3({ frameEl, anchorEl, reward, mystery }) {
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
  const revealed = reward.revealed;
  return (
    <div ref={ref} style={{ position: 'absolute', left: pos ? pos.x : -9999, top: pos ? pos.y : -9999, zIndex: 80, pointerEvents: 'none', opacity: pos ? 1 : 0 }}>
      {revealed
        ? (reward.kind === 'item' ? <FinalItemTooltip item={toTooltipItem(reward.item)} /> : <RewardModTooltip mod={reward.mod} />)
        : (reward.kind === 'item' ? <SealedItemTooltip reward={reward} mystery={mystery} /> : <SealedModTooltip reward={reward} mystery={mystery} />)}
    </div>
  );
}

/* ─── masked (sealed) tooltips ──────────────────────────────────────── */
function RedBar({ w = 60, accent, h = 9 }) {
  return <span style={{ display: 'inline-block', width: w, height: h, borderRadius: 2, verticalAlign: 'middle', background: `repeating-linear-gradient(45deg, ${hexA(accent, 0.22)}, ${hexA(accent, 0.22)} 3px, ${hexA(accent, 0.07)} 3px, ${hexA(accent, 0.07)} 6px)` }} />;
}
function QMark({ accent }) {
  return <span style={{ fontFamily: INV.mono, fontSize: 11.5, color: hexA(accent, 0.7), letterSpacing: 1 }}>???</span>;
}
function SealedHead({ accent, catAccent, typeLabel }) {
  return (
    <div style={{ padding: '14px 16px 12px', borderBottom: '1px solid rgba(240,240,240,0.08)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 5 }}>
        <div style={{ width: 5, height: 5, transform: 'rotate(45deg)', background: catAccent, boxShadow: `0 0 6px ${catAccent}aa` }} />
        <div style={{ fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.8, textTransform: 'uppercase', color: catAccent }}>{typeLabel}</div>
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 5, padding: '2px 8px', background: hexA(accent, 0.1), border: `1px solid ${hexA(accent, 0.4)}`, borderRadius: 2 }}>
          <svg width="9" height="9" viewBox="0 0 16 16" fill="none" stroke={accent} strokeWidth="1.6"><rect x="3.5" y="7" width="9" height="6.5" rx="1" /><path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" /></svg>
          <span style={{ fontFamily: INV.mono, fontSize: 9, color: accent, letterSpacing: 1.2, textTransform: 'uppercase' }}>Sealed</span>
        </div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <div style={{ fontSize: 18, fontWeight: 400, color: 'rgba(240,240,240,0.45)', letterSpacing: 2 }}>?????????</div>
      </div>
    </div>
  );
}

function SealedItemTooltip({ reward, mystery }) {
  const accent = reward.accent;
  const item = reward.item;
  const attrs = item.attrs || [];
  const slots = item.modSlots || [];
  const typeLabel = mystery === 'sealed' ? 'Item' : reward.catLabel;
  const catAccent = mystery === 'sealed' ? 'rgba(240,240,240,0.5)' : CATEGORY_ACCENT[item.cat];
  const barW = [78, 60, 92, 70];
  return (
    <div style={ttFinalShell(accent)}>
      <SealedHead accent={accent} catAccent={catAccent} typeLabel={typeLabel} />
      <div style={{ padding: '12px 16px 14px' }}>
        {attrs.length > 0 && (
          <FinalSection label="Stats">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 6, columnGap: 12, alignItems: 'center' }}>
              {attrs.map((a, i) => (
                <React.Fragment key={i}>
                  <RedBar w={barW[i % barW.length]} accent={accent} />
                  <div style={{ textAlign: 'right' }}><QMark accent={accent} /></div>
                </React.Fragment>
              ))}
            </div>
          </FinalSection>
        )}
        {slots.length > 0 && (
          <FinalSection label={`Mods · 0/${slots.length}`}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {slots.map((s, i) => (
                <div key={i} style={{ padding: '6px 10px', border: '1px dashed rgba(255,255,255,0.14)', borderLeft: `2px solid ${hexA(accent, 0.5)}`, display: 'flex', alignItems: 'center', gap: 8 }}>
                  <svg width="10" height="10" viewBox="0 0 16 16" fill="none" stroke="rgba(240,240,240,0.4)" strokeWidth="1.5"><rect x="3.5" y="7" width="9" height="6.5" rx="1" /><path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" /></svg>
                  <span style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.45)', fontStyle: 'italic' }}>Sealed slot</span>
                </div>
              ))}
            </div>
          </FinalSection>
        )}
        <FinalSection label="Description" last>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <RedBar w={236} accent={accent} h={7} />
            <RedBar w={188} accent={accent} h={7} />
          </div>
        </FinalSection>
      </div>
    </div>
  );
}

function SealedModTooltip({ reward, mystery }) {
  const accent = reward.accent;
  const mod = reward.mod;
  const attrs = mod.attributes || [];
  const typeLabel = mystery === 'sealed' ? 'Item Mod' : MOD_TYPE_LABEL[mod.itemModTypeId];
  const catAccent = mystery === 'sealed' ? 'rgba(240,240,240,0.5)' : MOD_TYPE_ACCENT[mod.itemModTypeId];
  const barW = [70, 88, 58];
  return (
    <div style={ttFinalShell(accent)}>
      <SealedHead accent={accent} catAccent={catAccent} typeLabel={typeLabel} />
      <div style={{ padding: '12px 16px 14px' }}>
        {attrs.length > 0 && (
          <FinalSection label="Effects">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 6, columnGap: 12, alignItems: 'center' }}>
              {attrs.map((a, i) => (
                <React.Fragment key={i}>
                  <RedBar w={barW[i % barW.length]} accent={accent} />
                  <div style={{ textAlign: 'right' }}><QMark accent={accent} /></div>
                </React.Fragment>
              ))}
            </div>
          </FinalSection>
        )}
        <FinalSection label="Description" last>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <RedBar w={236} accent={accent} h={7} />
            <RedBar w={170} accent={accent} h={7} />
          </div>
        </FinalSection>
      </div>
    </div>
  );
}

Object.assign(window, {
  SORT_OPTS, sortChallenges3, SortControl, RingMeter, TypeHero,
  SealIcon3, RevealIcon3, Reward3, useRewardLayer3, RewardLayer3, AnchoredTooltip3,
  RedBar, QMark, SealedHead, SealedItemTooltip, SealedModTooltip,
});
