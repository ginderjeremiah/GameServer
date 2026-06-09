/* Tooltip Variant A · Refined Card
   Dark glassmorphic restyle of the current card. Same content order
   (title, stats, mods, description, equipped) but quieter chrome, real
   hierarchy, and color-coded numeric values. */

function ItemTooltipA({ item }) {
  return (
    <div style={ttCardShell()}>
      {/* title row */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 12, marginBottom: 12 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
            letterSpacing: 1.6, textTransform: 'uppercase',
            color: 'rgba(192,216,255,0.85)', marginBottom: 3,
          }}>{ITEM_CATEGORIES[item.itemCategoryId]}</div>
          <div style={{ fontSize: 16, fontWeight: 500, color: '#f0f0f0', letterSpacing: -0.1, lineHeight: 1.2 }}>
            {item.name}
          </div>
        </div>
        {item.equipped && <EquippedDot />}
      </div>

      {/* stats */}
      {item.attributes.length > 0 && (
        <Section label="Stats">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {item.attributes.map((a) => (
              <div key={a.name} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }}>
                <span style={{ color: 'rgba(240,240,240,0.75)' }}>{a.name}</span>
                <span style={{
                  fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                  color: statColor(a.value), letterSpacing: 0.3,
                }}>{statSign(a.value, a.suffix || '')}</span>
              </div>
            ))}
          </div>
        </Section>
      )}

      {/* mods */}
      {item.appliedMods.length > 0 && (
        <Section label="Applied mods">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {item.appliedMods.map((m) => (
              <div key={m.name} style={{
                padding: '6px 8px',
                background: 'rgba(255,255,255,0.03)',
                borderLeft: `2px solid ${MOD_TYPE_ACCENT[m.modType]}`,
              }}>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 2 }}>
                  <span style={{ fontSize: 12, fontWeight: 500, color: '#f0f0f0' }}>{m.name}</span>
                  <span style={{
                    fontFamily: 'Geist Mono, monospace', fontSize: 9,
                    letterSpacing: 1, textTransform: 'uppercase',
                    color: MOD_TYPE_ACCENT[m.modType],
                  }}>{MOD_TYPE_LABEL[m.modType]}</span>
                </div>
                <div style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.68)', lineHeight: 1.45 }}>{m.description}</div>
              </div>
            ))}
          </div>
        </Section>
      )}

      {/* description */}
      {item.description && (
        <Section label="Description" last>
          <div style={{
            fontSize: 11.5, fontStyle: 'italic',
            color: 'rgba(240,240,240,0.62)', lineHeight: 1.5,
          }}>{item.description}</div>
        </Section>
      )}
    </div>
  );
}

function SkillTooltipA({ skill }) {
  const s = skillStats(skill);
  const ready = s.remainingCd <= 0.01;

  return (
    <div style={ttCardShell()}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 12, marginBottom: 12 }}>
        <div style={{ flex: 1 }}>
          <div style={{
            fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
            letterSpacing: 1.6, textTransform: 'uppercase',
            color: 'rgba(192,216,255,0.85)', marginBottom: 3,
          }}>Skill</div>
          <div style={{ fontSize: 16, fontWeight: 500, color: '#f0f0f0', letterSpacing: -0.1 }}>
            {skill.name}
          </div>
        </div>
        <CooldownBadge remainingCd={s.remainingCd} adjustedCd={s.adjustedCd} ready={ready} />
      </div>

      <Section label="Damage breakdown">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
          <DmgRow label="Base damage" value={`${skill.baseDamage}`} />
          {skill.damageMultipliers.map((m) => (
            <DmgRow key={m.attribute}
              label={<>+ {m.attribute} <span style={{ color: 'rgba(240,240,240,0.45)' }}>×{m.multiplier}</span></>}
              value={`${m.value}`} positive />
          ))}
          <DmgRow label={<>− Enemy defense</>} value={`${skill.enemyDefense}`} negative />
          <div style={{ height: 1, background: 'rgba(240,240,240,0.1)', margin: '4px 0' }} />
          <DmgRow label="Total" value={`${s.total}`} bold accent />
        </div>
      </Section>

      <Section label="Stats" last>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontFamily: 'Geist Mono, monospace', fontSize: 11, color: 'rgba(240,240,240,0.72)', letterSpacing: 0.5 }}>
          <span>Cooldown · <span style={{ color: '#f0f0f0' }}>{fmt(s.adjustedCd)}s</span></span>
          <span>DPS · <span style={{ color: '#f0f0f0' }}>{fmt(s.dps)}</span></span>
        </div>
        {skill.description && (
          <div style={{
            marginTop: 8, fontSize: 11.5, fontStyle: 'italic',
            color: 'rgba(240,240,240,0.6)', lineHeight: 1.5,
          }}>{skill.description}</div>
        )}
      </Section>
    </div>
  );
}

/* ─── shared chrome ────────────────────────────────────────────────── */

function ttCardShell() {
  return {
    width: 260,
    background: 'rgba(20,21,27,0.96)',
    border: '1px solid rgba(255,255,255,0.14)',
    borderRadius: 3,
    boxShadow: '0 12px 28px rgba(0,0,0,0.55), 0 0 0 1px rgba(0,0,0,0.4)',
    color: '#f0f0f0',
    padding: '14px 16px',
    backdropFilter: 'blur(6px)',
  };
}

function Section({ label, children, last }) {
  return (
    <div style={{ marginBottom: last ? 0 : 12 }}>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9.5,
        letterSpacing: 1.6, textTransform: 'uppercase',
        color: 'rgba(240,240,240,0.45)',
        marginBottom: 6,
      }}>{label}</div>
      {children}
    </div>
  );
}

function EquippedDot() {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 6,
      padding: '2px 8px',
      background: 'rgba(189,224,180,0.1)',
      border: '1px solid rgba(189,224,180,0.45)',
      borderRadius: 2,
    }}>
      <div style={{
        width: 5, height: 5, borderRadius: '50%',
        background: '#bde0b4',
        boxShadow: '0 0 4px rgba(189,224,180,0.9)',
      }} />
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9,
        color: '#bde0b4', letterSpacing: 1.2, textTransform: 'uppercase',
      }}>Equipped</span>
    </div>
  );
}

function CooldownBadge({ remainingCd, adjustedCd, ready }) {
  return (
    <div style={{
      padding: '4px 8px',
      background: ready ? 'rgba(189,224,180,0.12)' : 'rgba(161,194,247,0.1)',
      border: `1px solid ${ready ? 'rgba(189,224,180,0.4)' : 'rgba(161,194,247,0.35)'}`,
      borderRadius: 2,
      fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
      color: ready ? '#bde0b4' : '#c0d8ff',
      letterSpacing: 0.5, whiteSpace: 'nowrap',
    }}>
      {ready ? 'READY' : `${fmt(remainingCd)}s / ${fmt(adjustedCd)}s`}
    </div>
  );
}

function DmgRow({ label, value, accent, bold, positive, negative }) {
  return (
    <div style={{
      display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
      fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
      color: bold ? '#f0f0f0' : 'rgba(240,240,240,0.72)',
      fontWeight: bold ? 600 : 400,
      letterSpacing: 0.3,
    }}>
      <span>{label}</span>
      <span style={{
        color: accent ? '#a1c2f7' : positive ? '#bde0b4' : negative ? '#f0a094' : 'inherit',
      }}>{value}</span>
    </div>
  );
}

window.ItemTooltipA = ItemTooltipA;
window.SkillTooltipA = SkillTooltipA;
