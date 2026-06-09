/* Variant 1: Refined Card
   Closest to the current screen — keeps the off-white card on the dark
   gradient, but adds tabs, inline validation, strength meter, show/hide,
   caps-lock hint, and a disabled-until-valid submit. */

function VariantCard() {
  const f = useLoginForm('login');
  const { capsOn, capsHandlers } = useCapsLock();
  const [showPw, setShowPw] = React.useState(false);

  const fieldShell = (state) => ({
    display: 'flex', alignItems: 'center', gap: 8,
    background: 'white', border: `1px solid ${state === 'err' ? '#c94a3a' : state === 'ok' ? '#9bb7e8' : 'rgba(0,0,0,0.6)'}`,
    borderRadius: 5, padding: '0 10px', height: 38,
    boxShadow: state === 'focus' ? '0 0 0 3px rgba(161,194,247,0.45)' : 'none',
    transition: 'box-shadow 120ms, border-color 120ms',
  });

  const labelRow = (label, ok, error) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 6 }}>
      <span style={{ fontSize: 12, fontWeight: 500, letterSpacing: 0.2, color: '#222' }}>{label}</span>
      <span style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: error ? '#c94a3a' : ok ? '#3e8a4a' : '#888', display: 'flex', alignItems: 'center', gap: 4 }}>
        {error ? <><IconX /> {error}</> : ok ? <><IconCheck /> ok</> : null}
      </span>
    </div>
  );

  const usernameErr = f.fieldError('u', f.uV);
  const passwordErr = f.fieldError('p', f.pV);
  const confirmErr = f.fieldError('c', f.cV);

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#111',
    }}>
      <div style={{
        width: 340, background: '#f0f0f0',
        border: '1px solid #000', borderRadius: 5,
        boxShadow: '0 0 0.5rem #000',
        padding: '26px 28px 24px',
      }}>
        <h1 style={{
          margin: 0, fontSize: 22, fontWeight: 600, letterSpacing: -0.2,
          color: '#1a1a1a',
        }}>{f.mode === 'login' ? 'Welcome back' : 'Create account'}</h1>
        <p style={{
          margin: '4px 0 18px', fontSize: 12, color: '#666',
          fontFamily: 'Geist Mono, monospace',
        }}>
          {f.mode === 'login' ? 'Sign in to continue your run.' : 'Pick a hero name to begin.'}
        </p>

        {/* Tabs */}
        <div style={{
          display: 'grid', gridTemplateColumns: '1fr 1fr',
          background: 'rgba(0,0,0,0.06)', borderRadius: 6, padding: 3,
          marginBottom: 18, position: 'relative',
        }}>
          <div style={{
            position: 'absolute', top: 3, bottom: 3,
            width: 'calc(50% - 3px)',
            left: f.mode === 'login' ? 3 : 'calc(50% + 0px)',
            background: 'white',
            borderRadius: 4,
            boxShadow: '0 1px 2px rgba(0,0,0,0.15)',
            transition: 'left 220ms cubic-bezier(.4,.0,.2,1)',
          }} />
          {['login', 'signup'].map((m) => (
            <button key={m} onClick={() => f.setMode(m)} style={{
              position: 'relative', zIndex: 1,
              background: 'transparent', border: 'none', cursor: 'pointer',
              padding: '7px 0', fontSize: 13, fontWeight: 500,
              color: f.mode === m ? '#111' : '#777',
              fontFamily: 'inherit',
            }}>{m === 'login' ? 'Sign in' : 'Create account'}</button>
          ))}
        </div>

        {/* Username */}
        <div style={{ marginBottom: 14 }}>
          {labelRow('Username', f.uV.ok && f.username, usernameErr)}
          <div style={fieldShell(usernameErr ? 'err' : f.uV.ok ? 'ok' : 'default')}>
            <input
              type="text"
              value={f.username}
              onChange={(e) => f.setUsername(e.target.value)}
              onBlur={() => f.markTouched('u')}
              placeholder="hero_name"
              style={{ flex: 1, border: 'none', outline: 'none', background: 'transparent', fontSize: 14, fontFamily: 'inherit', color: '#111' }}
            />
            {f.username && (usernameErr ? <IconX /> : f.uV.ok && <IconCheck />)}
          </div>
        </div>

        {/* Password */}
        <div style={{ marginBottom: f.mode === 'signup' ? 14 : 18 }}>
          {labelRow('Password', null, passwordErr)}
          <div style={fieldShell(passwordErr ? 'err' : f.password && f.pV.ok ? 'ok' : 'default')}>
            <input
              type={showPw ? 'text' : 'password'}
              value={f.password}
              onChange={(e) => f.setPassword(e.target.value)}
              onBlur={() => f.markTouched('p')}
              {...capsHandlers}
              placeholder={f.mode === 'signup' ? '8+ chars, letters + numbers' : 'Enter your password'}
              style={{ flex: 1, border: 'none', outline: 'none', background: 'transparent', fontSize: 14, fontFamily: 'inherit', color: '#111' }}
            />
            <button onClick={() => setShowPw((s) => !s)} type="button"
              style={{ border: 'none', background: 'transparent', cursor: 'pointer', padding: 4, display: 'flex' }}>
              <IconEye open={!showPw} />
            </button>
          </div>
          {capsOn && (
            <div style={{ fontFamily: 'Geist Mono, monospace', fontSize: 11, color: '#806a1a', marginTop: 4 }}>
              ⚠ Caps Lock is on
            </div>
          )}
          {f.mode === 'signup' && (
            <div style={{ marginTop: 8 }}>
              <StrengthMeter score={f.strength.score} label={f.strength.label} />
            </div>
          )}
        </div>

        {/* Confirm (signup only) */}
        {f.mode === 'signup' && (
          <div style={{ marginBottom: 18 }}>
            {labelRow('Confirm password', f.cV.ok && f.confirm, confirmErr)}
            <div style={fieldShell(confirmErr ? 'err' : f.confirm && f.cV.ok ? 'ok' : 'default')}>
              <input
                type={showPw ? 'text' : 'password'}
                value={f.confirm}
                onChange={(e) => f.setConfirm(e.target.value)}
                onBlur={() => f.markTouched('c')}
                placeholder="Re-enter password"
                style={{ flex: 1, border: 'none', outline: 'none', background: 'transparent', fontSize: 14, fontFamily: 'inherit', color: '#111' }}
              />
              {f.confirm && (confirmErr ? <IconX /> : f.cV.ok && <IconCheck />)}
            </div>
          </div>
        )}

        {/* Server error */}
        {f.serverError && (
          <div style={{
            background: 'rgba(201,74,58,0.1)', border: '1px solid rgba(201,74,58,0.4)',
            color: '#9c2a1d', borderRadius: 5, padding: '8px 10px',
            fontSize: 12, marginBottom: 12,
            fontFamily: 'Geist Mono, monospace',
          }}>{f.serverError}</div>
        )}
        {f.success && (
          <div style={{
            background: 'rgba(62,138,74,0.12)', border: '1px solid rgba(62,138,74,0.4)',
            color: '#1f5f2a', borderRadius: 5, padding: '8px 10px',
            fontSize: 12, marginBottom: 12, display: 'flex', alignItems: 'center', gap: 6,
            fontFamily: 'Geist Mono, monospace',
          }}><IconCheck /> {f.mode === 'login' ? 'Signed in — loading world…' : 'Account created — entering…'}</div>
        )}

        {/* Submit */}
        <button
          onClick={f.submit}
          disabled={!f.valid || f.submitting}
          style={{
            width: '100%', padding: '10px 0',
            background: f.valid ? '#a1c2f7' : '#cfcfcf',
            border: '1px solid #000',
            borderRadius: 5, cursor: f.valid && !f.submitting ? 'pointer' : 'not-allowed',
            fontSize: 14, fontWeight: 600, color: '#111',
            fontFamily: 'inherit',
            transition: 'background 160ms, filter 120ms',
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
          }}>
          {f.submitting && (
            <div style={{
              width: 13, height: 13, border: '2px solid rgba(0,0,0,0.25)',
              borderTopColor: '#111', borderRadius: '50%',
              animation: 'spin 0.8s linear infinite',
            }} />
          )}
          {f.submitting
            ? (f.mode === 'login' ? 'Signing in…' : 'Creating account…')
            : (f.mode === 'login' ? 'Sign in' : 'Create account')}
        </button>

        <p style={{
          margin: '14px 0 0', fontSize: 11, color: '#888',
          fontFamily: 'Geist Mono, monospace', textAlign: 'center',
        }}>
          {f.mode === 'login'
            ? <>New here? <a onClick={() => f.setMode('signup')} style={linkStyle}>Create an account</a></>
            : <>Already have one? <a onClick={() => f.setMode('login')} style={linkStyle}>Sign in</a></>}
        </p>
      </div>

      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}

const linkStyle = { color: '#3a6bbf', cursor: 'pointer', textDecoration: 'underline' };

window.VariantCard = VariantCard;
