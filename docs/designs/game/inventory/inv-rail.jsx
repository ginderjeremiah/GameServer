/* inv-rail.jsx — Direction A · "Loadout Rail" (converged)
   Three columns: a labeled equipped rail (left), the inventory grid (center),
   and a dedicated right column that shows Equipped Totals by default and is
   covered by an item detail/mod drawer when an item is selected. Hovering a
   grid item shows the finalized floating tooltip; drag onto a rail slot to
   equip. Closest evolution of the current screen, with C's stats panel. */

function InventoryRail({ tweaks }) {
  const inv = useInventory('category');
  const { slotSize, perPage, glow, accentBorders } = tweaks;
  const panelRef = React.useRef(null);
  const [hoverCell, setHoverCell] = React.useState(null);
  const selected = inv.selectedId != null ? inv.itemsById[inv.selectedId] : null;

  const { pages, page, slice } = paginate(inv.visible, inv.page, perPage);
  React.useEffect(() => { inv.setPage(0); }, [inv.filterCat, inv.favOnly, inv.sort]);

  const groups = [['armor', 'Armor'], ['arms', 'Arms']];

  return (
    <InvFrame>
      {/* header */}
      <div style={{ padding: '20px 28px 14px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <Diamond size={11} />
        <div>
          <div style={{ fontSize: 21, fontWeight: 500, letterSpacing: -0.3 }}>Inventory</div>
          <MonoLabel color={INV.t4}>Drag to a slot · click to inspect & mod · ⌘-click to equip</MonoLabel>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, display: 'flex', gap: 18, padding: '0 28px 24px' }}>
        {/* equipped rail (slots only) */}
        <div style={{
          width: 300, flexShrink: 0, display: 'flex', flexDirection: 'column',
          background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, padding: 16,
        }}>
          <SectionRule label="Equipped" accent={INV.accent} />
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 16 }}>
            {groups.map(([g, label]) => {
              const slots = EQUIP_SLOTS.filter((s) => s.group === g);
              if (!slots.length) return null;
              return (
                <div key={g}>
                  <div style={{ marginBottom: 8 }}><MonoLabel color={INV.t3} style={{ fontSize: 9 }}>{label}</MonoLabel></div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                    {slots.map((s) => (
                      <EquipDropSlot key={s.id} slot={s} item={inv.equippedBySlot[s.id]} layout="row" size={48}
                        selected={selected && inv.equippedBySlot[s.id] && selected.id === inv.equippedBySlot[s.id].id}
                        dragItem={inv.dragItemId != null ? inv.itemsById[inv.dragItemId] : null}
                        onSelect={(it) => inv.setSelectedId(it.id)}
                        onDropItem={(slotId) => { if (inv.dragItemId != null) { const d = inv.itemsById[inv.dragItemId]; if (s.accepts.includes(d.cat)) inv.equip(d.id, slotId); } }}
                        onUnequip={(slotId) => inv.unequip(slotId)} />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
          {/* future: saved loadouts */}
          <div style={{ marginTop: 14, paddingTop: 14, borderTop: `1px solid ${INV.b1}` }}>
            <button title="Saved loadouts — coming soon" disabled style={{
              width: '100%', padding: '7px 0', fontFamily: INV.sans, fontSize: 11.5, color: INV.t4,
              background: 'transparent', border: `1px dashed ${INV.b2}`, borderRadius: 2, cursor: 'not-allowed',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
            }}>+ Save loadout</button>
          </div>
        </div>

        {/* inventory grid */}
        <div ref={panelRef} style={{
          flex: 1, minWidth: 0, position: 'relative', display: 'flex', flexDirection: 'column',
          background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, overflow: 'hidden',
        }}>
          <div style={{ padding: '14px 16px 10px', borderBottom: `1px solid ${INV.b1}` }}>
            <InvToolbar inv={inv} />
          </div>
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 16 }}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignContent: 'flex-start' }}>
              {slice.map((it) => (
                <GridSlot key={it.id} item={it} size={slotSize} glow={glow} accentBorders={accentBorders}
                  selected={selected && selected.id === it.id}
                  equipped={it.equipSlot != null}
                  onSelect={(x) => inv.setSelectedId(inv.selectedId === x.id ? null : x.id)}
                  onToggleEquip={(x) => inv.toggleEquip(x, inv.equippedBySlot)}
                  onToggleFav={inv.toggleFav}
                  onHover={(x, el) => setHoverCell(x ? { item: x, el } : null)}
                  onDragItem={(x) => { inv.setDragItemId(x.id); setHoverCell(null); }}
                  onDragEnd={() => inv.setDragItemId(null)} />
              ))}
              {slice.length === 0 && (
                <div style={{ width: '100%', textAlign: 'center', padding: 40, color: INV.t4, fontSize: 13 }}>
                  No items match this filter.
                </div>
              )}
            </div>
          </div>
          <div style={{ padding: '10px 16px', borderTop: `1px solid ${INV.b1}` }}>
            <Pager page={page} pages={pages} total={inv.visible.length} onPage={inv.setPage} />
          </div>

          {/* hover tooltip (suppressed while the drawer is open or dragging) */}
          {hoverCell && !selected && inv.dragItemId == null && (
            <AnchoredTooltip item={hoverCell.item} cellEl={hoverCell.el} panelEl={panelRef.current} />
          )}
        </div>

        {/* right column: dedicated Equipped Totals */}
        <div style={{
          width: 330, flexShrink: 0, display: 'flex', flexDirection: 'column',
          background: INV.panel, border: `1px solid ${INV.b1}`, borderRadius: 4, overflow: 'hidden',
        }}>
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: '16px 18px' }}>
            <EquippedStats items={inv.items} />
            <div style={{ marginTop: 18, textAlign: 'center' }}>
              <MonoLabel color={INV.t4} style={{ fontSize: 8.5 }}>Click an item to inspect & mod it</MonoLabel>
            </div>
          </div>
        </div>
      </div>

      {/* item detail — right-side slide-over: covers the right side, dims the
          rest of the page, and closes when you click outside it */}
      <div style={{ position: 'absolute', inset: 0, zIndex: 60, pointerEvents: selected ? 'auto' : 'none' }}>
        <div onClick={() => inv.setSelectedId(null)} style={{
          position: 'absolute', inset: 0, background: 'rgba(0,0,0,0.5)',
          opacity: selected ? 1 : 0, transition: 'opacity 200ms',
        }} />
        <div style={{
          position: 'absolute', top: 0, right: 0, bottom: 0, width: 380,
          background: 'rgba(18,19,25,0.99)', borderLeft: `1px solid ${INV.b2}`,
          boxShadow: '-16px 0 48px rgba(0,0,0,0.5)',
          transform: selected ? 'translateX(0)' : 'translateX(100%)',
          transition: 'transform 220ms cubic-bezier(.4,0,.2,1)',
          display: 'flex', flexDirection: 'column',
        }}>
          {selected && <DrawerDetail item={selected} inv={inv} onClose={() => inv.setSelectedId(null)} />}
        </div>
      </div>
    </InvFrame>
  );
}

function DrawerDetail({ item, inv, onClose }) {
  const acc = catAccent(item.cat);
  return (
    <>
      <div style={{ padding: '16px 18px 14px', borderBottom: `1px solid ${INV.b1}`, borderLeft: `3px solid ${acc}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
          <Diamond size={5} color={acc} />
          <MonoLabel color={acc}>{catName(item.cat)}</MonoLabel>
          <span style={{ marginLeft: 'auto' }}><RarityTag rarity={item.rarity} /></span>
          <button onClick={onClose} style={{ border: 'none', background: 'transparent', color: INV.t3, cursor: 'pointer', fontSize: 16, lineHeight: 1, padding: 0, marginLeft: 4 }}>×</button>
        </div>
        <div style={{ fontSize: 18, fontWeight: 400, letterSpacing: -0.2 }}>{item.name}</div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: '14px 18px' }}>
        <div style={{ marginBottom: 16 }}>
          <SectionRule label="Stats" />
          <StatGrid attrs={item.attrs} />
        </div>
        <div style={{ marginBottom: 16 }}>
          <SectionRule label={`Mod slots · ${item.modSlots.length}`} />
          <ModSlots item={item} onApply={inv.applyMod} onRemove={inv.removeMod} />
        </div>
        {item.desc && (
          <div style={{ fontSize: 11.5, fontStyle: 'italic', color: INV.t3, lineHeight: 1.55 }}>{item.desc}</div>
        )}
      </div>
      <div style={{ padding: 16, borderTop: `1px solid ${INV.b1}` }}>
        <EquipButton item={item} inv={inv} full />
      </div>
    </>
  );
}

Object.assign(window, { InventoryRail, DrawerDetail });
