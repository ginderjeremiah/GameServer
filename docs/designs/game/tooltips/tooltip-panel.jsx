/* Tooltip Variant C · Layered Panel
   Left accent strip colored by category (or blue for skills). Larger title,
   sections separated by faint dividers with mono small-caps headers. Most
   visual hierarchy, biggest "in-game" feel. */

function ItemTooltipC({ item }) {
  const accent = CATEGORY_ACCENT[item.itemCategoryId];
  return (
    <div style={ttPanelShell(accent)}>
      {/* title */}
      <div style={{
        padding: '14px 16px 12px',
        borderBottom: '1px solid rgba(240,240,240,0.08)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
          <div style={{
            width: 5, height: 5, transform: 'rotate(45deg)',
            background: accent, boxShadow: `0 0 6px ${accent}aa`,
          }} />
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
            letterSpacing: 1.8, textTransform: 'uppercase',
            color: accent,
          }}>{ITEM_CATEGORIES[item.itemCategoryId]}</div>
          {item.equipped && (
            <span style={{
              marginLeft: 'auto',
              fontFamily: 'Geist Mono, monospace', fontSize: 9,
              color: '#bde0b4', letterSpacing: 1.3, textTransform: 'uppercase',
              display: 'flex', alignItems: 'center', gap: 4,
            }}>
              <div style={{ width: 5, height: 5, borderRadius: '50%', background: '#bde0b4', boxShadow: '0 0 4px rgba(189,224,180,0.9)' }} />
              Equipped
            </span>
          )}
        </div>
        <div style={{
          fontSize: 18, fontWeight: 400, color: '#f0f0f0',
          letterSpacing: -0.2, lineHeight: 1.15,
        }}>{item.name}</div>
      </div>

      <div style={{ padding: '12px 16px 14px' }}>
        {item.attributes.length > 0 && (
          <PanelSection label="Stats">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 4, columnGap: 12 }}>
              {item.attributes.map((a) => (
                <React.Fragment key={a.name}>
                  <div style={{ fontSize: 12, color: 'rgba(240,240,240,0.75)' }}>{a.name}</div>
                  <div style={{
                    fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                    color: statColor(a.value), letterSpacing: 0.3, textAlign: 'right',
                  }}>{statSign(a.value, a.suffix || '')}</div>
                </React.Fragment>
              ))}
            </div>
          </PanelSection>
        )}

        {item.appliedMods.length > 0 && (
          <PanelSection label="Applied mods">
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {item.appliedMods.map((m) => (
                <div key={m.name}>
                  <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 2 }}>
                    <span style={{
                      fontFamily: 'Geist Mono, monospace', fontSize: 8.5,
                      letterSpacing: 1.2, textTransform: 'uppercase',
                      color: MOD_TYPE_ACCENT[m.modType],
                      padding: '1px 5px',
                      border: `1px solid ${MOD_TYPE_ACCENT[m.modType]}55`,
                      borderRadius: 2,
                    }}>{MOD_TYPE_LABEL[m.modType]}</span>
                    <span style={{ fontSize: 12, fontWeight: 500, color: '#f0f0f0' }}>{m.name}</span>
                  </div>
                  <div style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.65)', lineHeight: 1.5, paddingLeft: 2 }}>{m.description}</div>
                </div>
              ))}
            </div>
          </PanelSection>
        )}

        {item.description && (
          <PanelSection label="Description" last>
            <div style={{
              fontSize: 11.5, fontStyle: 'italic',
              color: 'rgba(240,240,240,0.6)', lineHeight: 1.55,
            }}>{item.description}</div>
          </PanelSection>
        )}
      </div>
    </div>
  );
}

function SkillTooltipC({ skill }) {
  const s = skillStats(skill);
  const ready = s.remainingCd <= 0.01;
  const accent = '#a1c2f7';

  return (
    <div style={ttPanelShell(accent)}>
      {/* title */}
      <div style={{
        padding: '14px 16px 12px',
        borderBottom: '1px solid rgba(240,240,240,0.08)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
          <div style={{
            width: 5, height: 5, transform: 'rotate(45deg)',
            background: accent, boxShadow: `0 0 6px ${accent}aa`,
          }} />
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
            letterSpacing: 1.8, textTransform: 'uppercase',
            color: accent,
          }}>Skill</div>
          <div style={{ marginLeft: 'auto' }}>
            <CooldownPill remaining={s.remainingCd} total={s.adjustedCd} ready={ready} />
          </div>
        </div>
        <div style={{ fontSize: 18, fontWeight: 400, color: '#f0f0f0', letterSpacing: -0.2 }}>
          {skill.name}
        </div>
      </div>

      <div style={{ padding: '12px 16px 14px' }}>
        <PanelSection label="Damage breakdown">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
            <DmgRowC label="Base" value={skill.baseDamage} />
            {skill.damageMultipliers.map((m) => (
              <DmgRowC key={m.attribute}
                label={<>{m.attribute} <span style={{ color: 'rgba(240,240,240,0.45)' }}>×{m.multiplier}</span></>}
                value={`+${m.value}`} positive />
            ))}
            <DmgRowC label="Enemy defense" value={`−${skill.enemyDefense}`} negative />
          </div>
          <div style={{
            marginTop: 8, paddingTop: 8,
            borderTop: '1px solid rgba(240,240,240,0.08)',
            display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
          }}>
            <span style={{
              fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
              letterSpacing: 1.6, textTransform: 'uppercase',
              color: 'rgba(240,240,240,0.5)',
            }}>Total</span>
            <span style={{
              fontSize: 22, fontWeight: 500, color: accent,
              fontFamily: 'Geist, sans-serif', letterSpacing: -0.4,
            }}>{s.total}</span>
          </div>
        </PanelSection>

        <PanelSection label="Tempo">
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <Metric label="Cooldown" value={`${fmt(s.adjustedCd)}s`} />
            <Metric label="DPS" value={fmt(s.dps)} />
          </div>
        </PanelSection>

        {skill.description && (
          <PanelSection label="Description" last>
            <div style={{
              fontSize: 11.5, fontStyle: 'italic',
              color: 'rgba(240,240,240,0.6)', lineHeight: 1.55,
            }}>{skill.description}</div>
          </PanelSection>
        )}
      </div>
    </div>
  );
}

/* ── chrome ─────────────────────────────────────────────────────── */

function ttPanelShell(accent) {
  return {
    width: 280,
    background: 'rgba(20,21,27,0.96)',
    border: '1px solid rgba(255,255,255,0.14)',
    borderLeft: `3px solid ${accent}`,
    borderRadius: 3,
    boxShadow: `0 12px 28px rgba(0,0,0,0.55), 0 0 0 1px rgba(0,0,0,0.4), -4px 0 16px ${accent}22`,
    color: '#f0f0f0',
    overflow: 'hidden',
    backdropFilter: 'blur(6px)',
  };
}

function PanelSection({ label, children, last }) {
  return (
    <div style={{ marginBottom: last ? 0 : 12 }}>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
        letterSpacing: 1.8, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.4)',
        marginBottom: 7,
        display: 'flex', alignItems: 'center', gap: 8,
      }}>
        {label}
        <div style={{ flex: 1, height: 1, background: 'rgba(240,240,240,0.06)' }} />
      </div>
      {children}
    </div>
  );
}

function DmgRowC({ label, value, positive, negative }) {
  const color = positive ? '#bde0b4' : negative ? '#f0a094' : '#f0f0f0';
  return (
    <div style={{
      display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
      fontSize: 11.5,
    }}>
      <span style={{ color: 'rgba(240,240,240,0.78)' }}>{label}</span>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
        color, letterSpacing: 0.3,
      }}>{value}</span>
    </div>
  );
}

function Metric({ label, value }) {
  return (
    <div style={{
      padding: '7px 10px',
      background: 'rgba(255,255,255,0.03)',
      border: '1px solid rgba(255,255,255,0.08)',
      borderRadius: 2,
    }}>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9,
        letterSpacing: 1.3, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.5)', marginBottom: 2,
      }}>{label}</div>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 14,
        color: '#f0f0f0', letterSpacing: 0.3,
      }}>{value}</div>
    </div>
  );
}

function CooldownPill({ remaining, total, ready }) {
  const progress = ready ? 1 : Math.max(0, (total - remaining) / total);
  return (
    <div style={{
      position: 'relative',
      width: 56, height: 18,
      background: 'rgba(240,240,240,0.06)',
      border: '1px solid rgba(255,255,255,0.12)',
      borderRadius: 2,
      overflow: 'hidden',
    }}>
      <div style={{
        position: 'absolute', inset: 0,
        width: `${progress * 100}%`,
        background: ready
          ? 'linear-gradient(90deg, rgba(189,224,180,0.5), rgba(189,224,180,0.2))'
          : 'linear-gradient(90deg, rgba(161,194,247,0.45), rgba(161,194,247,0.15))',
      }} />
      <div style={{
        position: 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontFamily: 'Geist Mono, monospace', fontSize: 9,
        color: ready ? '#bde0b4' : '#c0d8ff', letterSpacing: 0.6,
      }}>{ready ? 'READY' : `${fmt(remaining)}s`}</div>
    </div>
  );
}

window.ItemTooltipC = ItemTooltipC;
window.SkillTooltipC = SkillTooltipC;
