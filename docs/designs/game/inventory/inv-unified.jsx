/* inv-unified.jsx — Direction C · "Unified Workbench"
   A compact equipped strip + grid on the left, and a PERSISTENT inspector on
   the right that always shows the hovered (preview) or selected (pinned) item
   with its stats and INLINE editable mod slots. No drawers, no modals — the
   fewest clicks to inspect and mod. Recommended convergence pick. */

function InventoryUnified({ tweaks }) {
  const inv = useInventory('category');
  const { slotSize, perPage, glow, accentBorders } = tweaks;
  const [hoverId, setHoverId] = React.useState(null);

  const { pages, page, slice } = paginate(inv.visible, inv.page, perPage);
  React.useEffect(() => { inv.setPage(0); }, [inv.filterCat, inv.favOnly, inv.sort]);

  const selected = inv.selectedId != null ? inv.itemsById[inv.selectedId] : null;
  const hovered = hoverId != null ? inv.itemsById[hoverId] : null;
  const inspectorItem = selected || hovered;
  const pinned = !!selected;
  const dragItem = inv.dragItemId != null ? inv.itemsById[inv.dragItemId] : null;
  const groups = [['armor', 'Armor'], ['arms', 'Arms']];

  return (
    <InvFrame>
      <div style={{ padding: '20px 28px 14px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <Diamond size={11} />
        <div>
          <div style={{ fontSize: 21, fontWeight: 500, letterSpacing: -0.3 }}>Inventory</div>
          <MonoLabel color={INV.t4}>Hover to preview · click to pin & mod · drag to equip</MonoLabel>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, display: 'flex', gap: 18, padding: '0 28px 24px' }}>
        {/* left: vertical equipped list (kept from A) */}
        <div style={{
          width: 300, flexShrink: 0, display: 'flex', flexDirection: 'column',
          background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, padding: 16,
        }}>
          <SectionRule label="Equipped" accent={INV.accent} />
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 14 }}>
            {groups.map(([g, label]) => {
              const slots = EQUIP_SLOTS.filter((s) => s.group === g);
              if (!slots.length) return null;
              return (
                <div key={g}>
                  <div style={{ marginBottom: 8 }}><MonoLabel color={INV.t3} style={{ fontSize: 9 }}>{label}</MonoLabel></div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                    {slots.map((s) => {
                      const it = inv.equippedBySlot[s.id];
                      return (
                        <EquipDropSlot key={s.id} slot={s} item={it} layout="row" size={46}
                          selected={selected && it && selected.id === it.id}
                          dragItem={dragItem}
                          onSelect={(x) => inv.setSelectedId(x.id)}
                          onDropItem={(slotId) => { if (dragItem && s.accepts.includes(dragItem.cat)) inv.equip(dragItem.id, slotId); }}
                          onUnequip={(slotId) => inv.unequip(slotId)}
                          onHover={(x) => setHoverId(x ? x.id : null)} />
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* center: grid */}
        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, overflow: 'hidden' }}>
          <div style={{ padding: '12px 16px 10px', borderBottom: `1px solid ${INV.b1}` }}>
            <InvToolbar inv={inv} />
          </div>
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 14 }}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignContent: 'flex-start' }}>
              {slice.map((it) => (
                <GridSlot key={it.id} item={it} size={slotSize} glow={glow} accentBorders={accentBorders}
                  selected={selected && selected.id === it.id} equipped={it.equipSlot != null}
                  onSelect={(x) => inv.setSelectedId(inv.selectedId === x.id ? null : x.id)}
                  onToggleEquip={(x) => inv.toggleEquip(x, inv.equippedBySlot)}
                  onToggleFav={inv.toggleFav}
                  onHover={(x) => setHoverId(x ? x.id : null)}
                  onDragItem={(x) => { inv.setDragItemId(x.id); }}
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

        {/* right: persistent panel — equipped totals by default, item on hover/select */}
        <div style={{
          width: 330, flexShrink: 0, display: 'flex', flexDirection: 'column',
          background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, overflow: 'hidden',
        }}>
          {inspectorItem
            ? <Inspector item={inspectorItem} inv={inv} pinned={pinned} onClose={() => inv.setSelectedId(null)} />
            : (
              <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '16px 18px' }}>
                <EquippedStats items={inv.items} />
                <div style={{ marginTop: 18, textAlign: 'center' }}>
                  <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>Hover or click an item to inspect it</MonoLabel>
                </div>
              </div>
            )}
        </div>
      </div>
    </InvFrame>
  );
}

function InspectorEmpty() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12, padding: 24 }}>
      <div style={{ width: 56, height: 56, borderRadius: 3, border: `1px dashed ${INV.b2}`, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Diamond size={12} color={INV.t4} filled={false} />
      </div>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: 14, color: INV.t2 }}>Nothing selected</div>
        <MonoLabel color={INV.t4} style={{ display: 'block', marginTop: 4 }}>Hover or click an item</MonoLabel>
      </div>
    </div>
  );
}

function Inspector({ item, inv, pinned, onClose }) {
  const acc = catAccent(item.cat);
  return (
    <>
      <div style={{ padding: '16px 18px 14px', borderBottom: `1px solid ${INV.b1}`, borderLeft: `3px solid ${acc}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
          <Diamond size={5} color={acc} />
          <MonoLabel color={acc}>{catName(item.cat)}</MonoLabel>
          {item.equipSlot != null && (
            <span style={{ display: 'flex', alignItems: 'center', gap: 5, marginLeft: 4 }}>
              <Diamond size={4} color={INV.success} />
              <MonoLabel color={INV.success} style={{ fontSize: 8.5 }}>Equipped</MonoLabel>
            </span>
          )}
          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
            <button onClick={() => inv.toggleFav(item.id)} title={item.fav ? 'Unfavorite' : 'Favorite'}
              style={{ border: 'none', background: 'transparent', cursor: 'pointer', padding: 0, display: 'flex' }}>
              <svg width="13" height="13" viewBox="0 0 16 16" fill={item.fav ? INV.gold : 'none'} stroke={INV.gold} strokeWidth="1.3">
                <path d="M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z" strokeLinejoin="round" />
              </svg>
            </button>
            {pinned && <button onClick={onClose} style={{ border: 'none', background: 'transparent', color: INV.t3, cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: 0 }}>×</button>}
          </div>
        </div>
        <div style={{ fontSize: 18, fontWeight: 400, letterSpacing: -0.2, lineHeight: 1.15 }}>{item.name}</div>
        <div style={{ marginTop: 6 }}><RarityTag rarity={item.rarity} /></div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '14px 18px' }}>
        <div style={{ marginBottom: 16 }}>
          <SectionRule label="Stats" />
          <StatGrid attrs={item.attrs} />
        </div>
        <div style={{ marginBottom: 16 }}>
          <SectionRule label={`Mod slots · ${item.modSlots.length}`} />
          {pinned
            ? <ModSlots item={item} onApply={inv.applyMod} onRemove={inv.removeMod} />
            : <ModSlotsPreview item={item} />}
          {!pinned && item.modSlots.length > 0 && (
            <div style={{ marginTop: 8, textAlign: 'center' }}>
              <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>Click the item to edit mods</MonoLabel>
            </div>
          )}
        </div>
        {item.desc && <div style={{ fontSize: 11.5, fontStyle: 'italic', color: INV.t3, lineHeight: 1.55 }}>{item.desc}</div>}
      </div>

      <div style={{ padding: 16, borderTop: `1px solid ${INV.b1}` }}>
        <EquipButton item={item} inv={inv} full />
      </div>
    </>
  );
}

/* read-only mod view when only hovering (not pinned) */
function ModSlotsPreview({ item }) {
  if (!item.modSlots || item.modSlots.length === 0) {
    return <div style={{ fontSize: 11.5, color: INV.t4, fontStyle: 'italic' }}>This item has no mod slots.</div>;
  }
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      {item.modSlots.map((s) => {
        const modId = item.applied?.[s.id];
        const mod = modId != null ? MODS[modId] : null;
        const acc = MOD_TYPE_ACCENT[s.type];
        return (
          <div key={s.id} style={{
            padding: '7px 9px', borderLeft: `2px solid ${acc}`, borderRadius: 2,
            background: mod ? INV.fill : 'transparent', border: `1px ${mod ? 'solid' : 'dashed'} ${mod ? INV.b1 : INV.b2}`, borderLeftWidth: 2, borderLeftColor: acc,
          }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 500, color: mod ? INV.text : INV.t3 }}>{mod ? mod.name : 'Empty slot'}</span>
              <MonoLabel color={acc} style={{ fontSize: 8.5 }}>{MOD_TYPE_LABEL[s.type]}</MonoLabel>
            </div>
            {mod && <div style={{ fontSize: 11, color: INV.t3, lineHeight: 1.45, marginTop: 2 }}>{mod.description}</div>}
          </div>
        );
      })}
    </div>
  );
}

Object.assign(window, { InventoryUnified, Inspector, InspectorEmpty, ModSlotsPreview });
