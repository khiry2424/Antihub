import React, { useEffect, useState, useCallback } from 'react';
import {
  Rocket,
  OctagonX,
  Settings,
  Minimize2,
  Activity,
  Cpu,
  HardDrive,
  Layers,
  CheckCircle2,
  AlertTriangle,
} from 'lucide-react';
import { hubBridge } from './bridge/hubBridge';
import { HubConfig, HubEventLog, HubStateSnapshot } from './types/hub';
import { AccountCard } from './components/AccountCard';
import { SharedBrainBanner } from './components/SharedBrainBanner';
import { TaskbarArchitecturePanel } from './components/TaskbarArchitecturePanel';
import { SettingsModal } from './components/SettingsModal';

export const App: React.FC = () => {
  const [snapshot, setSnapshot] = useState<HubStateSnapshot | null>(null);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [toast, setToast] = useState<{
    message: string;
    type: 'success' | 'warn' | 'error';
  } | null>(null);
  const [logs, setLogs] = useState<HubEventLog[]>([
    {
      id: 'init-1',
      timestamp: new Date().toLocaleTimeString(),
      level: 'info',
      source: 'HUB-CORE',
      message:
        'Initialized Antigravity Multi-Account Hub & System Tray Process Monitor',
    },
    {
      id: 'init-2',
      timestamp: new Date().toLocaleTimeString(),
      level: 'success',
      source: 'BRAIN-SYNC',
      message:
        'Bound ANTIGRAVITY_APP_DATA_DIR -> %USERPROFILE%\\.gemini\\antigravity',
    },
    {
      id: 'init-3',
      timestamp: new Date().toLocaleTimeString(),
      level: 'info',
      source: 'AUMID-SHELL',
      message:
        'Verified 3 independent AppUserModelIDs (Antigravity.Account.1..3)',
    },
  ]);

  const appendLog = useCallback(
    (
      level: 'info' | 'success' | 'warn' | 'error',
      source: string,
      message: string
    ) => {
      setLogs((prev) => [
        {
          id: `${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
          timestamp: new Date().toLocaleTimeString(),
          level,
          source,
          message,
        },
        ...prev.slice(0, 49),
      ]);
    },
    []
  );

  const showToast = useCallback(
    (message: string, type: 'success' | 'warn' | 'error' = 'success') => {
      setToast({ message, type });
      setTimeout(() => {
        setToast((current) => (current?.message === message ? null : current));
      }, 3500);
    },
    []
  );

  const refreshState = useCallback(async () => {
    try {
      const data = await hubBridge.getHubState();
      setSnapshot(data);
    } catch (err: any) {
      console.error('State poll error:', err);
    }
  }, []);

  useEffect(() => {
    refreshState();
    const intervalMs = snapshot?.config?.pollIntervalMs ?? 1500;
    let timer: ReturnType<typeof setInterval> | null = null;

    const startPolling = () => {
      if (timer) clearInterval(timer);
      timer = setInterval(() => {
        if (!document.hidden) {
          refreshState();
        }
      }, intervalMs);
    };

    const handleVisibility = () => {
      if (!document.hidden) {
        refreshState();
      }
    };

    startPolling();
    document.addEventListener('visibilitychange', handleVisibility);
    return () => {
      if (timer) clearInterval(timer);
      document.removeEventListener('visibilitychange', handleVisibility);
    };
  }, [refreshState, snapshot?.config?.pollIntervalMs]);

  // Listen for native System Tray menu events pushed into window
  useEffect(() => {
    const handler = (e: Event) => {
      const custom = e as CustomEvent<{
        type: string;
        message?: string;
        openSettings?: boolean;
      }>;
      if (custom.detail?.openSettings) {
        setIsSettingsOpen(true);
      }
      if (custom.detail?.message) {
        appendLog('info', 'TRAY-MENU', custom.detail.message);
        showToast(custom.detail.message, 'success');
      }
      refreshState();
    };
    window.addEventListener('antigravity-tray-event', handler);
    return () => window.removeEventListener('antigravity-tray-event', handler);
  }, [appendLog, refreshState, showToast]);

  const runAction = useCallback(
    async (
      actionKey: string,
      source: string,
      fn: () => Promise<{ ok: boolean; message: string }>
    ) => {
      setBusyAction(actionKey);
      try {
        const res = await fn();
        appendLog(res.ok ? 'success' : 'warn', source, res.message);
        showToast(res.message, res.ok ? 'success' : 'warn');
        await refreshState();
      } catch (err: any) {
        const msg = err?.message || 'Operation failed';
        appendLog('error', source, msg);
        showToast(msg, 'error');
      } finally {
        setBusyAction(null);
      }
    },
    [appendLog, refreshState, showToast]
  );

  const handleLaunch = useCallback(
    (id: number) =>
      runAction(`launch-${id}`, `ACCOUNT-${id}`, () =>
        hubBridge.launchInstance(id)
      ),
    [runAction]
  );

  const handleFocus = useCallback(
    (id: number) =>
      runAction(`focus-${id}`, `WIN32-FOCUS`, () =>
        hubBridge.focusInstance(id)
      ),
    [runAction]
  );

  const handleRestart = useCallback(
    (id: number) =>
      runAction(`restart-${id}`, `ACCOUNT-${id}`, () =>
        hubBridge.restartInstance(id)
      ),
    [runAction]
  );

  const handleTerminate = useCallback(
    (id: number) =>
      runAction(`kill-${id}`, `ACCOUNT-${id}`, () =>
        hubBridge.terminateInstance(id)
      ),
    [runAction]
  );

  const handleClearAuth = useCallback(
    (id: number) =>
      runAction(`clear-auth-${id}`, `AUTH-CACHE-${id}`, () =>
        hubBridge.clearAuthCache(id)
      ),
    [runAction]
  );

  const handleOpenProfile = useCallback(
    (id: number) =>
      runAction(`open-prof-${id}`, `EXPLORER`, () =>
        hubBridge.openProfileFolder(id)
      ),
    [runAction]
  );

  const handleOpenSharedStorage = useCallback(
    () =>
      runAction('open-brain', 'EXPLORER', () =>
        hubBridge.openSharedStorageFolder()
      ),
    [runAction]
  );

  const handleOptimizeSqlite = useCallback(
    () =>
      runAction('sqlite-wal', 'SQLITE-GUARD', () =>
        hubBridge.optimizeSqliteWal()
      ),
    [runAction]
  );

  const handleSyncWorkspaces = useCallback(
    () =>
      runAction('sync-ws', 'STATE-SYNC', () =>
        hubBridge.syncWorkspacesAndSettings()
      ),
    [runAction]
  );

  const handleRegenerateShortcuts = useCallback(
    () =>
      runAction('regen-lnk', 'AUMID-SHELL', () =>
        hubBridge.regenerateShortcuts()
      ),
    [runAction]
  );

  const handleSaveConfig = useCallback(
    (updated: HubConfig) =>
      runAction('save-cfg', 'CONFIG', () => hubBridge.saveConfig(updated)),
    [runAction]
  );

  if (!snapshot) {
    return (
      <div className="min-h-screen bg-[#08090d] flex items-center justify-center text-zinc-400 font-sans">
        <div className="flex items-center gap-3 text-xs">
          <div className="w-4 h-4 rounded-full border-2 border-sky-400 border-t-transparent animate-spin" />
          <span>Connecting to Antigravity Hub Orchestrator...</span>
        </div>
      </div>
    );
  }

  const runningCount = snapshot.accounts.filter(
    (a) => a.status === 'running'
  ).length;

  return (
    <div className="min-h-screen bg-[#08090d] text-zinc-100 flex flex-col justify-between relative selection:bg-sky-500/30 selection:text-sky-200">
      {/* Toast Notification */}
      {toast && (
        <div className="fixed bottom-5 right-5 z-50 flex items-center gap-2.5 px-3.5 py-2.5 rounded-lg glass-panel border border-white/[0.12] shadow-2xl text-xs font-medium animate-fade-in">
          {toast.type === 'error' ? (
            <AlertTriangle className="w-4 h-4 text-rose-400 shrink-0" />
          ) : (
            <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
          )}
          <span className="text-zinc-100">{toast.message}</span>
        </div>
      )}

      {/* Main Container */}
      <div className="relative z-10 max-w-7xl w-full mx-auto px-4 sm:px-6 py-3 space-y-3">
        {/* Top Command Bar (Pro Studio Header) */}
        <header className="glass-panel rounded-xl px-4 py-2.5 border border-white/[0.08] flex flex-col lg:flex-row lg:items-center lg:justify-between gap-2.5">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-white/[0.04] border border-white/[0.08] flex items-center justify-center shadow-inner text-sky-400">
              <Layers className="w-4 h-4" />
            </div>

            <div>
              <div className="flex items-center gap-2">
                <h1 className="font-sans font-semibold text-base sm:text-lg tracking-tight text-white">
                  Antigravity Hub
                </h1>
                <span className="text-[10px] font-mono text-zinc-400 px-1.5 py-0.5 rounded bg-white/[0.04] border border-white/[0.06]">
                  v1.0 • Win32 Shell Engine
                </span>
              </div>
              <p className="text-[11px] text-zinc-400 mt-0.5">
                3 isolated profiles • independent taskbar tiles • shared conversation brain
              </p>
            </div>
          </div>

          {/* Master Telemetry & Actions */}
          <div className="flex flex-wrap items-center gap-2">
            {/* Live Aggregate Telemetry Pill */}
            <div className="hidden xl:flex items-center gap-2.5 px-3 py-1.5 rounded-lg bg-black/30 border border-white/[0.05] text-xs font-mono">
              <span className="flex items-center gap-1.5 text-emerald-300">
                <Activity className="w-3.5 h-3.5" />
                {runningCount}/3 Active
              </span>
              <span className="text-zinc-700">|</span>
              <span className="flex items-center gap-1 text-sky-300">
                <Cpu className="w-3.5 h-3.5" />
                {snapshot.totalCpuPercent.toFixed(1)}%
              </span>
              <span className="text-zinc-700">|</span>
              <span className="flex items-center gap-1 text-violet-300">
                <HardDrive className="w-3.5 h-3.5" />
                {snapshot.totalMemoryMb.toFixed(0)} MB
              </span>
            </div>

            <button
              onClick={() =>
                runAction('launch-all', 'ORCHESTRATOR', () =>
                  hubBridge.launchAllInstances()
                )
              }
              disabled={busyAction !== null}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-emerald-500/15 hover:bg-emerald-500/25 text-emerald-300 border border-emerald-500/30 hover:border-emerald-500/50 transition-all cursor-pointer disabled:opacity-50"
            >
              <Rocket className="w-3.5 h-3.5" />
              <span>Launch All</span>
            </button>

            <button
              onClick={() =>
                runAction('kill-all', 'ORCHESTRATOR', () =>
                  hubBridge.terminateAllInstances()
                )
              }
              disabled={busyAction !== null}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-rose-500/10 hover:bg-rose-500/20 text-rose-300 border border-rose-500/20 transition-all cursor-pointer disabled:opacity-50"
            >
              <OctagonX className="w-3.5 h-3.5" />
              <span>Terminate All</span>
            </button>

            <button
              onClick={() => setIsSettingsOpen(true)}
              className="btn-pro flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium cursor-pointer"
            >
              <Settings className="w-3.5 h-3.5 text-zinc-400" />
              <span>Settings</span>
            </button>

            <button
              onClick={() =>
                runAction('tray', 'SYSTEM-TRAY', () => hubBridge.minimizeToTray())
              }
              title="Minimize Hub to System Tray"
              className="btn-pro p-1.5 rounded-lg cursor-pointer text-zinc-400 hover:text-zinc-200"
            >
              <Minimize2 className="w-4 h-4" />
            </button>
          </div>
        </header>

        {/* PRIMARY HERO: 3 Account Instance Cards (Top of viewport!) */}
        <section className="grid grid-cols-1 min-[900px]:grid-cols-3 gap-4 items-stretch">
          {snapshot.accounts.map((account) => (
            <AccountCard
              key={account.id}
              account={account}
              busyAction={busyAction}
              onLaunch={handleLaunch}
              onFocus={handleFocus}
              onRestart={handleRestart}
              onTerminate={handleTerminate}
              onClearAuth={handleClearAuth}
              onOpenProfile={handleOpenProfile}
            />
          ))}
        </section>

        {/* BOTTOM SECTION 1: Unified Agent Brain & Conversation Synchronizer */}
        <SharedBrainBanner
          brain={snapshot.brain}
          onOpenSharedStorage={handleOpenSharedStorage}
          onOptimizeSqlite={handleOptimizeSqlite}
          onSyncWorkspaces={handleSyncWorkspaces}
        />

        {/* BOTTOM SECTION 2: Taskbar AUMID Separation & Event Stream */}
        <TaskbarArchitecturePanel
          accounts={snapshot.accounts}
          shortcutsReady={snapshot.shortcutsReady}
          logs={logs}
          onRegenerateShortcuts={handleRegenerateShortcuts}
        />
      </div>

      {/* Settings & Paths Modal */}
      <SettingsModal
        config={snapshot.config}
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        onSave={handleSaveConfig}
      />
    </div>
  );
};
