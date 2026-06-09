/* Variant 3: Minimal Stack
   No card. Centered column directly on the dark gradient. Underlined fields,
   big quiet typography, validation as a single live status line below.
   Feels editorial / contemplative — for a slower, story-driven game. */

function VariantStack() {
  const f = useLoginForm('login');
  const { capsOn, capsHandlers } = useCapsLock();
  const [showPw, setShowPw] = React.useState(false);

  const usernameErr = f.fieldError('u', f.uV);
  const passwordErr = f.fieldError('p', f.pV);
  const confirmErr = f.fieldError('c', f.cV);

  // collect the single highest-priority hint to show inline
  const statusLine = (() => {
    if (f.serverError) return { type: 'err', text: f.serverError };
    if (f.success) return { type: 'ok', text: f.mode === 'login' ? 'Signed in — loading world…' : 'Account created — entering…' };
    if (usernameErr) return { type: 'err', text: 'Username · ' + usernameErr.toLowerCase() };
    if (passwordErr) return { type: 'err', text: 'Password · ' + passwordErr.toLowerCase() };
    if (confirmErr) return { type: 'err', text: 'Confirm · ' + confirmErr.toLowerCase() };
    if (capsOn) return { type: 'warn', text: 'Caps Lock is on' };
    if (f.mode === 'signup' && f.password) return { type: 'info', text: `Strength · ${f.strength.label.toLowerCase()}` };
    if (f.valid && f.username) return { type: 'ok', text: 'Ready' };
    return { type: 'idle', text: '\u00A0' };
  })();

  const underlineField = (state) => ({
    width: '100%', boxSizing: 'border-box',
    height: 44, padding: '0 36px 0 0',
    background: 'transparent',
    border: 'none',
    borderBottom: `1px solid ${
      state === 'err' ? '#e08778'
      : state === 'ok' ? 'rgba(168,212,160,0.85)'
      : 'rgba(240,240,240,0.45)'}`,
    outline: 'none',
    color: '#f0f0f0', fontSize: 18, fontFamily: 'inherit',
    letterSpacing: 0.2,
    transition: 'border-color 160ms',
  });

  const statusColor = {
    err: '#f0a094', ok: '#bde0b4', warn: '#f0d28a',
    info: 'rgba(240,240,240,0.78)', idle: 'rgba(240,240,240,0.55)',
  }[statusLine.type];

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#f0f0f0', padding: '40px',
      boxSizing: 'border-box',
    }}>
      <div style={{ width: 360, maxWidth: '100%' }}>
        {/* Diamond mark */}
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 36 }}>
          <div style={{
            width: 18, height: 18, transform: 'rotate(45deg)',
            border: '1px solid #a1c2f7',
            boxShadow: '0 0 8px rgba(161,194,247,0.4)',
            position: 'relative',
          }}>
            <div style={{
              position: 'absolute', inset: 4,
              background: '#a1c2f7',
            }} />
          </div>
        </div>

        <div style={{ textAlign: 'center', marginBottom: 42 }}>
          <h1 style={{
            margin: 0, fontSize: 32, fontWeight: 400, letterSpacing: -0.5,
            lineHeight: 1.1, color: '#f0f0f0',
          }}>{f.mode === 'login' ? 'Welcome back.' : 'Begin a new run.'}</h1>
          <p style={{
            margin: '10px 0 0', fontSize: 12.5, color: 'rgba(240,240,240,0.72)',
            fontFamily: 'Geist Mono, monospace', letterSpacing: 0.6,
          }}>
            {f.mode === 'login' ? 'Sign in to continue.' : 'Pick a name. Forge ahead.'}
          </p>
        </div>

        {/* Username */}
        <div style={{ marginBottom: 22, position: 'relative' }}>
          <input
            type="text" value={f.username}
            onChange={(e) => f.setUsername(e.target.value)}
            onBlur={() => f.markTouched('u')}
            placeholder="Username"
            style={underlineField(usernameErr ? 'err' : f.uV.ok && f.username ? 'ok' : 'default')} />
          {f.username && (
            <div style={{ position: 'absolute', right: 0, top: 14 }}>
              {usernameErr ? <IconX color="#f0a094" size={16} /> : f.uV.ok && <IconCheck color="#bde0b4" size={16} />}
            </div>
          )}
        </div>

        {/* Password */}
        <div style={{ marginBottom: f.mode === 'signup' ? 22 : 14, position: 'relative' }}>
          <input
            type={showPw ? 'text' : 'password'} value={f.password}
            onChange={(e) => f.setPassword(e.target.value)}
            onBlur={() => f.markTouched('p')}
            {...capsHandlers}
            placeholder="Password"
            style={underlineField(passwordErr ? 'err' : f.password && f.pV.ok ? 'ok' : 'default')} />
          <button onClick={() => setShowPw((s) => !s)} type="button" style={{
            position: 'absolute', right: 0, top: 10,
            border: 'none', background: 'transparent', cursor: 'pointer',
            padding: 6, display: 'flex',
          }}><IconEye open={!showPw} color="rgba(240,240,240,0.75)" /></button>
          {f.mode === 'signup' && f.password && (
            <div style={{ marginTop: 10 }}>
              <SegmentStrength score={f.strength.score} />
            </div>
          )}
        </div>

        {/* Confirm */}
        {f.mode === 'signup' && (
          <div style={{ marginBottom: 22, position: 'relative' }}>
            <input
              type={showPw ? 'text' : 'password'} value={f.confirm}
              onChange={(e) => f.setConfirm(e.target.value)}
              onBlur={() => f.markTouched('c')}
              placeholder="Confirm password"
              style={underlineField(confirmErr ? 'err' : f.confirm && f.cV.ok ? 'ok' : 'default')} />
            {f.confirm && (
              <div style={{ position: 'absolute', right: 0, top: 14 }}>
                {confirmErr ? <IconX color="#f0a094" size={16} /> : f.cV.ok && <IconCheck color="#bde0b4" size={16} />}
              </div>
            )}
          </div>
        )}

        {/* Single status line */}
        <div style={{
          fontFamily: 'Geist Mono, monospace', fontSize: 11.5,
          color: statusColor, letterSpacing: 0.6, textAlign: 'center',
          minHeight: 18, marginBottom: 22,
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
          transition: 'color 160ms',
        }}>
          {statusLine.type === 'ok' && <IconCheck color={statusColor} />}
          {statusLine.type === 'err' && <IconX color={statusColor} />}
          {statusLine.text}
        </div>

        <button
          onClick={f.submit}
          disabled={!f.valid || f.submitting}
          style={{
            width: '100%', padding: '13px 0',
            background: f.valid ? '#a1c2f7' : 'transparent',
            color: f.valid ? '#111' : 'rgba(240,240,240,0.55)',
            border: `1px solid ${f.valid ? '#a1c2f7' : 'rgba(240,240,240,0.35)'}`,
            borderRadius: 2,
            cursor: f.valid && !f.submitting ? 'pointer' : 'not-allowed',
            fontSize: 13, fontWeight: 500, fontFamily: 'inherit',
            letterSpacing: 2, textTransform: 'uppercase',
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
            transition: 'all 180ms',
          }}>
          {f.submitting && (
            <div style={{
              width: 11, height: 11, border: '2px solid rgba(0,0,0,0.25)',
              borderTopColor: '#111', borderRadius: '50%',
              animation: 'spin 0.8s linear infinite',
            }} />
          )}
          {f.submitting ? '...' : (f.mode === 'login' ? 'Sign In' : 'Create')}
        </button>

        {/* mode toggle */}
        <div style={{
          marginTop: 28, textAlign: 'center',
          fontFamily: 'Geist Mono, monospace', fontSize: 11,
          color: 'rgba(240,240,240,0.65)', letterSpacing: 0.4,
        }}>
          {f.mode === 'login' ? 'No account yet?' : 'Already a hero?'}
          {' '}
          <a onClick={() => f.setMode(f.mode === 'login' ? 'signup' : 'login')} style={{
            color: '#c0d8ff', cursor: 'pointer', textDecoration: 'none',
            borderBottom: '1px solid rgba(192,216,255,0.55)',
            paddingBottom: 1,
          }}>
            {f.mode === 'login' ? 'Create one' : 'Sign in'}
          </a>
        </div>
      </div>
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}

function SegmentStrength({ score }) {
  return (
    <div style={{ display: 'flex', gap: 4, justifyContent: 'space-between' }}>
      {[0, 1, 2, 3].map((i) => (
        <div key={i} style={{
          flex: 1, height: 2,
          background: i < score
            ? (score >= 3 ? '#bde0b4' : score >= 2 ? '#e8b850' : '#e0907a')
            : 'rgba(240,240,240,0.22)',
          transition: 'background 160ms',
        }} />
      ))}
    </div>
  );
}

window.VariantStack = VariantStack;
