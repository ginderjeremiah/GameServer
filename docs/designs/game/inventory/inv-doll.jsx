/* inv-doll.jsx — Direction B · "Paper Doll"
   Equipped gear flanks a character silhouette in group columns (data-driven,
   so extra/────split slots just extend a column). Inventory grid sits below.
   Clicking any item opens a centered "Forge" modal — a focused workbench for
   stats, mods, and equip. Hovering still shows the floating tooltip. */

function InventoryDoll({ tweaks }) {
  const inv = useInventory('category');
  const { slotSize, perPage, glow, accentBorders } = tweaks;
  const panelRef = React.useRef(null);
  const [hoverCell, setHoverCell] = React.useState(null);
  const forgeItem = inv.selectedId != null ? inv.itemsById[inv.selectedId] : null;

  const { pages, page, slice } = paginate(inv.visible, inv.page, perPage);
  React.useEffect(() => { inv.setPage(0); }, [inv.filterCat, inv.favOnly, inv.sort]);

  const dragItem = inv.dragItemId != null ? inv.itemsById[inv.dragItemId] : null;
  const colFor = (g) => EQUIP_SLOTS.filter((s) => s.group === g);

  const SlotWithLabel = ({ slot, align }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: align === 'right' ? 'flex-end' : 'flex-start', gap: 4 }}>
      <MonoLabel color={INV.t3} style={{ fontSize: 8.5 }}>{slot.label}</MonoLabel>
      <EquipDropSlot slot={slot} item={inv.equippedBySlot[slot.id]} layout="tile" size={slotSize}
        dragItem={dragItem}
        onSelect={(it) => inv.setSelectedId(it.id)}
        onDropItem={(slotId) => { if (dragItem && slot.accepts.includes(dragItem.cat)) inv.equip(dragItem.id, slotId); }}
        onUnequip={(slotId) => inv.unequip(slotId)}
        onHover={(x, el) => setHoverCell(x ? { item: x, el } : null)} />
    </div>
  );

  return (
    <InvFrame>
      <div style={{ padding: '20px 28px 14px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <Diamond size={11} />
        <div>
          <div style={{ fontSize: 21, fontWeight: 500, letterSpacing: -0.3 }}>Inventory</div>
          <MonoLabel color={INV.t4}>Drag onto a slot to equip · click an item to open the forge</MonoLabel>
        </div>
      </div>

      <div ref={panelRef} style={{ flex: 1, minHeight: 0, position: 'relative', display: 'flex', flexDirection: 'column', padding: '0 28px 24px', gap: 16 }}>
        {/* top: paper doll (left) + equipped stats (right) */}
        <div style={{ display: 'flex', gap: 16 }}>
        <div style={{
          flex: 1, background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4,
          padding: '18px 24px', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 28,
        }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {colFor('armor').map((s) => <SlotWithLabel key={s.id} slot={s} align="right" />)}
          </div>

          {/* silhouette placeholder */}
          <div style={{
            width: 150, alignSelf: 'stretch', minHeight: 220, position: 'relative',
            border: `1px solid ${INV.b1}`, borderRadius: 4, overflow: 'hidden',
            background: 'repeating-linear-gradient(135deg, rgba(255,255,255,0.035) 0 8px, transparent 8px 16px)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <svg width="74" height="150" viewBox="0 0 74 150" fill="none" stroke="rgba(161,194,247,0.35)" strokeWidth="1.4">
              <circle cx="37" cy="24" r="15" />
              <path d="M37 39c-16 0-24 10-24 26v34h48V65c0-16-8-26-24-26z" strokeLinejoin="round" />
              <path d="M13 70L2 96M61 70l11 26M28 99l-4 46M46 99l4 46" strokeLinecap="round" />
            </svg>
            <MonoLabel color={INV.t4} style={{ position: 'absolute', bottom: 8, fontSize: 8.5 }}>Character</MonoLabel>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {colFor('arms').map((s) => <SlotWithLabel key={s.id} slot={s} align="left" />)}
          </div>
        </div>
          <div style={{ width: 300, flexShrink: 0, background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, padding: 16, overflowY: 'auto' }}>
            <EquippedStats items={inv.items} />
          </div>
        </div>

        {/* inventory */}
        <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, overflow: 'hidden' }}>
          <div style={{ padding: '12px 16px 10px', borderBottom: `1px solid ${INV.b1}` }}>
            <InvToolbar inv={inv} />
          </div>
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 14 }}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignContent: 'flex-start' }}>
              {slice.map((it) => (
                <GridSlot key={it.id} item={it} size={slotSize} glow={glow} accentBorders={accentBorders}
                  selected={forgeItem && forgeItem.id === it.id} equipped={it.equipSlot != null}
                  onSelect={(x) => inv.setSelectedId(x.id)}
                  onToggleEquip={(x) => inv.toggleEquip(x, inv.equippedBySlot)}
                  onToggleFav={inv.toggleFav}
                  onHover={(x, el) => setHoverCell(x ? { item: x, el } : null)}
                  onDragItem={(x) => { inv.setDragItemId(x.id); setHoverCell(null); }}
                  onDragEnd={() => inv.setDragItemId(null)} />
              ))}
              {slice.length === 0 && (
                <div style={{ width: '100%', textAlign: 'center', padding: 32, color: INV.t4, fontSize: 13 }}>No items match this filter.</div>
              )}
            </div>
          </div>
          <div style={{ padding: '10px 16px', borderTop: `1px solid ${INV.b1}` }}>
            <Pager page={page} pages={pages} total={inv.visible.length} onPage={inv.setPage} />
          </div>
        </div>

        {hoverCell && !forgeItem && inv.dragItemId == null && (
          <AnchoredTooltip item={hoverCell.item} cellEl={hoverCell.el} panelEl={panelRef.current} />
        )}

        {forgeItem && <ForgeModal item={forgeItem} inv={inv} onClose={() => inv.setSelectedId(null)} />}
      </div>
    </InvFrame>
  );
}

function ForgeModal({ item, inv, onClose }) {
  const acc = catAccent(item.cat);
  return (
    <div onClick={onClose} style={{
      position: 'absolute', inset: 0, zIndex: 80, background: 'rgba(0,0,0,0.55)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 24,
    }}>
      <div onClick={(e) => e.stopPropagation()} style={{
        width: 560, maxHeight: '100%', display: 'flex', flexDirection: 'column',
        background: 'rgba(18,19,25,0.99)', border: `1px solid ${INV.b2}`, borderLeft: `3px solid ${acc}`,
        borderRadius: 4, boxShadow: '0 24px 60px rgba(0,0,0,0.6)', overflow: 'hidden',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '14px 18px', borderBottom: `1px solid ${INV.b1}` }}>
          <MonoLabel color={acc}>Forge</MonoLabel>
          <div style={{ flex: 1 }} />
          <button onClick={onClose} style={{ border: 'none', background: 'transparent', color: INV.t3, cursor: 'pointer', fontSize: 18, lineHeight: 1, padding: 0 }}>×</button>
        </div>
        <div style={{ display: 'flex', minHeight: 0 }}>
          {/* left: item identity + stats */}
          <div style={{ width: 230, flexShrink: 0, padding: 18, borderRight: `1px solid ${INV.b1}`, overflowY: 'auto' }}>
            <div style={{
              width: 70, height: 70, borderRadius: 4, background: hexA(acc, 0.08),
              border: `1px solid ${hexA(acc, 0.5)}`, display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: 12,
            }}>
              <ItemGlyph cat={item.cat} color={acc} size={34} />
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 5 }}>
              <Diamond size={5} color={acc} /><MonoLabel color={acc}>{catName(item.cat)}</MonoLabel>
            </div>
            <div style={{ fontSize: 17, fontWeight: 400, letterSpacing: -0.2, marginBottom: 4 }}>{item.name}</div>
            <div style={{ marginBottom: 14 }}><RarityTag rarity={item.rarity} /></div>
            <SectionRule label="Stats" />
            <StatGrid attrs={item.attrs} />
            {item.desc && <div style={{ marginTop: 14, fontSize: 11.5, fontStyle: 'italic', color: INV.t3, lineHeight: 1.55 }}>{item.desc}</div>}
          </div>
          {/* right: mods + equip */}
          <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
            <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 18 }}>
              <SectionRule label={`Mod slots · ${item.modSlots.length}`} />
              <ModSlots item={item} onApply={inv.applyMod} onRemove={inv.removeMod} />
            </div>
            <div style={{ padding: 16, borderTop: `1px solid ${INV.b1}` }}>
              <EquipButton item={item} inv={inv} full />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { InventoryDoll, ForgeModal });
