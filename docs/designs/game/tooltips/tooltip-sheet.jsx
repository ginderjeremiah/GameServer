/* Tooltip Variant B · Data Sheet
   More structured / spec-sheet feel. Header bar with mono name. Stats and
   damage breakdown in 2-column tables with right-aligned numeric values.
   Mods stacked compactly. Reads like an inspection panel. */

function ItemTooltipB({ item }) {
  return (
    <div style={ttSheetShell()}>
      {/* header */}
      <div style={{
        padding: '10px 14px',
        borderBottom: '1px solid rgba(255,255,255,0.1)',
        background: 'rgba(255,255,255,0.02)',
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10 }}>
          <span style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 13.5,
            color: '#f0f0f0', letterSpacing: 0.4,
          }}>{item.name}</span>
          {item.equipped && (
            <span style={{
              fontFamily: 'Geist Mono, monospace', fontSize: 9,
              color: '#bde0b4', letterSpacing: 1.4, textTransform: 'uppercase',
              padding: '2px 6px', border: '1px solid rgba(189,224,180,0.4)',
              borderRadius: 2,
            }}>Equipped</span>
          )}
        </div>
        <div style={{
          marginTop: 3,
          fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
          letterSpacing: 1.5, textTransform: 'uppercase',
          color: 'rgba(240,240,240,0.5)',
        }}>{ITEM_CATEGORIES[item.itemCategoryId]} · ID {String(101).padStart(4, '0')}</div>
      </div>

      <div style={{ padding: 14 }}>
        {/* stats table */}
        {item.attributes.length > 0 && (
          <SheetSection label="Stats">
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <tbody>
                {item.attributes.map((a) => (
                  <tr key={a.name}>
                    <td style={tdLabel}>{a.name}</td>
                    <td style={{
                      ...tdValue,
                      color: statColor(a.value),
                    }}>{statSign(a.value, a.suffix || '')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </SheetSection>
        )}

        {/* mods */}
        {item.appliedMods.length > 0 && (
          <SheetSection label={`Applied mods · ${item.appliedMods.length}`}>
            <div style={{ display: 'flex', flexDirection: 'column' }}>
              {item.appliedMods.map((m, i) => (
                <div key={m.name} style={{
                  padding: '6px 0',
                  borderTop: i > 0 ? '1px solid rgba(240,240,240,0.06)' : 'none',
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 2 }}>
                    <span style={{
                      fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                      color: '#f0f0f0', letterSpacing: 0.3,
                    }}>{m.name}</span>
                    <span style={{
                      fontFamily: 'Geist Mono, monospace', fontSize: 9,
                      letterSpacing: 1.2, textTransform: 'uppercase',
                      color: MOD_TYPE_ACCENT[m.modType],
                    }}>{MOD_TYPE_LABEL[m.modType]}</span>
                  </div>
                  <div style={{ fontSize: 11, color: 'rgba(240,240,240,0.62)', lineHeight: 1.5 }}>{m.description}</div>
                </div>
              ))}
            </div>
          </SheetSection>
        )}

        {/* description */}
        {item.description && (
          <SheetSection label="Description" last>
            <div style={{
              fontSize: 11, fontStyle: 'italic',
              color: 'rgba(240,240,240,0.55)', lineHeight: 1.55,
            }}>{item.description}</div>
          </SheetSection>
        )}
      </div>
    </div>
  );
}

function SkillTooltipB({ skill }) {
  const s = skillStats(skill);
  const ready = s.remainingCd <= 0.01;

  return (
    <div style={ttSheetShell()}>
      <div style={{
        padding: '10px 14px',
        borderBottom: '1px solid rgba(255,255,255,0.1)',
        background: 'rgba(255,255,255,0.02)',
        display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 10,
      }}>
        <div>
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 13.5,
            color: '#f0f0f0', letterSpacing: 0.4,
          }}>{skill.name}</div>
          <div style={{
            marginTop: 3,
            fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
            letterSpacing: 1.5, textTransform: 'uppercase',
            color: 'rgba(240,240,240,0.5)',
          }}>Skill · cd {fmt(s.adjustedCd)}s</div>
        </div>
        <CdPipDisplay remaining={s.remainingCd} total={s.adjustedCd} ready={ready} />
      </div>

      <div style={{ padding: 14 }}>
        <SheetSection label="Damage">
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <tbody>
              <tr>
                <td style={tdLabel}>Base</td>
                <td style={tdValue}>{skill.baseDamage}</td>
              </tr>
              {skill.damageMultipliers.map((m) => (
                <tr key={m.attribute}>
                  <td style={tdLabel}>
                    {m.attribute} <span style={{ color: 'rgba(240,240,240,0.4)' }}>×{m.multiplier}</span>
                  </td>
                  <td style={{ ...tdValue, color: '#bde0b4' }}>+{m.value}</td>
                </tr>
              ))}
              <tr>
                <td style={tdLabel}>Enemy defense</td>
                <td style={{ ...tdValue, color: '#f0a094' }}>−{skill.enemyDefense}</td>
              </tr>
              <tr style={{ borderTop: '1px solid rgba(240,240,240,0.12)' }}>
                <td style={{ ...tdLabel, paddingTop: 6, color: '#f0f0f0', fontWeight: 500 }}>Total</td>
                <td style={{ ...tdValue, paddingTop: 6, color: '#a1c2f7', fontWeight: 600, fontSize: 13 }}>{s.total}</td>
              </tr>
            </tbody>
          </table>
        </SheetSection>

        <SheetSection label="Output">
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <tbody>
              <tr>
                <td style={tdLabel}>DPS</td>
                <td style={tdValue}>{fmt(s.dps)}</td>
              </tr>
              <tr>
                <td style={tdLabel}>Cooldown</td>
                <td style={tdValue}>{fmt(s.adjustedCd)}s</td>
              </tr>
            </tbody>
          </table>
        </SheetSection>

        {skill.description && (
          <SheetSection label="Description" last>
            <div style={{
              fontSize: 11, fontStyle: 'italic',
              color: 'rgba(240,240,240,0.55)', lineHeight: 1.55,
            }}>{skill.description}</div>
          </SheetSection>
        )}
      </div>
    </div>
  );
}

/* ── chrome ─────────────────────────────────────────────────────── */

function ttSheetShell() {
  return {
    width: 260,
    background: 'rgba(20,21,27,0.96)',
    border: '1px solid rgba(255,255,255,0.14)',
    borderRadius: 3,
    boxShadow: '0 12px 28px rgba(0,0,0,0.55), 0 0 0 1px rgba(0,0,0,0.4)',
    color: '#f0f0f0',
    overflow: 'hidden',
    backdropFilter: 'blur(6px)',
  };
}

function SheetSection({ label, children, last }) {
  return (
    <div style={{ marginBottom: last ? 0 : 11 }}>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
        letterSpacing: 1.5, textTransform: 'uppercase',
        color: 'rgba(192,216,255,0.7)',
        marginBottom: 5,
        borderBottom: '1px solid rgba(240,240,240,0.08)',
        paddingBottom: 3,
      }}>{label}</div>
      {children}
    </div>
  );
}

const tdLabel = {
  fontFamily: 'Geist, sans-serif', fontSize: 11.5,
  color: 'rgba(240,240,240,0.72)',
  padding: '3px 0', letterSpacing: 0.1,
};
const tdValue = {
  fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
  textAlign: 'right',
  padding: '3px 0', letterSpacing: 0.3,
  color: '#f0f0f0',
};

function CdPipDisplay({ remaining, total, ready }) {
  // small pip row showing cooldown elapsed/remaining
  const pips = 8;
  const filled = ready ? pips : Math.max(0, Math.round(((total - remaining) / total) * pips));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3 }}>
      <div style={{ display: 'flex', gap: 2 }}>
        {Array.from({ length: pips }).map((_, i) => (
          <div key={i} style={{
            width: 6, height: 12,
            background: i < filled
              ? (ready ? '#bde0b4' : '#a1c2f7')
              : 'rgba(240,240,240,0.14)',
            transition: 'background 160ms',
          }} />
        ))}
      </div>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9,
        color: ready ? '#bde0b4' : 'rgba(240,240,240,0.55)',
        letterSpacing: 1.2, textTransform: 'uppercase',
      }}>{ready ? 'Ready' : `${fmt(remaining)}s`}</span>
    </div>
  );
}

window.ItemTooltipB = ItemTooltipB;
window.SkillTooltipB = SkillTooltipB;
