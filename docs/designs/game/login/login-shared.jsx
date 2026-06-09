/* Shared validation, hook, and small UI primitives for the login variants. */

// ─── validation ───────────────────────────────────────────────────────────
const USERNAME_RE = /^[a-zA-Z][a-zA-Z0-9_-]{2,19}$/;

function validateUsername(v) {
  if (!v) return { ok: false, msg: 'Required' };
  if (v.length < 3) return { ok: false, msg: 'At least 3 characters' };
  if (v.length > 20) return { ok: false, msg: 'Max 20 characters' };
  if (/^[0-9]/.test(v)) return { ok: false, msg: "Can't start with a number" };
  if (!USERNAME_RE.test(v)) return { ok: false, msg: 'Letters, numbers, _ or - only' };
  return { ok: true, msg: 'Looks good' };
}

function passwordStrength(v) {
  if (!v) return { score: 0, label: '—' };
  let s = 0;
  if (v.length >= 8) s++;
  if (v.length >= 12) s++;
  if (/[a-z]/.test(v) && /[A-Z]/.test(v)) s++;
  if (/[0-9]/.test(v)) s++;
  if (/[^A-Za-z0-9]/.test(v)) s++;
  const labels = ['Too short', 'Weak', 'Fair', 'Good', 'Strong', 'Strong'];
  return { score: Math.min(s, 4), label: labels[s] };
}

function validatePassword(v, mode) {
  if (!v) return { ok: false, msg: 'Required' };
  if (mode === 'login') return { ok: true, msg: '' };
  if (v.length < 8) return { ok: false, msg: 'At least 8 characters' };
  if (!/[a-zA-Z]/.test(v) || !/[0-9]/.test(v))
    return { ok: false, msg: 'Mix letters and numbers' };
  return { ok: true, msg: '' };
}

function validateConfirm(v, password) {
  if (!v) return { ok: false, msg: 'Required' };
  if (v !== password) return { ok: false, msg: "Passwords don't match" };
  return { ok: true, msg: 'Match' };
}

// ─── form hook ────────────────────────────────────────────────────────────
function useLoginForm(initialMode = 'login') {
  const [mode, setMode] = React.useState(initialMode);
  const [username, setUsername] = React.useState('');
  const [password, setPassword] = React.useState('');
  const [confirm, setConfirm] = React.useState('');
  const [touched, setTouched] = React.useState({});
  const [submitting, setSubmitting] = React.useState(false);
  const [submitted, setSubmitted] = React.useState(false);
  const [serverError, setServerError] = React.useState(null);
  const [success, setSuccess] = React.useState(false);
  const [capsOn, setCapsOn] = React.useState(false);

  const uV = validateUsername(username);
  const pV = validatePassword(password, mode);
  const cV = mode === 'signup' ? validateConfirm(confirm, password) : { ok: true, msg: '' };
  const strength = passwordStrength(password);

  const showError = (key) => (touched[key] || submitted);
  const fieldError = (key, v) => (showError(key) && !v.ok ? v.msg : '');

  const valid =
    uV.ok && pV.ok && (mode === 'login' || cV.ok);

  const markTouched = (k) => setTouched((t) => ({ ...t, [k]: true }));

  const reset = () => {
    setTouched({});
    setSubmitted(false);
    setServerError(null);
    setSuccess(false);
  };

  const switchMode = (m) => {
    setMode(m);
    setConfirm('');
    reset();
  };

  const submit = async () => {
    setSubmitted(true);
    setServerError(null);
    if (!valid) return;
    setSubmitting(true);
    // Simulate API: usernames "taken" / "guest" fail in interesting ways.
    await new Promise((r) => setTimeout(r, 850));
    setSubmitting(false);
    if (mode === 'signup' && username.toLowerCase() === 'admin') {
      setServerError('That name is already taken.');
      return;
    }
    if (mode === 'login' && password === 'wrong') {
      setServerError('Incorrect username or password.');
      return;
    }
    setSuccess(true);
    setTimeout(() => setSuccess(false), 2200);
  };

  return {
    mode, setMode: switchMode,
    username, setUsername,
    password, setPassword,
    confirm, setConfirm,
    uV, pV, cV, strength,
    fieldError, markTouched, showError,
    valid, submitting, submitted, serverError, success,
    submit, reset,
    capsOn, setCapsOn,
  };
}

// ─── strength meter ──────────────────────────────────────────────────────
function StrengthMeter({ score, label, accent = '#a1c2f7' }) {
  const colors = ['#c9c9c9', '#d97a5a', '#d9a23a', '#7eb46a', '#3e8a4a'];
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{ display: 'flex', gap: 3, flex: 1 }}>
        {[0, 1, 2, 3].map((i) => (
          <div key={i} style={{
            flex: 1, height: 4, borderRadius: 2,
            background: i < score ? colors[score] : 'rgba(0,0,0,0.12)',
            transition: 'background 160ms ease',
          }} />
        ))}
      </div>
      <span style={{
        fontFamily: 'Geist Mono, ui-monospace, monospace',
        fontSize: 11, color: score >= 3 ? '#3e8a4a' : score >= 2 ? '#806a1a' : '#666',
        minWidth: 56, textAlign: 'right',
      }}>{label}</span>
    </div>
  );
}

// ─── icons ────────────────────────────────────────────────────────────────
function IconCheck({ size = 14, color = '#3e8a4a' }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="none">
      <path d="M3 8.5l3 3 7-7" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
function IconX({ size = 14, color = '#c94a3a' }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="none">
      <path d="M4 4l8 8M12 4l-8 8" stroke={color} strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
function IconEye({ open = true, size = 16, color = '#555' }) {
  return open ? (
    <svg width={size} height={size} viewBox="0 0 20 20" fill="none">
      <path d="M2 10s3-5 8-5 8 5 8 5-3 5-8 5-8-5-8-5z" stroke={color} strokeWidth="1.4" />
      <circle cx="10" cy="10" r="2.5" stroke={color} strokeWidth="1.4" />
    </svg>
  ) : (
    <svg width={size} height={size} viewBox="0 0 20 20" fill="none">
      <path d="M2 10s3-5 8-5c2 0 3.7.8 5 1.8M18 10s-3 5-8 5c-2 0-3.7-.8-5-1.8" stroke={color} strokeWidth="1.4" />
      <path d="M3 3l14 14" stroke={color} strokeWidth="1.4" strokeLinecap="round" />
    </svg>
  );
}

// ─── caps lock detection ──────────────────────────────────────────────────
function useCapsLock() {
  const [on, setOn] = React.useState(false);
  const handler = React.useCallback((e) => {
    if (typeof e.getModifierState === 'function') {
      setOn(e.getModifierState('CapsLock'));
    }
  }, []);
  return { capsOn: on, capsHandlers: { onKeyDown: handler, onKeyUp: handler } };
}

Object.assign(window, {
  useLoginForm, useCapsLock,
  validateUsername, validatePassword, validateConfirm, passwordStrength,
  StrengthMeter, IconCheck, IconX, IconEye,
});
