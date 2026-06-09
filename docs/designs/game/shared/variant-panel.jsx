/* Variant 2: Game Panel
   Split panel — left side is a placeholder hero art panel with flavor copy,
   right side is the form. On signup, includes a class pick. Modeled to feel
   like a game launcher / character-select screen. */

function VariantPanel() {
  const f = useLoginForm('login');
  const { capsOn, capsHandlers } = useCapsLock();
  const [showPw, setShowPw] = React.useState(false);
  const [klass, setKlass] = React.useState('warrior');

  const classes = [
    { id: 'warrior', label: 'Warrior', sub: 'STR · HP+' },
    { id: 'rogue',   label: 'Rogue',   sub: 'AGI · CRIT+' },
    { id: 'mage',    label: 'Mage',    sub: 'INT · MP+' },
  ];

  const fieldStyle = (state) => ({
    width: '100%', boxSizing: 'border-box',
    height: 40, padding: '0 38px 0 12px',
    background: 'rgba(255,255,255,0.04)',
    border: `1px solid ${state === 'err' ? '#c94a3a' : state === 'ok' ? '#7eb46a' : 'rgba(255,255,255,0.18)'}`,
    borderRadius: 4, outline: 'none',
    color: '#f0f0f0', fontSize: 14, fontFamily: 'inherit',
    transition: 'border-color 140ms, background 140ms',
  });

  const usernameErr = f.fieldError('u', f.uV);
  const passwordErr = f.fieldError('p', f.pV);
  const confirmErr = f.fieldError('c', f.cV);

  const Hint = ({ ok, error, idle }) => (
    <div style={{
      fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
      color: error ? '#e08778' : ok ? '#a8d4a0' : 'rgba(240,240,240,0.45)',
      marginTop: 5, display: 'flex', alignItems: 'center', gap: 5,
      minHeight: 14,
    }}>
      {error ? <><IconX color="#e08778" /> {error}</>
        : ok ? <><IconCheck color="#a8d4a0" /> {ok}</>
        : idle}
    </div>
  );

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
    }}>
      <div style={{
        display: 'grid', gridTemplateColumns: '320px 1fr',
        width: 800, height: 540,
        background: '#1a1b22', borderRadius: 8,
        border: '1px solid rgba(255,255,255,0.12)',
        boxShadow: '0 12px 40px rgba(0,0,0,0.6), 0 0 0 1px rgba(0,0,0,0.5)',
        overflow: 'hidden', color: '#f0f0f0',
      }}>
        {/* Left: hero art panel */}
        <div style={{
          background: `
            repeating-linear-gradient(135deg, rgba(255,255,255,0.025) 0 6px, transparent 6px 14px),
            linear-gradient(180deg, #232531 0%, #14151b 100%)`,
          padding: '24px 22px', display: 'flex', flexDirection: 'column',
          justifyContent: 'space-between',
          borderRight: '1px solid rgba(255,255,255,0.08)',
        }}>
          <div>
            <div style={{
              fontFamily: 'Geist Mono, monospace', fontSize: 10,
              letterSpacing: 2, textTransform: 'uppercase',
              color: 'rgba(161,194,247,0.7)', marginBottom: 8,
            }}>◇ Tactic Foundry</div>
            <div style={{ fontSize: 22, fontWeight: 600, letterSpacing: -0.3, lineHeight: 1.15 }}>
              {f.mode === 'login' ? 'Return to the\nrealm.' : 'Forge a new\nhero.'}
            </div>
          </div>

          {/* placeholder hero portrait */}
          <div style={{
            position: 'relative',
            aspectRatio: '1 / 1',
            margin: '12px 0',
            border: '1px solid rgba(255,255,255,0.14)',
            borderRadius: 4,
            background: `
              repeating-linear-gradient(45deg, rgba(255,255,255,0.04) 0 4px, transparent 4px 9px),
              radial-gradient(circle at 50% 40%, rgba(161,194,247,0.18) 0%, transparent 60%)`,
            display: 'flex', alignItems: 'flex-end', justifyContent: 'center',
            padding: 10, overflow: 'hidden',
          }}>
            <div style={{
              fontFamily: 'Geist Mono, monospace', fontSize: 10,
              color: 'rgba(240,240,240,0.55)', letterSpacing: 1.5,
            }}>HERO · PORTRAIT</div>
            <div style={{
              position: 'absolute', top: 8, left: 8,
              fontFamily: 'Geist Mono, monospace', fontSize: 9,
              color: 'rgba(240,240,240,0.4)',
            }}>256×256</div>
          </div>

          <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10.5, lineHeight: 1.6, color: 'rgba(240,240,240,0.55)' }}>
            {f.mode === 'login'
              ? '> Your party rests in town.\n> 14 quests remain.'
              : '> Choose wisely.\n> Each class shapes your run.'}
          </div>
        </div>

        {/* Right: form */}
        <div style={{ padding: '28px 36px', display: 'flex', flexDirection: 'column' }}>
          {/* Tabs */}
          <div style={{ display: 'flex', gap: 24, borderBottom: '1px solid rgba(255,255,255,0.1)', marginBottom: 22 }}>
            {['login', 'signup'].map((m) => (
              <button key={m} onClick={() => f.setMode(m)} style={{
                background: 'transparent', border: 'none', color: f.mode === m ? '#f0f0f0' : 'rgba(240,240,240,0.45)',
                padding: '0 0 10px', cursor: 'pointer', fontFamily: 'inherit', fontSize: 13,
                fontWeight: 500, letterSpacing: 0.3, position: 'relative',
                borderBottom: `2px solid ${f.mode === m ? '#a1c2f7' : 'transparent'}`,
                marginBottom: -1,
              }}>{m === 'login' ? 'Sign In' : 'Create Account'}</button>
            ))}
          </div>

          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 14 }}>
            {/* Username */}
            <div>
              <Label>Username</Label>
              <div style={{ position: 'relative' }}>
                <input
                  type="text" value={f.username}
                  onChange={(e) => f.setUsername(e.target.value)}
                  onBlur={() => f.markTouched('u')}
                  placeholder="hero_name"
                  style={fieldStyle(usernameErr ? 'err' : f.uV.ok && f.username ? 'ok' : 'default')} />
                <FieldIcon error={!!usernameErr} ok={f.uV.ok && !!f.username} />
              </div>
              <Hint ok={f.uV.ok && f.username ? 'Available' : null} error={usernameErr}
                idle={<span>3–20 chars · letters, numbers, _ or -</span>} />
            </div>

            {/* Password */}
            <div>
              <Label>Password</Label>
              <div style={{ position: 'relative' }}>
                <input
                  type={showPw ? 'text' : 'password'} value={f.password}
                  onChange={(e) => f.setPassword(e.target.value)}
                  onBlur={() => f.markTouched('p')}
                  {...capsHandlers}
                  placeholder={f.mode === 'signup' ? '8+ characters' : 'Password'}
                  style={fieldStyle(passwordErr ? 'err' : f.password && f.pV.ok ? 'ok' : 'default')} />
                <button onClick={() => setShowPw((s) => !s)} type="button" style={{
                  position: 'absolute', top: '50%', right: 10, transform: 'translateY(-50%)',
                  border: 'none', background: 'transparent', cursor: 'pointer',
                  padding: 4, display: 'flex',
                }}><IconEye open={!showPw} color="rgba(240,240,240,0.5)" /></button>
              </div>
              {f.mode === 'signup' ? (
                <div style={{ marginTop: 6 }}>
                  <StrengthMeterDark score={f.strength.score} label={f.strength.label} />
                </div>
              ) : (
                <Hint error={passwordErr} idle={capsOn ? <span style={{ color: '#e8c878' }}>⚠ Caps Lock is on</span> : null} />
              )}
              {f.mode === 'signup' && capsOn && (
                <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 10.5, color: '#e8c878', marginTop: 4 }}>
                  ⚠ Caps Lock is on
                </div>
              )}
            </div>

            {/* Confirm */}
            {f.mode === 'signup' && (
              <div>
                <Label>Confirm password</Label>
                <div style={{ position: 'relative' }}>
                  <input
                    type={showPw ? 'text' : 'password'} value={f.confirm}
                    onChange={(e) => f.setConfirm(e.target.value)}
                    onBlur={() => f.markTouched('c')}
                    placeholder="Re-enter"
                    style={fieldStyle(confirmErr ? 'err' : f.confirm && f.cV.ok ? 'ok' : 'default')} />
                  <FieldIcon error={!!confirmErr} ok={f.cV.ok && !!f.confirm} />
                </div>
                <Hint ok={f.cV.ok && f.confirm ? 'Match' : null} error={confirmErr} />
              </div>
            )}

            {/* Class pick */}
            {f.mode === 'signup' && (
              <div>
                <Label>Starting class</Label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
                  {classes.map((c) => (
                    <button key={c.id} onClick={() => setKlass(c.id)} type="button" style={{
                      background: klass === c.id ? 'rgba(161,194,247,0.15)' : 'rgba(255,255,255,0.03)',
                      border: `1px solid ${klass === c.id ? '#a1c2f7' : 'rgba(255,255,255,0.14)'}`,
                      borderRadius: 4, padding: '8px 6px', color: '#f0f0f0',
                      cursor: 'pointer', fontFamily: 'inherit',
                      transition: 'all 140ms',
                    }}>
                      <div style={{ fontSize: 12, fontWeight: 500 }}>{c.label}</div>
                      <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 9.5, color: 'rgba(240,240,240,0.55)', marginTop: 2 }}>{c.sub}</div>
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>

          {f.serverError && (
            <div style={{
              background: 'rgba(201,74,58,0.12)', border: '1px solid rgba(201,74,58,0.4)',
              color: '#e08778', borderRadius: 4, padding: '7px 10px',
              fontSize: 11.5, marginTop: 14, fontFamily: 'Geist Mono, monospace',
            }}>{f.serverError}</div>
          )}
          {f.success && (
            <div style={{
              background: 'rgba(126,180,106,0.12)', border: '1px solid rgba(126,180,106,0.4)',
              color: '#a8d4a0', borderRadius: 4, padding: '7px 10px',
              fontSize: 11.5, marginTop: 14, fontFamily: 'Geist Mono, monospace',
              display: 'flex', alignItems: 'center', gap: 6,
            }}><IconCheck color="#a8d4a0" /> {f.mode === 'login' ? 'Signed in — loading world…' : `Hero forged · class: ${klass}`}</div>
          )}

          <button
            onClick={f.submit}
            disabled={!f.valid || f.submitting}
            style={{
              marginTop: 16, padding: '11px 0',
              background: f.valid ? '#a1c2f7' : 'rgba(255,255,255,0.08)',
              color: f.valid ? '#111' : 'rgba(240,240,240,0.4)',
              border: 'none', borderRadius: 4, cursor: f.valid && !f.submitting ? 'pointer' : 'not-allowed',
              fontSize: 13.5, fontWeight: 600, fontFamily: 'inherit',
              letterSpacing: 0.4, textTransform: 'uppercase',
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
              transition: 'background 160ms',
            }}>
            {f.submitting && (
              <div style={{
                width: 12, height: 12, border: '2px solid rgba(0,0,0,0.25)',
                borderTopColor: '#111', borderRadius: '50%',
                animation: 'spin 0.8s linear infinite',
              }} />
            )}
            {f.submitting ? '…' : (f.mode === 'login' ? 'Enter Realm' : 'Forge Hero')}
          </button>
        </div>
      </div>
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}

function Label({ children }) {
  return <div style={{
    fontFamily: 'Geist Mono, monospace', fontSize: 10, letterSpacing: 1.2,
    textTransform: 'uppercase', color: 'rgba(240,240,240,0.55)', marginBottom: 6,
  }}>{children}</div>;
}

function FieldIcon({ error, ok }) {
  if (!error && !ok) return null;
  return (
    <div style={{
      position: 'absolute', top: '50%', right: 12, transform: 'translateY(-50%)',
      display: 'flex',
    }}>{error ? <IconX color="#e08778" /> : <IconCheck color="#a8d4a0" />}</div>
  );
}

function StrengthMeterDark({ score, label }) {
  const colors = ['#555', '#d97a5a', '#d9a23a', '#7eb46a', '#a8d4a0'];
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{ display: 'flex', gap: 3, flex: 1 }}>
        {[0, 1, 2, 3].map((i) => (
          <div key={i} style={{
            flex: 1, height: 3, borderRadius: 2,
            background: i < score ? colors[score] : 'rgba(255,255,255,0.08)',
            transition: 'background 160ms',
          }} />
        ))}
      </div>
      <span style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 10,
        color: score >= 3 ? '#a8d4a0' : score >= 2 ? '#d9a23a' : 'rgba(240,240,240,0.45)',
        minWidth: 56, textAlign: 'right', letterSpacing: 0.5,
      }}>{label}</span>
    </div>
  );
}

window.VariantPanel = VariantPanel;
