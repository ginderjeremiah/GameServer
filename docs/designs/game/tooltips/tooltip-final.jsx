/* Final tooltip designs — synthesized from feedback.

   ITEM = Variant C base with A's mod tiles + A's EQUIPPED badge.
   SKILL = Variant C base with a smaller bolded "Total" row (like B/A).

   Skill exists in two flavors so the user can compare cooldown indicators
   live: <FinalSkillTooltipPips> and <FinalSkillTooltipPill>. Both share an
   `useAnimatedSkill` hook that runs the cooldown 0 → ready on a loop. */

function useAnimatedSkill(baseSkill, { holdReadyMs = 1100 } = {}) {
  const [, force] = React.useReducer((x) => x + 1, 0);
  const startRef = React.useRef(performance.now());
  React.useEffect(() => {
    let raf;
    const loop = () => { force(); raf = requestAnimationFrame(loop); };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, []);

  const adjustedCdMs = baseSkill.cooldownMs / baseSkill.cdMultiplier;
  const cycleMs = adjustedCdMs + holdReadyMs;
  const elapsed = (performance.now() - startRef.current) % cycleMs;
  const remainingCdMs = elapsed < adjustedCdMs ? adjustedCdMs - elapsed : 0;
  return { ...baseSkill, remainingCdMs };
}

/* ─── Final Item tooltip ─────────────────────────────────────────── */

function FinalItemTooltip({ item }) {
  const accent = CATEGORY_ACCENT[item.itemCategoryId];
  return (
    <div style={ttFinalShell(accent)}>
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
            letterSpacing: 1.8, textTransform: 'uppercase', color: accent,
          }}>{ITEM_CATEGORIES[item.itemCategoryId]}</div>
          {item.equipped && (
            <div style={{ marginLeft: 'auto' }}>
              <EquippedBadge />
            </div>
          )}
        </div>
        <div style={{
          fontSize: 18, fontWeight: 400, color: '#f0f0f0',
          letterSpacing: -0.2, lineHeight: 1.15,
        }}>{item.name}</div>
      </div>

      <div style={{ padding: '12px 16px 14px' }}>
        {/* stats */}
        {item.attributes.length > 0 && (
          <FinalSection label="Stats">
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', rowGap: 4, columnGap: 12 }}>
              {item.attributes.map((a) => (
                <React.Fragment key={a.name}>
                  <div style={{ fontSize: 12, color: 'rgba(240,240,240,0.78)' }}>{a.name}</div>
                  <div style={{
                    fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
                    color: statColor(a.value), letterSpacing: 0.3, textAlign: 'right',
                  }}>{statSign(a.value, a.suffix || '')}</div>
                </React.Fragment>
              ))}
            </div>
          </FinalSection>
        )}

        {/* mods — show EVERY slot, filled or empty (uses modSlots when the
            caller provides it; falls back to the old appliedMods contract) */}
        {(() => {
          const slots = item.modSlots
            ? item.modSlots
            : (item.appliedMods || []).map((m) => ({ modType: m.modType, mod: m }));
          if (slots.length === 0) return null;
          const filled = slots.filter((s) => s.mod).length;
          return (
            <FinalSection label={`Mods · ${filled}/${slots.length}`}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {slots.map((s, i) => {
                  const mAcc = MOD_TYPE_ACCENT[s.modType];
                  return s.mod ? (
                    <div key={i} style={{
                      padding: '6px 10px',
                      background: 'rgba(255,255,255,0.03)',
                      borderLeft: `2px solid ${mAcc}`,
                    }}>
                      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 2 }}>
                        <span style={{ fontSize: 12, fontWeight: 500, color: '#f0f0f0' }}>{s.mod.name}</span>
                        <span style={{
                          fontFamily: 'Geist Mono, monospace', fontSize: 9,
                          letterSpacing: 1.2, textTransform: 'uppercase', color: mAcc,
                        }}>{MOD_TYPE_LABEL[s.modType]}</span>
                      </div>
                      <div style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.65)', lineHeight: 1.5 }}>
                        {s.mod.description}
                      </div>
                    </div>
                  ) : (
                    <div key={i} style={{
                      padding: '6px 10px',
                      border: '1px dashed rgba(255,255,255,0.14)',
                      borderLeft: `2px solid ${mAcc}`,
                      display: 'flex', alignItems: 'center', gap: 8,
                    }}>
                      <span style={{ fontSize: 11.5, color: 'rgba(240,240,240,0.5)', fontStyle: 'italic' }}>Empty slot</span>
                      <span style={{
                        fontFamily: 'Geist Mono, monospace', fontSize: 9,
                        letterSpacing: 1.2, textTransform: 'uppercase', color: mAcc,
                      }}>{MOD_TYPE_LABEL[s.modType]}</span>
                    </div>
                  );
                })}
              </div>
            </FinalSection>
          );
        })()}

        {/* description */}
        {item.description && (
          <FinalSection label="Description" last>
            <div style={{
              fontSize: 11.5, fontStyle: 'italic',
              color: 'rgba(240,240,240,0.6)', lineHeight: 1.55,
            }}>{item.description}</div>
          </FinalSection>
        )}
      </div>
    </div>
  );
}

/* ─── Final Skill tooltip (parameterized by cooldown indicator) ─── */

function FinalSkillTooltipBase({ skill, cooldownIndicator: CooldownIndicator }) {
  const animated = useAnimatedSkill(skill);
  const s = skillStats(animated);
  const ready = s.remainingCd <= 0.01;
  const accent = '#a1c2f7';

  return (
    <div style={ttFinalShell(accent)}>
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
            letterSpacing: 1.8, textTransform: 'uppercase', color: accent,
          }}>Skill</div>
          <div style={{ marginLeft: 'auto' }}>
            <CooldownIndicator remaining={s.remainingCd} total={s.adjustedCd} ready={ready} />
          </div>
        </div>
        <div style={{ fontSize: 18, fontWeight: 400, color: '#f0f0f0', letterSpacing: -0.2 }}>
          {skill.name}
        </div>
      </div>

      <div style={{ padding: '12px 16px 14px' }}>
        <FinalSection label="Damage breakdown">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
            <FinalDmgRow label="Base" value={skill.baseDamage} />
            {skill.damageMultipliers.map((m) => (
              <FinalDmgRow key={m.attribute}
                label={<>{m.attribute} <span style={{ color: 'rgba(240,240,240,0.45)' }}>×{m.multiplier}</span></>}
                value={`+${m.value}`} positive />
            ))}
            <FinalDmgRow label="Enemy defense" value={`−${skill.enemyDefense}`} negative />
            <div style={{
              marginTop: 4, paddingTop: 6,
              borderTop: '1px solid rgba(240,240,240,0.1)',
              display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
              fontSize: 13, fontWeight: 600, color: '#f0f0f0',
            }}>
              <span>Total</span>
              <span style={{
                fontFamily: 'Geist Mono, monospace',
                color: accent, letterSpacing: 0.3,
              }}>{s.total}</span>
            </div>
          </div>
        </FinalSection>

        <FinalSection label="Tempo">
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <FinalMetric label="Cooldown" value={`${fmt(s.adjustedCd)}s`} />
            <FinalMetric label="DPS" value={fmt(s.dps)} />
          </div>
        </FinalSection>

        {skill.description && (
          <FinalSection label="Description" last>
            <div style={{
              fontSize: 11.5, fontStyle: 'italic',
              color: 'rgba(240,240,240,0.6)', lineHeight: 1.55,
            }}>{skill.description}</div>
          </FinalSection>
        )}
      </div>
    </div>
  );
}

function FinalSkillTooltipPill({ skill }) {
  return <FinalSkillTooltipBase skill={skill} cooldownIndicator={CooldownPillAnim} />;
}
function FinalSkillTooltipPips({ skill }) {
  return <FinalSkillTooltipBase skill={skill} cooldownIndicator={CooldownPipsAnim} />;
}

/* ─── Animated cooldown indicators ───────────────────────────────── */

function CooldownPillAnim({ remaining, total, ready }) {
  const progress = ready ? 1 : Math.max(0, (total - remaining) / total);
  return (
    <div style={{
      position: 'relative',
      width: 60, height: 18,
      background: 'rgba(240,240,240,0.06)',
      border: '1px solid rgba(255,255,255,0.14)',
      borderRadius: 2,
      overflow: 'hidden',
    }}>
      <div style={{
        position: 'absolute', inset: 0,
        width: `${progress * 100}%`,
        background: ready
          ? 'linear-gradient(90deg, rgba(189,224,180,0.55), rgba(189,224,180,0.2))'
          : 'linear-gradient(90deg, rgba(161,194,247,0.5), rgba(161,194,247,0.18))',
        // no transition — driven by RAF every frame for smooth fill
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

function CooldownPipsAnim({ remaining, total, ready }) {
  const pips = 8;
  const exactFilled = ready ? pips : Math.max(0, ((total - remaining) / total) * pips);
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3 }}>
      <div style={{ display: 'flex', gap: 2 }}>
        {Array.from({ length: pips }).map((_, i) => {
          const fill = Math.max(0, Math.min(1, exactFilled - i));
          return (
            <div key={i} style={{
              position: 'relative',
              width: 6, height: 12,
              background: 'rgba(240,240,240,0.14)',
              overflow: 'hidden',
            }}>
              <div style={{
                position: 'absolute', left: 0, right: 0, bottom: 0,
                height: `${fill * 100}%`,
                background: ready ? '#bde0b4' : '#a1c2f7',
              }} />
            </div>
          );
        })}
      </div>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 9,
        color: ready ? '#bde0b4' : 'rgba(240,240,240,0.62)',
        letterSpacing: 1.2, textTransform: 'uppercase',
      }}>{ready ? 'Ready' : `${fmt(remaining)}s`}</span>
    </div>
  );
}

/* ─── shared chrome (matches C) ──────────────────────────────────── */

function ttFinalShell(accent) {
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

function FinalSection({ label, children, last }) {
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

function FinalDmgRow({ label, value, positive, negative }) {
  const color = positive ? '#bde0b4' : negative ? '#f0a094' : '#f0f0f0';
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', fontSize: 11.5 }}>
      <span style={{ color: 'rgba(240,240,240,0.78)' }}>{label}</span>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
        color, letterSpacing: 0.3,
      }}>{value}</span>
    </div>
  );
}

function FinalMetric({ label, value }) {
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

function EquippedBadge() {
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

Object.assign(window, {
  useAnimatedSkill,
  FinalItemTooltip, FinalSkillTooltipPill, FinalSkillTooltipPips,
});
