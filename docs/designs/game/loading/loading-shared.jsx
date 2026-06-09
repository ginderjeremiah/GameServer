/* Shared loader for the modernized loading screen.

   Models real, sequential network calls — not a time-based fake progress
   bar. Each item is a separate request; progress is count-based; phases
   transition on real events. Supports four scenarios for the prototype.

     phases:  'checking' → 'loading' → 'done'
                            ↘ 'error' (paused; awaits retry())
     item.status:  'pending' | 'loading' | 'done' | 'error'
                   (start as 'cached' for the partial scenario — rendered as
                    done immediately, no spinner)
*/

const LOAD_ITEMS = [
  { key: 'zones',      label: 'Zones' },
  { key: 'enemies',    label: 'Enemies' },
  { key: 'items',      label: 'Items' },
  { key: 'skills',     label: 'Skills' },
  { key: 'itemMods',   label: 'Item Mods' },
  { key: 'attributes', label: 'Attributes' },
  { key: 'challenges', label: 'Challenges' },
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function buildItems(scenario) {
  return LOAD_ITEMS.map((it, i) => ({
    ...it,
    status: scenario === 'partial' && i < 3 ? 'done' : 'pending',
    durationMs: 0,
    error: null,
    retried: false,
  }));
}

function useNetworkLoader(scenario = 'fresh', opts = {}) {
  const [, force] = React.useReducer((x) => x + 1, 0);
  const ref = React.useRef(null);

  // (Re)initialize when scenario changes
  React.useEffect(() => {
    const token = Symbol('run');
    ref.current = {
      token,
      scenario,
      items: buildItems(scenario),
      phase: 'checking',
      activeIndex: -1,
      checkMs: 0,
    };
    force();
    runMachine(token);
    return () => {
      if (ref.current) ref.current.token = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scenario]);

  async function runMachine(token, fromError = false) {
    const cancelled = () => !ref.current || ref.current.token !== token;

    if (!fromError) {
      // ── checking phase ────────────────────────────────────────────────
      ref.current.phase = 'checking';
      ref.current.activeIndex = -1;
      force();
      const checkDelay = scenario === 'cached' ? 480 : scenario === 'partial' ? 560 : 700;
      await sleep(checkDelay);
      if (cancelled()) return;

      if (scenario === 'cached') {
        // All up to date — flash everything to done and finish.
        ref.current.items = ref.current.items.map((it) => ({
          ...it, status: 'done', durationMs: 0, cached: true,
        }));
        ref.current.phase = 'done';
        force();
        if (opts.autoRestart !== false) {
          await sleep(opts.completePauseMs ?? 2600);
          if (cancelled()) return;
          restart();
        }
        return;
      }
    }

    // ── loading phase: sequential per-item ────────────────────────────
    ref.current.phase = 'loading';

    for (let i = 0; i < ref.current.items.length; i++) {
      const it = ref.current.items[i];
      if (it.status === 'done') continue;

      ref.current.activeIndex = i;
      ref.current.items[i] = { ...it, status: 'loading', error: null };
      force();

      // simulate a real fetch — unknown but realistic
      const start = performance.now();
      const realDelay = 380 + Math.random() * 520;
      await sleep(realDelay);
      if (cancelled()) return;

      // failOnce: row #3 (Skills) fails first try only
      const shouldFail =
        scenario === 'failOnce' && i === 3 && !ref.current.items[i].retried;

      if (shouldFail) {
        ref.current.items[i] = {
          ...ref.current.items[i],
          status: 'error',
          error: 'Network timeout — could not reach server.',
        };
        ref.current.phase = 'error';
        force();
        return; // wait for retry()
      }

      ref.current.items[i] = {
        ...ref.current.items[i],
        status: 'done',
        durationMs: Math.round(performance.now() - start),
      };
      force();
      await sleep(80); // brief beat between items
      if (cancelled()) return;
    }

    ref.current.phase = 'done';
    ref.current.activeIndex = ref.current.items.length;
    force();

    if (opts.autoRestart !== false) {
      await sleep(opts.completePauseMs ?? 3200);
      if (cancelled()) return;
      restart();
    }
  }

  function restart() {
    const token = Symbol('run');
    ref.current = {
      token,
      scenario: ref.current.scenario,
      items: buildItems(ref.current.scenario),
      phase: 'checking',
      activeIndex: -1,
      checkMs: 0,
    };
    force();
    runMachine(token);
  }

  function retry() {
    if (!ref.current || ref.current.phase !== 'error') return;
    const i = ref.current.activeIndex;
    ref.current.items[i] = {
      ...ref.current.items[i],
      status: 'loading',
      error: null,
      retried: true,
    };
    ref.current.phase = 'loading';
    force();
    runMachine(ref.current.token, true);
  }

  const s = ref.current || {
    items: buildItems(scenario), phase: 'checking', activeIndex: -1,
  };
  const completed = s.items.filter((i) => i.status === 'done').length;

  return {
    items: s.items,
    phase: s.phase,           // 'checking' | 'loading' | 'done' | 'error'
    activeIndex: s.activeIndex,
    completed,
    total: s.items.length,
    restart,
    retry,
    scenario,
  };
}

Object.assign(window, { LOAD_ITEMS, useNetworkLoader });
