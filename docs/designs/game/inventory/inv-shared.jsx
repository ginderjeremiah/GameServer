/* inv-shared.jsx — state hook + shared UI for the Inventory directions.

   Depends on: inv-data.jsx (EQUIP_SLOTS, RARITY, MODS, ITEMS, SORTS, helpers),
   tooltip-shared.jsx (CATEGORY_ACCENT, statSign, statColor, MOD_TYPE_*),
   tooltip-final.jsx (FinalItemTooltip). All loaded earlier in the HTML.

   Interaction model (consistent across every direction, tuned for few clicks):
   - Click an item / equipped slot  → SELECT it (inspect + mod). Never destructive.
   - Drag an item → an equip slot    → EQUIP (primary, the kept behavior).
   - Ctrl/⌘ + click an item          → equip to its default slot / unequip.
   - Hover an item                   → ★ favorite toggle appears.
   - Equip / Unequip button in the inspector = one-click alternative.
   This leaves clean room for the planned Ctrl-click + saved loadouts.
*/

const INV = {
  grad: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
  surface: '#14151b',
  panel: 'rgba(20,21,27,0.5)',
  panelSolid: 'rgba(20,21,27,0.85)',
  fill: 'rgba(255,255,255,0.03)',
  fill2: 'rgba(255,255,255,0.06)',
  text: '#f0f0f0',
  t2: 'rgba(240,240,240,0.78)',
  t3: 'rgba(240,240,240,0.55)',
  t4: 'rgba(240,240,240,0.4)',
  b1: 'rgba(255,255,255,0.08)',
  b2: 'rgba(255,255,255,0.14)',
  b3: 'rgba(255,255,255,0.22)',
  accent: '#a1c2f7',
  accentDim: 'rgba(161,194,247,0.5)',
  success: '#bde0b4',
  error: '#f0a094',
  gold: '#e8c878',
  mono: 'Geist Mono, monospace',
  sans: 'Geist, Arial, Helvetica, sans-serif',
};

const catAccent = (cat) => (window.CATEGORY_ACCENT && window.CATEGORY_ACCENT[cat]) || INV.accent;
const catName = (cat) => (window.ITEM_CATEGORIES && window.ITEM_CATEGORIES[cat]) || '';
const rarityColor = (rarity) => (RARITY[rarity] && RARITY[rarity].color) || INV.accent;

function hexA(hex, a) {
  const h = String(hex).replace('#', '');
  const n = h.length === 3 ? h.replace(/./g, (c) => c + c) : h;
  const r = parseInt(n.slice(0, 2), 16), g = parseInt(n.slice(2, 4), 16), b = parseInt(n.slice(4, 6), 16);
  return `rgba(${r},${g},${b},${a})`;
}

/* ─── State hook ────────────────────────────────────────────────────── */
function useInventory(initialSort = 'category') {
  const [items, setItems] = React.useState(() => ITEMS.map((i) => ({ ...i, applied: { ...i.applied } })));
  const [selectedId, setSelectedId] = React.useState(null);
  const [hoverId, setHoverId] = React.useState(null);
  const [sort, setSort] = React.useState(initialSort);
  const [filterCat, setFilterCat] = React.useState(null);
  const [favOnly, setFavOnly] = React.useState(false);
  const [page, setPage] = React.useState(0);
  const [dragItemId, setDragItemId] = React.useState(null);

  const itemsById = React.useMemo(() => Object.fromEntries(items.map((i) => [i.id, i])), [items]);
  const equippedBySlot = React.useMemo(() => {
    const m = {};
    for (const it of items) if (it.equipSlot != null) m[it.equipSlot] = it;
    return m;
  }, [items]);

  const equip = (itemId, slotId) => setItems((prev) => prev.map((it) => {
    if (it.id === itemId) return { ...it, equipSlot: slotId };
    if (it.equipSlot === slotId) return { ...it, equipSlot: null };
    return it;
  }));
  const unequip = (slotId) => setItems((prev) => prev.map((it) => (it.equipSlot === slotId ? { ...it, equipSlot: null } : it)));
  const toggleEquip = (item, equippedBySlotNow) => {
    if (item.equipSlot != null) unequip(item.equipSlot);
    else { const s = defaultSlotForItem(item, equippedBySlotNow); if (s != null) equip(item.id, s); }
  };
  const toggleFav = (itemId) => setItems((prev) => prev.map((it) => (it.id === itemId ? { ...it, fav: !it.fav } : it)));
  const applyMod = (itemId, slotId, modId) => setItems((prev) => prev.map((it) => (it.id === itemId ? { ...it, applied: { ...it.applied, [slotId]: modId } } : it)));
  const removeMod = (itemId, slotId) => setItems((prev) => prev.map((it) => {
    if (it.id !== itemId) return it;
    const a = { ...it.applied }; delete a[slotId]; return { ...it, applied: a };
  }));

  const counts = React.useMemo(() => {
    const c = { all: items.length, fav: items.filter((i) => i.fav).length };
    for (const it of items) c[it.cat] = (c[it.cat] || 0) + 1;
    return c;
  }, [items]);

  const visible = React.useMemo(() => {
    let list = items.slice();
    if (favOnly) list = list.filter((i) => i.fav);
    if (filterCat != null) list = list.filter((i) => i.cat === filterCat);
    list.sort(SORTS[sort].cmp);
    return list;
  }, [items, favOnly, filterCat, sort]);

  return {
    items, itemsById, equippedBySlot, counts, visible,
    selectedId, setSelectedId, hoverId, setHoverId,
    sort, setSort, filterCat, setFilterCat, favOnly, setFavOnly,
    page, setPage, dragItemId, setDragItemId,
    equip, unequip, toggleEquip, toggleFav, applyMod, removeMod,
  };
}

/* ─── tooltip adapter ───────────────────────────────────────────────── */
function toTooltipItem(item) {
  // Pass the FULL slot list (filled + empty) so the tooltip can show gaps.
  const modSlots = (item.modSlots || []).map((s) => {
    const modId = item.applied?.[s.id];
    const m = modId != null ? MODS[modId] : null;
    return { modType: s.type, mod: m ? { name: m.name, description: m.description } : null };
  });
  return {
    name: item.name,
    itemCategoryId: item.cat,
    attributes: item.attrs,
    modSlots,
    appliedMods: modSlots.filter((s) => s.mod).map((s) => ({ name: s.mod.name, description: s.mod.description, modType: s.modType })),
    description: item.desc,
    equipped: item.equipSlot != null,
  };
}

// Sum every attribute across equipped items + their applied mods.
function computeEquippedStats(items) {
  const map = new Map();
  const add = (a) => {
    const e = map.get(a.name) || { name: a.name, value: 0, suffix: a.suffix || '' };
    e.value += a.value; if (a.suffix) e.suffix = a.suffix;
    map.set(a.name, e);
  };
  for (const it of items) {
    if (it.equipSlot == null) continue;
    (it.attrs || []).forEach(add);
    Object.values(it.applied || {}).forEach((modId) => (MODS[modId]?.attributes || []).forEach(add));
  }
  const ORDER = ['Strength', 'Endurance', 'Intellect', 'Agility', 'Dexterity', 'Luck', 'Max Health', 'Defense', 'Cooldown Recovery', 'Drop Bonus', 'Critical Chance', 'Critical Damage', 'Dodge Chance', 'Block Chance', 'Block Reduction'];
  return [...map.values()].filter((e) => e.value !== 0).sort((a, b) => {
    const ia = ORDER.indexOf(a.name), ib = ORDER.indexOf(b.name);
    return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib) || a.name.localeCompare(b.name);
  });
}

/* ─── small chrome ──────────────────────────────────────────────────── */
function Diamond({ size = 6, color = INV.accent, filled = true }) {
  return (
    <span style={{
      display: 'inline-block', width: size, height: size, transform: 'rotate(45deg)',
      background: filled ? color : 'transparent', border: filled ? 'none' : `1px solid ${color}`,
      boxShadow: filled ? `0 0 6px ${hexA(color, 0.7)}` : 'none', flexShrink: 0,
    }} />
  );
}

function MonoLabel({ children, color = INV.t4, style }) {
  return (
    <span style={{ fontFamily: INV.mono, fontSize: 9.5, letterSpacing: 1.6, textTransform: 'uppercase', color, ...style }}>
      {children}
    </span>
  );
}

function SectionRule({ label, color = INV.t4, accent }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
      {accent && <Diamond size={5} color={accent} />}
      <MonoLabel color={color}>{label}</MonoLabel>
      <div style={{ flex: 1, height: 1, background: INV.b1 }} />
    </div>
  );
}

function ItemGlyph({ cat, color, size = 22 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="none"
      stroke={color} strokeWidth="1.2" strokeLinejoin="round" strokeLinecap="round">
      <path d={CATEGORY_GLYPH[cat]} />
    </svg>
  );
}

function RarityPips({ rarity, color }) {
  const lvl = RARITY[rarity].level;
  return (
    <div style={{ display: 'flex', gap: 2 }}>
      {Array.from({ length: 6 }).map((_, i) => (
        <span key={i} style={{
          width: 3, height: 3, borderRadius: '50%',
          background: i < lvl ? color : 'rgba(240,240,240,0.14)',
        }} />
      ))}
    </div>
  );
}

function RarityDot({ rarity, size = 7 }) {
  const c = rarityColor(rarity);
  return <span style={{
    display: 'inline-block', width: size, height: size, borderRadius: '50%',
    background: c, boxShadow: `0 0 6px ${hexA(c, 0.65)}`, flexShrink: 0,
  }} />;
}

function RarityTag({ rarity }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <RarityDot rarity={rarity} size={6} />
      <MonoLabel color={rarityColor(rarity)} style={{ fontSize: 9 }}>{RARITY[rarity].label}</MonoLabel>
    </span>
  );
}

function StatGrid({ attrs }) {
  if (!attrs || !attrs.length) return null;
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 4, columnGap: 12 }}>
      {attrs.map((a) => (
        <React.Fragment key={a.name}>
          <div style={{ fontSize: 12, color: INV.t2 }}>{a.name}</div>
          <div style={{ fontFamily: INV.mono, fontSize: 11.5, color: statColor(a.value), textAlign: 'right', letterSpacing: 0.3 }}>
            {statSign(a.value, a.suffix || '')}
          </div>
        </React.Fragment>
      ))}
    </div>
  );
}

function EquippedStats({ items, title = 'Equipped totals' }) {
  const stats = computeEquippedStats(items);
  const filled = items.filter((i) => i.equipSlot != null).length;
  return (
    <div>
      <SectionRule label={title} accent={INV.accent} />
      {stats.length
        ? <StatGrid attrs={stats} />
        : <div style={{ fontSize: 12, color: INV.t4, fontStyle: 'italic', padding: '4px 0' }}>No gear equipped yet.</div>}
      <div style={{ marginTop: 12, paddingTop: 10, borderTop: `1px solid ${INV.b1}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <MonoLabel color={INV.t4}>Slots filled</MonoLabel>
        <span style={{ fontFamily: INV.mono, fontSize: 12, color: INV.t2 }}>{filled} / {EQUIP_SLOTS.length}</span>
      </div>
    </div>
  );
}

function FavStar({ on, onToggle, show }) {
  return (
    <button
      onClick={(e) => { e.stopPropagation(); onToggle(); }}
      title={on ? 'Unfavorite' : 'Favorite'}
      style={{
        position: 'absolute', top: 3, right: 3, width: 18, height: 18,
        padding: 0, border: 'none', background: 'transparent', cursor: 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        opacity: on ? 1 : show ? 0.75 : 0, transition: 'opacity 120ms', zIndex: 3,
      }}>
      <svg width="12" height="12" viewBox="0 0 16 16"
        fill={on ? INV.gold : 'none'} stroke={on ? INV.gold : 'rgba(240,240,240,0.85)'} strokeWidth="1.3">
        <path d="M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z" strokeLinejoin="round" />
      </svg>
    </button>
  );
}

/* ─── Inventory grid cell ───────────────────────────────────────────── */
function GridSlot({
  item, size = 64, selected, equipped, glow = true, accentBorders = true,
  onSelect, onToggleEquip, onToggleFav, onHover, onDragItem, onDragEnd,
}) {
  const [hover, setHover] = React.useState(false);
  const acc = catAccent(item.cat);
  const rc = rarityColor(item.rarity);
  const r = RARITY[item.rarity];
  const borderCol = accentBorders ? hexA(rc, Math.min(0.85, 0.34 + r.level * 0.09)) : INV.b2;
  const glowShadow = glow && r.glow > 0 ? `0 0 ${5 + r.glow * 16}px ${hexA(rc, r.glow * 0.5)}` : 'none';

  return (
    <div
      draggable
      onDragStart={(e) => { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', String(item.id)); onDragItem?.(item); }}
      onDragEnd={() => onDragEnd?.()}
      onClick={(e) => { if (e.metaKey || e.ctrlKey) onToggleEquip?.(item); else onSelect?.(item); }}
      onDoubleClick={() => onToggleEquip?.(item)}
      onMouseEnter={(e) => { setHover(true); onHover?.(item, e.currentTarget); }}
      onMouseLeave={() => { setHover(false); onHover?.(null, null); }}
      title=""
      style={{
        position: 'relative', width: size, height: size, flexShrink: 0,
        background: accentBorders ? hexA(rc, 0.05 + r.level * 0.012) : INV.fill,
        border: `1px solid ${selected ? INV.accent : borderCol}`,
        borderRadius: 3, cursor: 'grab',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        boxShadow: selected
          ? `0 0 0 1px ${INV.accent}, 0 0 14px ${hexA(INV.accent, 0.4)}`
          : (hover ? `0 0 0 1px ${hexA(rc, 0.6)}, ${glowShadow}` : glowShadow),
        transition: 'box-shadow 120ms, border-color 120ms, transform 120ms',
        transform: hover ? 'translateY(-1px)' : 'none',
      }}>
      {/* centre glyph is a NEUTRAL stand-in for the item's real art, so the
          only colour signals are the rarity border + the category corner icon */}
      <ItemGlyph cat={item.cat} color={equipped ? 'rgba(240,240,240,0.95)' : 'rgba(240,240,240,0.6)'} size={Math.round(size * 0.42)} />

      <FavStar on={!!item.fav} show={hover} onToggle={() => onToggleFav?.(item.id)} />

      {/* category indicator bottom-left — small + persistent; the centre
          glyph is a stand-in for the item's real art */}
      <div style={{
        position: 'absolute', left: 3, bottom: 3, width: 15, height: 15, borderRadius: 2,
        background: 'rgba(0,0,0,0.35)', display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <ItemGlyph cat={item.cat} color={hexA(acc, 0.85)} size={10} />
      </div>

      {/* equipped marker bottom-right */}
      {equipped && (
        <div style={{ position: 'absolute', right: 4, bottom: 4, display: 'flex', alignItems: 'center', gap: 3 }}>
          <Diamond size={5} color={INV.success} />
        </div>
      )}

      {/* applied-mod count top-left */}
      {Object.keys(item.applied || {}).length > 0 && (
        <span style={{
          position: 'absolute', top: 3, left: 4, fontFamily: INV.mono, fontSize: 8,
          color: INV.t3, letterSpacing: 0.4,
        }}>{Object.keys(item.applied).length}◈</span>
      )}
    </div>
  );
}

/* ─── Equipment drop-slot ───────────────────────────────────────────── */
function EquipDropSlot({
  slot, item, size = 64, selected, dragItem, layout = 'tile',
  onSelect, onDropItem, onUnequip, onHover,
}) {
  const [over, setOver] = React.useState(false);
  const [hover, setHover] = React.useState(false);
  const acc = item ? catAccent(item.cat) : INV.accent;
  const rc = item ? rarityColor(item.rarity) : INV.accent;
  const canAccept = dragItem && slot.accepts.includes(dragItem.cat);
  const filled = !!item;

  const tile = (
    <div
      onDragOver={(e) => { if (canAccept) { e.preventDefault(); setOver(true); } }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => { e.preventDefault(); setOver(false); onDropItem?.(slot.id); }}
      onClick={(e) => { if ((e.metaKey || e.ctrlKey) && filled) onUnequip?.(slot.id); else if (filled) onSelect?.(item); }}
      onMouseEnter={(e) => { setHover(true); if (filled) onHover?.(item, e.currentTarget); }}
      onMouseLeave={() => { setHover(false); onHover?.(null, null); }}
      style={{
        position: 'relative', width: size, height: size, flexShrink: 0,
        background: over ? hexA(INV.accent, 0.14) : filled ? hexA(rc, 0.07) : INV.fill,
        border: `1px ${filled ? 'solid' : 'dashed'} ${
          over ? INV.accent : selected ? INV.accent : filled ? hexA(rc, 0.6) : (canAccept ? INV.accentDim : INV.b2)}`,
        borderRadius: 3, cursor: filled ? 'pointer' : 'default',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        boxShadow: over ? `0 0 14px ${hexA(INV.accent, 0.45)}`
          : selected ? `0 0 0 1px ${INV.accent}` : 'none',
        transition: 'all 120ms',
      }}>
      {filled
        ? <ItemGlyph cat={item.cat} color={acc} size={Math.round(size * 0.42)} />
        : <ItemGlyph cat={slot.accepts[0]} color="rgba(240,240,240,0.18)" size={Math.round(size * 0.4)} />}

      {filled && (hover) && (
        <button onClick={(e) => { e.stopPropagation(); onUnequip?.(slot.id); }} title="Unequip"
          style={{
            position: 'absolute', top: 2, right: 2, width: 16, height: 16, borderRadius: 2,
            border: 'none', background: 'rgba(0,0,0,0.5)', color: INV.t2, cursor: 'pointer',
            display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 11, lineHeight: 1, padding: 0,
          }}>×</button>
      )}
      {filled && Object.keys(item.applied || {}).length > 0 && (
        <span style={{ position: 'absolute', bottom: 3, right: 4, fontFamily: INV.mono, fontSize: 8, color: INV.t3 }}>
          {Object.keys(item.applied).length}◈
        </span>
      )}
    </div>
  );

  if (layout === 'tile') return tile;
  // row layout: label + tile + name
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
      <div style={{ width: 64, textAlign: 'right', flexShrink: 0 }}>
        <MonoLabel color={INV.t3}>{slot.label}</MonoLabel>
      </div>
      {tile}
      <div style={{ minWidth: 0, flex: 1 }}>
        {filled
          ? <div style={{ fontSize: 13, color: INV.text, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{item.name}</div>
          : <div style={{ fontSize: 12, color: INV.t4, fontStyle: 'italic' }}>Empty</div>}
        {filled && (
          <div style={{ marginTop: 3 }}><RarityTag rarity={item.rarity} /></div>
        )}
      </div>
    </div>
  );
}

/* ─── Toolbar: filter chips + favorites + sort ──────────────────────── */
function Chip({ active, accent = INV.accent, onClick, children, title }) {
  const [h, setH] = React.useState(false);
  return (
    <button onClick={onClick} title={title}
      onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 6, padding: '4px 10px',
        fontFamily: INV.sans, fontSize: 11.5, lineHeight: 1.2,
        background: active ? hexA(accent, 0.16) : h ? INV.fill2 : 'transparent',
        color: active ? INV.text : INV.t3,
        border: `1px solid ${active ? hexA(accent, 0.55) : INV.b1}`,
        borderRadius: 2, cursor: 'pointer', whiteSpace: 'nowrap', transition: 'all 120ms',
      }}>{children}</button>
  );
}

function InvToolbar({ inv, showSort = true }) {
  const cats = [1, 2, 3, 4, 5, 6];
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 5, flexWrap: 'wrap' }}>
        <Chip active={inv.filterCat == null && !inv.favOnly} onClick={() => { inv.setFilterCat(null); inv.setFavOnly(false); }}>
          All <span style={{ fontFamily: INV.mono, fontSize: 9.5, opacity: 0.6 }}>{inv.counts.all}</span>
        </Chip>
        {cats.map((c) => (
          <Chip key={c} active={inv.filterCat === c} accent={catAccent(c)}
            onClick={() => inv.setFilterCat(inv.filterCat === c ? null : c)}>
            <Diamond size={5} color={catAccent(c)} />
            {catName(c)} <span style={{ fontFamily: INV.mono, fontSize: 9.5, opacity: 0.6 }}>{inv.counts[c] || 0}</span>
          </Chip>
        ))}
        <Chip active={inv.favOnly} accent={INV.gold} onClick={() => inv.setFavOnly((v) => !v)} title="Show favorites only">
          <svg width="11" height="11" viewBox="0 0 16 16" fill={inv.favOnly ? INV.gold : 'none'} stroke={INV.gold} strokeWidth="1.3">
            <path d="M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z" strokeLinejoin="round" />
          </svg>
          Favorites <span style={{ fontFamily: INV.mono, fontSize: 9.5, opacity: 0.6 }}>{inv.counts.fav}</span>
        </Chip>
      </div>

      {showSort && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginLeft: 'auto' }}>
          <MonoLabel color={INV.t4}>Sort</MonoLabel>
          <div style={{ display: 'flex', gap: 0, border: `1px solid ${INV.b1}`, borderRadius: 2, overflow: 'hidden' }}>
            {Object.entries(SORTS).map(([k, s], i) => (
              <button key={k} onClick={() => inv.setSort(k)}
                style={{
                  padding: '4px 11px', fontFamily: INV.sans, fontSize: 11.5, cursor: 'pointer',
                  border: 'none', borderLeft: i ? `1px solid ${INV.b1}` : 'none',
                  background: inv.sort === k ? hexA(INV.accent, 0.16) : 'transparent',
                  color: inv.sort === k ? INV.text : INV.t3, transition: 'all 120ms',
                }}>{s.label}</button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function Pager({ page, pages, onPage, total }) {
  if (pages <= 1) return (
    <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
      <MonoLabel color={INV.t4}>{total} items</MonoLabel>
    </div>
  );
  const Btn = ({ d, children }) => (
    <button onClick={() => onPage(Math.max(0, Math.min(pages - 1, page + d)))}
      disabled={d < 0 ? page === 0 : page === pages - 1}
      style={{
        width: 24, height: 22, border: `1px solid ${INV.b1}`, borderRadius: 2, cursor: 'pointer',
        background: 'transparent', color: INV.t2, fontFamily: INV.mono, fontSize: 12,
        opacity: (d < 0 ? page === 0 : page === pages - 1) ? 0.3 : 1,
      }}>{children}</button>
  );
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, justifyContent: 'flex-end' }}>
      <MonoLabel color={INV.t4}>{total} items</MonoLabel>
      <div style={{ flex: 1 }} />
      <Btn d={-1}>‹</Btn>
      <MonoLabel color={INV.t3} style={{ minWidth: 44, textAlign: 'center' }}>{page + 1} / {pages}</MonoLabel>
      <Btn d={1}>›</Btn>
    </div>
  );
}

/* ─── Mod slots + picker ────────────────────────────────────────────── */
function ModPicker({ slotType, item, onPick, onClose }) {
  const opts = compatibleMods(slotType, item);
  const acc = MOD_TYPE_ACCENT[slotType];
  React.useEffect(() => {
    const off = (e) => { if (!e.target.closest('[data-modpicker]')) onClose(); };
    document.addEventListener('pointerdown', off, true);
    return () => document.removeEventListener('pointerdown', off, true);
  }, [onClose]);
  return (
    <div data-modpicker style={{
      position: 'absolute', top: '100%', left: 0, marginTop: 4, zIndex: 60, width: 248,
      background: 'rgba(20,21,27,0.98)', border: `1px solid ${INV.b2}`, borderLeft: `2px solid ${acc}`,
      borderRadius: 3, boxShadow: '0 12px 30px rgba(0,0,0,0.6)', padding: 6, backdropFilter: 'blur(6px)',
    }}>
      <div style={{ padding: '2px 4px 6px' }}>
        <MonoLabel color={acc}>Install {MOD_TYPE_LABEL[slotType]}</MonoLabel>
      </div>
      {opts.length === 0 && (
        <div style={{ padding: '8px 6px', fontSize: 11.5, color: INV.t4, fontStyle: 'italic' }}>
          No unlocked {MOD_TYPE_LABEL[slotType].toLowerCase()} mods available.
        </div>
      )}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 2, maxHeight: 200, overflowY: 'auto' }}>
        {opts.map((m) => (
          <button key={m.id} onClick={() => onPick(m.id)}
            onMouseEnter={(e) => (e.currentTarget.style.background = INV.fill2)}
            onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
            style={{
              textAlign: 'left', border: 'none', background: 'transparent', cursor: 'pointer',
              padding: '6px 8px', borderRadius: 2, borderLeft: `2px solid ${acc}`,
            }}>
            <div style={{ fontSize: 12, fontWeight: 500, color: INV.text }}>{m.name}</div>
            <div style={{ fontSize: 11, color: INV.t3, lineHeight: 1.45, marginTop: 1 }}>{m.description}</div>
          </button>
        ))}
      </div>
    </div>
  );
}

function ModSlotCell({ slot, item, onApply, onRemove }) {
  const [open, setOpen] = React.useState(false);
  const modId = item.applied?.[slot.id];
  const mod = modId != null ? MODS[modId] : null;
  const acc = MOD_TYPE_ACCENT[slot.type];
  const filled = !!mod;
  return (
    <div style={{ position: 'relative' }}>
      <div
        onClick={() => { if (!filled) setOpen((o) => !o); }}
        style={{
          display: 'flex', alignItems: 'flex-start', gap: 8, padding: '7px 9px',
          background: filled ? INV.fill : 'transparent',
          border: `1px ${filled ? 'solid' : 'dashed'} ${filled ? INV.b1 : INV.b2}`,
          borderLeft: `2px solid ${acc}`, borderRadius: 2,
          cursor: filled ? 'default' : 'pointer', minHeight: 38,
        }}>
        <div style={{ minWidth: 0, flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
            {filled
              ? <span style={{ fontSize: 12, fontWeight: 500, color: INV.text }}>{mod.name}</span>
              : <span style={{ fontSize: 12, color: INV.t3 }}>Empty slot</span>}
            <MonoLabel color={acc} style={{ fontSize: 8.5 }}>{MOD_TYPE_LABEL[slot.type]}</MonoLabel>
          </div>
          {filled
            ? <div style={{ fontSize: 11, color: INV.t3, lineHeight: 1.45, marginTop: 2 }}>{mod.description}</div>
            : <div style={{ fontSize: 11, color: INV.t4, marginTop: 2 }}>Click to install a {MOD_TYPE_LABEL[slot.type].toLowerCase()}</div>}
        </div>
        {filled
          ? (mod.removable
              ? <button onClick={(e) => { e.stopPropagation(); onRemove(slot.id); }} title="Remove mod"
                  style={{ flexShrink: 0, width: 18, height: 18, border: 'none', borderRadius: 2, background: INV.fill2, color: INV.t3, cursor: 'pointer', fontSize: 11, padding: 0 }}>×</button>
              : <span title="Permanent" style={{ flexShrink: 0 }}><Diamond size={5} color={INV.t4} filled={false} /></span>)
          : <span style={{ flexShrink: 0, color: acc, fontSize: 14, lineHeight: 1 }}>+</span>}
      </div>
      {open && <ModPicker slotType={slot.type} item={item} onClose={() => setOpen(false)}
        onPick={(mid) => { onApply(slot.id, mid); setOpen(false); }} />}
    </div>
  );
}

function ModSlots({ item, onApply, onRemove }) {
  if (!item.modSlots || item.modSlots.length === 0) {
    return <div style={{ fontSize: 11.5, color: INV.t4, fontStyle: 'italic', padding: '4px 0' }}>This item has no mod slots.</div>;
  }
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {item.modSlots.map((s) => (
        <ModSlotCell key={s.id} slot={s} item={item}
          onApply={(slotId, modId) => onApply(item.id, slotId, modId)}
          onRemove={(slotId) => onRemove(item.id, slotId)} />
      ))}
    </div>
  );
}

/* ─── Equip / Unequip action button ─────────────────────────────────── */
function EquipButton({ item, inv, full }) {
  const equipped = item.equipSlot != null;
  const [h, setH] = React.useState(false);
  return (
    <button
      onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
      onClick={() => inv.toggleEquip(item, inv.equippedBySlot)}
      style={{
        width: full ? '100%' : 'auto', padding: '8px 16px',
        fontFamily: INV.sans, fontSize: 12.5, fontWeight: 500, cursor: 'pointer',
        background: equipped ? (h ? hexA(INV.error, 0.16) : 'transparent') : (h ? hexA(INV.accent, 0.24) : hexA(INV.accent, 0.16)),
        color: equipped ? INV.error : INV.accent,
        border: `1px solid ${equipped ? hexA(INV.error, 0.5) : hexA(INV.accent, 0.55)}`,
        borderRadius: 2, transition: 'all 120ms',
        display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
      }}>
      {equipped ? 'Unequip' : 'Equip'}
      <MonoLabel color="currentColor" style={{ fontSize: 8.5, opacity: 0.7 }}>{equipped ? '⌘·click' : 'or drag'}</MonoLabel>
    </button>
  );
}

/* ─── Anchored hover tooltip (zoom-safe) ────────────────────────────── */
function AnchoredTooltip({ item, cellEl, panelEl }) {
  const ref = React.useRef(null);
  const [pos, setPos] = React.useState(null);
  React.useLayoutEffect(() => {
    if (!cellEl || !panelEl) { setPos(null); return; }
    const pr = panelEl.getBoundingClientRect();
    const cr = cellEl.getBoundingClientRect();
    const scale = pr.width / panelEl.offsetWidth || 1;
    const left0 = (cr.left - pr.left) / scale;
    const top0 = (cr.top - pr.top) / scale;
    const cw = cr.width / scale, ch = cr.height / scale;
    const pw = panelEl.offsetWidth, ph = panelEl.offsetHeight;
    const ttW = 280;
    const ttH = ref.current ? ref.current.offsetHeight : 320;
    let x = left0 + cw + 12;
    if (x + ttW > pw - 8) x = left0 - ttW - 12;
    x = Math.max(8, Math.min(x, pw - ttW - 8));
    let y = top0 + ch / 2 - 50;
    y = Math.max(8, Math.min(y, Math.max(8, ph - ttH - 8)));
    setPos({ x, y });
  }, [cellEl, panelEl, item]);
  return (
    <div ref={ref} style={{
      position: 'absolute', left: pos ? pos.x : -9999, top: pos ? pos.y : -9999,
      zIndex: 70, pointerEvents: 'none', opacity: pos ? 1 : 0,
    }}>
      <FinalItemTooltip item={toTooltipItem(item)} />
    </div>
  );
}

/* ─── Screen frame (gradient bg) ────────────────────────────────────── */
function InvFrame({ children }) {
  return (
    <div style={{
      width: '100%', height: '100%', background: INV.grad,
      fontFamily: INV.sans, color: INV.text, overflow: 'hidden',
      display: 'flex', flexDirection: 'column', position: 'relative',
    }}>{children}</div>
  );
}

/* paginate helper */
function paginate(list, page, perPage) {
  const pages = Math.max(1, Math.ceil(list.length / perPage));
  const p = Math.min(page, pages - 1);
  return { pages, page: p, slice: list.slice(p * perPage, p * perPage + perPage) };
}

Object.assign(window, {
  INV, catAccent, catName, rarityColor, hexA, useInventory, toTooltipItem, computeEquippedStats,
  Diamond, MonoLabel, SectionRule, ItemGlyph, RarityPips, RarityDot, RarityTag, StatGrid, EquippedStats, FavStar,
  GridSlot, EquipDropSlot, Chip, InvToolbar, Pager,
  ModPicker, ModSlotCell, ModSlots, EquipButton, AnchoredTooltip, InvFrame, paginate,
});
