/* Modernized loading screen — sliding manifest.
   Sequential, count-based progress (no fake percentages). Items live in a
   windowed list that cycles upward as each finishes. Handles four scenarios:
   fresh / cached / partial / failOnce. Stays locked until all items done. */

function LoadingRefined({ scenario = 'fresh' }) {
  const L = useNetworkLoader(scenario);
  const rowHeight = 42;
  const visibleRows = 5;
  const windowHeight = rowHeight * visibleRows;

  // anchor active row at slot 2 (middle of 5)
  const cursorY = (2 - L.activeIndex) * rowHeight;
  const current = L.items[L.activeIndex];

  const title = {
    checking: 'Checking for updates.',
    loading:  'Preparing the realm.',
    error:    'Connection failed.',
    done:     'Ready.',
  }[L.phase];

  const subtitle = (() => {
    if (L.phase === 'checking') return 'Verifying cached reference data…';
    if (L.phase === 'loading' && current) return <>Loading <span style={{ color: '#c0d8ff' }}>{current.label.toLowerCase()}</span><Dots /></>;
    if (L.phase === 'error' && current) return <>Could not load <span style={{ color: '#f0a094' }}>{current.label.toLowerCase()}</span>.</>;
    if (L.phase === 'done') return 'All systems nominal.';
    return '\u00A0';
  })();

  const progress = L.completed / L.total;

  return (
    <div style={{
      width: '100%', height: '100%',
      background: 'linear-gradient(0deg, #000 0%, #3c3c3c 100%)',
      fontFamily: 'Geist, Arial, Helvetica, sans-serif',
      color: '#f0f0f0',
      display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      padding: 40, boxSizing: 'border-box',
    }}>
      <div style={{ width: 380, maxWidth: '100%' }}>
        {/* Diamond mark */}
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 26 }}>
          <RefinedDiamond animate={L.phase !== 'done'} state={L.phase} />
        </div>

        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: 22 }}>
          <h1 style={{
            margin: 0, fontSize: 26, fontWeight: 400, letterSpacing: -0.3,
            color: L.phase === 'error' ? '#f0a094' : '#f0f0f0',
            transition: 'color 200ms',
          }}>{title}</h1>
          <p style={{
            margin: '8px 0 0', fontSize: 12.5,
            color: 'rgba(240,240,240,0.78)',
            fontFamily: 'Geist Mono, monospace', letterSpacing: 0.5,
            minHeight: 18,
          }}>{subtitle}</p>
        </div>

        {/* Progress bar — count-based */}
        <div style={{
          height: 2, background: 'rgba(240,240,240,0.14)',
          borderRadius: 1, overflow: 'hidden', position: 'relative',
        }}>
          <div style={{
            position: 'absolute', inset: 0,
            width: `${progress * 100}%`,
            background: L.phase === 'error'
              ? 'rgba(240,160,148,0.85)'
              : L.phase === 'done'
              ? 'linear-gradient(90deg, #a1c2f7 0%, #bde0b4 100%)'
              : '#a1c2f7',
            boxShadow: '0 0 6px rgba(161,194,247,0.45)',
            transition: 'width 480ms cubic-bezier(.4,0,.2,1), background 200ms',
          }} />
        </div>

        <div style={{
          display: 'flex', justifyContent: 'space-between',
          marginTop: 5,
          fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
          color: 'rgba(240,240,240,0.65)', letterSpacing: 0.5,
        }}>
          <span>{L.completed} of {L.total}</span>
          <span>{L.phase === 'done' ? 'complete' : L.phase === 'error' ? 'paused' : L.phase === 'checking' ? 'checking' : 'loading'}</span>
        </div>

        {/* Sliding manifest window */}
        <div style={{
          marginTop: 18,
          height: windowHeight,
          position: 'relative',
          overflow: 'hidden',
          maskImage: 'linear-gradient(to bottom, transparent 0%, black 22%, black 78%, transparent 100%)',
          WebkitMaskImage: 'linear-gradient(to bottom, transparent 0%, black 22%, black 78%, transparent 100%)',
        }}>
          <div style={{
            position: 'absolute', left: 0, right: 0,
            transform: `translateY(${cursorY}px)`,
            transition: 'transform 420ms cubic-bezier(.4,0,.2,1)',
          }}>
            {L.items.map((it, i) => (
              <ManifestRow key={it.key} item={it} offset={i - L.activeIndex} rowHeight={rowHeight} />
            ))}
          </div>
          {/* subtle active-row indicator */}
          {L.phase !== 'done' && L.phase !== 'checking' && (
            <div style={{
              position: 'absolute', left: 0, right: 0,
              top: rowHeight * 2, height: rowHeight,
              border: '1px solid rgba(161,194,247,0.18)',
              background: 'rgba(161,194,247,0.05)',
              borderRadius: 3,
              pointerEvents: 'none',
            }} />
          )}
        </div>

        {/* Retry on error */}
        {L.phase === 'error' && current && (
          <div style={{
            marginTop: 16, padding: '10px 12px',
            background: 'rgba(240,160,148,0.08)',
            border: '1px solid rgba(240,160,148,0.35)',
            borderRadius: 3,
            display: 'flex', alignItems: 'center', gap: 12,
          }}>
            <div style={{
              fontFamily: 'Geist Mono, monospace', fontSize: 11,
              color: '#f0a094', flex: 1, lineHeight: 1.4,
            }}>{current.error}</div>
            <button onClick={L.retry} style={{
              background: 'rgba(240,240,240,0.08)',
              border: '1px solid rgba(240,240,240,0.4)',
              color: '#f0f0f0',
              padding: '7px 14px', borderRadius: 2,
              fontFamily: 'inherit', fontSize: 11.5,
              letterSpacing: 1.2, textTransform: 'uppercase',
              cursor: 'pointer',
              transition: 'all 140ms',
            }}>Retry</button>
          </div>
        )}

        {/* Enter Realm button — locked until done */}
        <button
          disabled={L.phase !== 'done'}
          style={{
            marginTop: L.phase === 'error' ? 14 : 22,
            width: '100%', padding: '12px 0',
            background: L.phase === 'done' ? '#a1c2f7' : 'transparent',
            color: L.phase === 'done' ? '#111' : 'rgba(240,240,240,0.45)',
            border: `1px solid ${L.phase === 'done' ? '#a1c2f7' : 'rgba(240,240,240,0.25)'}`,
            borderRadius: 2,
            cursor: L.phase === 'done' ? 'pointer' : 'not-allowed',
            fontSize: 13, fontWeight: 500, fontFamily: 'inherit',
            letterSpacing: 2, textTransform: 'uppercase',
            transition: 'all 200ms',
          }}>
          {L.phase === 'done' ? 'Enter Realm' : L.phase === 'error' ? 'Locked' : 'Loading…'}
        </button>
      </div>
    </div>
  );
}

function ManifestRow({ item, offset, rowHeight }) {
  const isActive = offset === 0;
  const distance = Math.abs(offset);
  const opacity = distance > 2 ? 0 : 1 - distance * 0.18;

  return (
    <div style={{
      height: rowHeight,
      display: 'flex', alignItems: 'center', gap: 12,
      padding: '0 14px',
      opacity,
      transition: 'opacity 380ms ease',
    }}>
      <RowIcon status={item.status} />
      <div style={{
        flex: 1,
        fontSize: 14,
        color: item.status === 'done' ? 'rgba(240,240,240,0.7)'
          : item.status === 'loading' ? '#f0f0f0'
          : item.status === 'error' ? '#f0a094'
          : 'rgba(240,240,240,0.65)',
        fontWeight: isActive && item.status === 'loading' ? 500 : 400,
        letterSpacing: 0.1,
      }}>{item.label}</div>
      <div style={{
        fontFamily: 'Geist Mono, monospace', fontSize: 10.5,
        letterSpacing: 0.5,
        color: item.status === 'done' ? 'rgba(189,224,180,0.85)'
          : item.status === 'loading' ? '#c0d8ff'
          : item.status === 'error' ? '#f0a094'
          : 'rgba(240,240,240,0.4)',
        minWidth: 58, textAlign: 'right',
      }}>
        {item.status === 'done' && (item.cached ? 'cached' : item.durationMs ? `${item.durationMs}ms` : 'done')}
        {item.status === 'loading' && <span className="ld-loading-dots">loading</span>}
        {item.status === 'error' && 'failed'}
        {item.status === 'pending' && 'queued'}
      </div>

      <style>{`
        .ld-loading-dots::after {
          content: '';
          display: inline-block;
          width: 1em; text-align: left;
          animation: ld-dot-wave 1.4s steps(4, end) infinite;
        }
        @keyframes ld-dot-wave {
          0%   { content: ''; }
          25%  { content: '.'; }
          50%  { content: '..'; }
          75%  { content: '...'; }
          100% { content: ''; }
        }
      `}</style>
    </div>
  );
}

function RowIcon({ status }) {
  if (status === 'done') return <IconCheck color="#bde0b4" size={14} />;
  if (status === 'error') return <IconX color="#f0a094" size={14} />;
  if (status === 'loading') return (
    <div style={{
      width: 12, height: 12,
      border: '1.5px solid rgba(161,194,247,0.28)',
      borderTopColor: '#a1c2f7',
      borderRadius: '50%',
      animation: 'lr-spin 0.9s linear infinite',
    }}>
      <style>{`@keyframes lr-spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
  // pending
  return <div style={{
    width: 6, height: 6, borderRadius: '50%',
    background: 'rgba(240,240,240,0.35)',
    margin: '0 4px',
  }} />;
}

function Dots() {
  return (
    <span className="ld-loading-dots" style={{ marginLeft: 2 }} />
  );
}

function RefinedDiamond({ animate, state }) {
  const color = state === 'error' ? '#f0a094' : '#a1c2f7';
  return (
    <div style={{
      width: 16, height: 16, transform: 'rotate(45deg)',
      border: `1px solid ${color}`,
      boxShadow: `0 0 8px ${state === 'error' ? 'rgba(240,160,148,0.45)' : 'rgba(161,194,247,0.45)'}`,
      position: 'relative',
      animation: animate ? 'rd-pulse 1.6s ease-in-out infinite' : 'none',
      transition: 'border-color 200ms, box-shadow 200ms',
    }}>
      <div style={{
        position: 'absolute', inset: 4,
        background: color,
        transition: 'background 200ms',
      }} />
      <style>{`@keyframes rd-pulse {
        0%, 100% { box-shadow: 0 0 6px rgba(161,194,247,0.35); }
        50%      { box-shadow: 0 0 14px rgba(161,194,247,0.85); }
      }`}</style>
    </div>
  );
}

window.LoadingRefined = LoadingRefined;
