import React from 'react';
import {
  Play,
  Crosshair,
  RotateCw,
  Trash2,
  Square,
  FolderOpen,
  Cpu,
  HardDrive,
  ShieldCheck,
  Terminal,
  Clock,
  User,
} from 'lucide-react';
import { AccountInstance } from '../types/hub';

interface AccountCardProps {
  account: AccountInstance;
  busyAction: string | null;
  onLaunch: (id: number) => void;
  onFocus: (id: number) => void;
  onRestart: (id: number) => void;
  onTerminate: (id: number) => void;
  onClearAuth: (id: number) => void;
  onOpenProfile: (id: number) => void;
}

const ACCENT_CONFIG = {
  cyan: {
    dot: 'bg-sky-400',
    dotGlow: 'shadow-[0_0_8px_rgba(56,189,248,0.6)]',
    borderActive: 'border-sky-500/40 hover:border-sky-500/60',
    topHighlight: 'from-sky-500/80 via-cyan-400/30 to-transparent',
    badge: 'text-sky-300 bg-sky-500/10 border-sky-500/25',
    barFill: 'bg-gradient-to-r from-sky-500 to-cyan-400',
    primaryBtn:
      'bg-sky-500/15 hover:bg-sky-500/25 text-sky-200 border border-sky-500/35 hover:border-sky-500/60 shadow-sm shadow-sky-500/10',
  },
  violet: {
    dot: 'bg-violet-400',
    dotGlow: 'shadow-[0_0_8px_rgba(167,139,250,0.6)]',
    borderActive: 'border-violet-500/40 hover:border-violet-500/60',
    topHighlight: 'from-violet-500/80 via-purple-400/30 to-transparent',
    badge: 'text-violet-300 bg-violet-500/10 border-violet-500/25',
    barFill: 'bg-gradient-to-r from-violet-500 to-indigo-400',
    primaryBtn:
      'bg-violet-500/15 hover:bg-violet-500/25 text-violet-200 border border-violet-500/35 hover:border-violet-500/60 shadow-sm shadow-violet-500/10',
  },
  amber: {
    dot: 'bg-amber-400',
    dotGlow: 'shadow-[0_0_8px_rgba(251,191,36,0.6)]',
    borderActive: 'border-amber-500/40 hover:border-amber-500/60',
    topHighlight: 'from-amber-500/80 via-yellow-400/30 to-transparent',
    badge: 'text-amber-300 bg-amber-500/10 border-amber-500/25',
    barFill: 'bg-gradient-to-r from-amber-500 to-yellow-400',
    primaryBtn:
      'bg-amber-500/15 hover:bg-amber-500/25 text-amber-200 border border-amber-500/35 hover:border-amber-500/60 shadow-sm shadow-amber-500/10',
  },
};

function formatUptime(seconds: number): string {
  if (!seconds || seconds <= 0) return 'Offline';
  const hrs = Math.floor(seconds / 3600);
  const mins = Math.floor((seconds % 3600) / 60);
  const secs = Math.floor(seconds % 60);
  if (hrs > 0) return `${hrs}h ${mins}m ${secs}s`;
  if (mins > 0) return `${mins}m ${secs}s`;
  return `${secs}s`;
}

function formatMemory(mb: number): string {
  if (!mb || mb <= 0) return '0 MB';
  if (mb >= 10000) return `${(mb / 1024).toFixed(1)} GB`;
  return `${mb.toFixed(0)} MB`;
}

export const AccountCard: React.FC<AccountCardProps> = React.memo(({
  account,
  busyAction,
  onLaunch,
  onFocus,
  onRestart,
  onTerminate,
  onClearAuth,
  onOpenProfile,
}) => {
  const isRunning = account.status === 'running';
  const accent = ACCENT_CONFIG[account.accentColor] || ACCENT_CONFIG.cyan;
  const cpuNormalized = Math.min(100, Math.max(0, account.cpuPercent));
  const ramNormalized = Math.min(100, Math.round((account.memoryMb / 2048) * 100));

  return (
    <div
      className={`glass-panel glass-card-hover relative rounded-xl p-3.5 flex flex-col justify-between transition-all duration-200 overflow-hidden h-full ${
        isRunning ? accent.borderActive : 'border-white/[0.08]'
      }`}
    >
      {/* Top Specular Highlight */}
      {isRunning && (
        <div
          className={`absolute top-0 left-0 right-0 h-[2px] bg-gradient-to-r ${accent.topHighlight}`}
        />
      )}

      <div>
        {/* Tier 1: Subtitle Meta Row (Badge + Running Status) */}
        <div className="flex items-center justify-between gap-2 mb-1.5">
          <div className="flex items-center gap-1.5 min-w-0">
            <span
              className={`w-2 h-2 rounded-full shrink-0 ${accent.dot} ${
                isRunning ? accent.dotGlow : ''
              }`}
            />
            <span
              className={`text-[10px] font-mono font-semibold tracking-wider uppercase px-2 py-0.5 rounded border shrink-0 ${accent.badge}`}
            >
              {account.badgeText}
            </span>
          </div>

          {/* Operational Status Pill */}
          <div
            className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[11px] font-mono font-medium shrink-0 border ${
              isRunning
                ? 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30'
                : 'bg-zinc-800/40 text-zinc-400 border-zinc-700/40'
            }`}
          >
            <span
              className={`w-1.5 h-1.5 rounded-full ${
                isRunning
                  ? 'bg-emerald-400 animate-pulse shadow-[0_0_6px_#34d399]'
                  : 'bg-zinc-500'
              }`}
            />
            <span>{isRunning ? 'RUNNING' : 'STOPPED'}</span>
          </div>
        </div>

        {/* Tier 2: Account Title (Gets 100% of card width, ZERO line wrapping!) */}
        <div className="mb-2.5">
          <h3 className="font-sans font-bold text-base sm:text-lg text-white tracking-tight truncate">
            Account {account.id}: {account.label}
          </h3>
          <div className="font-mono text-[11px] text-zinc-400 mt-0.5 truncate">
            {account.aumid}
          </div>
        </div>

        {/* Google Account Authentication Banner Cardlet */}
        <div className="mb-2.5 p-2 rounded-lg bg-black/30 border border-white/[0.06] flex items-center justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0 flex-1">
            <div
              className={`w-7 h-7 rounded-md flex items-center justify-center shrink-0 ${
                account.userEmail
                  ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
                  : 'bg-zinc-800/60 text-zinc-400 border border-white/[0.06]'
              }`}
            >
              <User className="w-3.5 h-3.5" />
            </div>
            <div className="min-w-0 flex-1">
              <div className="text-[10px] uppercase font-mono tracking-wider text-zinc-400 font-medium whitespace-nowrap">
                Google Profile
              </div>
              <div
                className="font-mono text-xs truncate font-semibold"
                title={account.userEmail || 'Isolated Profile - Ready for Sign In'}
              >
                {account.userEmail ? (
                  <span className="text-emerald-300">{account.userEmail}</span>
                ) : (
                  <span className="text-zinc-400 italic">Not signed in</span>
                )}
              </div>
            </div>
          </div>
          {account.userEmail ? (
            <span className="shrink-0 whitespace-nowrap text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-semibold tracking-wider">
              ACTIVE
            </span>
          ) : (
            <span className="shrink-0 whitespace-nowrap text-[10px] font-mono px-2 py-0.5 rounded bg-zinc-800 text-zinc-400 border border-zinc-700/60 font-medium tracking-wider">
              CLEAN
            </span>
          )}
        </div>

        {/* Real-Time Precision CPU & RAM Meters */}
        <div className="grid grid-cols-2 gap-2.5 mb-2.5 p-2 rounded-lg bg-black/30 border border-white/[0.06]">
          {/* CPU Indicator */}
          <div className="space-y-1.5 min-w-0">
            <div className="flex items-center justify-between gap-1 whitespace-nowrap">
              <span className="text-zinc-400 flex items-center gap-1 text-[11px] shrink-0">
                <Cpu className="w-3 h-3 text-sky-400 shrink-0" />
                <span>CPU</span>
              </span>
              <span className="font-mono font-semibold text-zinc-200 text-[11px] tabular-nums shrink-0">
                {isRunning ? `${account.cpuPercent.toFixed(1)}%` : '0.0%'}
              </span>
            </div>
            <div className="w-full h-1 bg-zinc-800/90 rounded-full overflow-hidden">
              <div
                className={`h-full rounded-full transition-all duration-300 ${accent.barFill}`}
                style={{ width: `${isRunning ? Math.max(5, cpuNormalized) : 0}%` }}
              />
            </div>
          </div>

          {/* RAM Indicator */}
          <div className="space-y-1.5 border-l border-white/[0.06] pl-2.5 min-w-0">
            <div className="flex items-center justify-between gap-1 whitespace-nowrap">
              <span className="text-zinc-400 flex items-center gap-1 text-[11px] shrink-0">
                <HardDrive className="w-3 h-3 text-violet-400 shrink-0" />
                <span>RAM</span>
              </span>
              <span className="font-mono font-semibold text-zinc-200 text-[11px] tabular-nums shrink-0">
                {isRunning ? formatMemory(account.memoryMb) : '0 MB'}
              </span>
            </div>
            <div className="w-full h-1 bg-zinc-800/90 rounded-full overflow-hidden">
              <div
                className={`h-full rounded-full transition-all duration-300 ${accent.barFill}`}
                style={{ width: `${isRunning ? Math.max(6, ramNormalized) : 0}%` }}
              />
            </div>
          </div>
        </div>

        {/* Process & Session Specs */}
        <div className="space-y-1 text-xs mb-3">
          <div className="flex items-center justify-between gap-2 py-1 border-b border-white/[0.04]">
            <span className="text-zinc-400 flex items-center gap-1.5 shrink-0 whitespace-nowrap">
              <Terminal className="w-3.5 h-3.5 text-zinc-500 shrink-0" />
              <span>Process</span>
            </span>
            <span
              className="font-mono text-[11px] text-zinc-300 whitespace-nowrap truncate text-right tabular-nums"
              title={
                isRunning && account.pid
                  ? `PID ${account.pid} (${account.processCount} processes)`
                  : 'Not Active'
              }
            >
              {isRunning && account.pid
                ? `PID ${account.pid} (${account.processCount})`
                : 'Not Active'}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2 py-1 border-b border-white/[0.04]">
            <span className="text-zinc-400 flex items-center gap-1.5 shrink-0 whitespace-nowrap">
              <Clock className="w-3.5 h-3.5 text-zinc-500 shrink-0" />
              <span>Uptime</span>
            </span>
            <span className="font-mono text-[11px] text-zinc-300 whitespace-nowrap tabular-nums">
              {formatUptime(account.uptimeSeconds)}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2 py-1 border-b border-white/[0.04]">
            <span className="text-zinc-400 flex items-center gap-1.5 shrink-0 whitespace-nowrap">
              <ShieldCheck className="w-3.5 h-3.5 text-zinc-500 shrink-0" />
              <span>Profile Dir</span>
            </span>
            <button
              onClick={() => onOpenProfile(account.id)}
              title={account.profileDir}
              className="font-mono text-[11px] text-sky-400/90 hover:text-sky-300 flex items-center gap-1 whitespace-nowrap shrink-0 transition-colors cursor-pointer"
            >
              <span>...\Account{account.id}</span>
              <FolderOpen className="w-3 h-3 shrink-0" />
            </button>
          </div>

          <div className="pt-1">
            <div className="text-[10px] uppercase font-mono tracking-wider text-zinc-400 mb-1 whitespace-nowrap">
              Active Workspace
            </div>
            <div
              className="font-mono text-[11px] text-zinc-300 truncate bg-black/40 px-2.5 py-1.5 rounded border border-white/[0.05]"
              title={account.lastWorkspace || 'Shared Workspace Ready'}
            >
              {account.lastWorkspace || 'Shared Workspace Ready'}
            </div>
          </div>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="space-y-1.5 pt-2 border-t border-white/[0.06] relative z-10">
        {!isRunning ? (
          <button
            onClick={() => onLaunch(account.id)}
            disabled={busyAction !== null}
            className={`w-full flex items-center justify-center gap-2 py-2 px-3 rounded-lg text-xs font-semibold whitespace-nowrap transition-all cursor-pointer disabled:opacity-50 ${accent.primaryBtn}`}
          >
            <Play className="w-3.5 h-3.5 fill-current shrink-0" />
            <span>Launch Account {account.id}</span>
          </button>
        ) : (
          <div className="grid grid-cols-2 gap-1.5">
            <button
              onClick={() => onFocus(account.id)}
              disabled={busyAction !== null}
              title="Bring Account window to front (or restore if closed to background)"
              className={`flex items-center justify-center gap-1.5 py-2 px-2 rounded-lg text-[11px] xl:text-xs font-semibold whitespace-nowrap transition-all cursor-pointer disabled:opacity-50 ${accent.primaryBtn}`}
            >
              <Crosshair className="w-3.5 h-3.5 shrink-0" />
              <span>Focus Window</span>
            </button>
            <button
              onClick={() => onTerminate(account.id)}
              disabled={busyAction !== null}
              className="flex items-center justify-center gap-1.5 py-2 px-2 rounded-lg text-[11px] xl:text-xs font-semibold whitespace-nowrap bg-rose-500/15 hover:bg-rose-500/25 text-rose-300 border border-rose-500/30 transition-all cursor-pointer disabled:opacity-50"
            >
              <Square className="w-3 h-3 fill-current shrink-0" />
              <span>Kill Instance</span>
            </button>
          </div>
        )}

        <div className="grid grid-cols-2 gap-1.5">
          <button
            onClick={() => onRestart(account.id)}
            disabled={busyAction !== null}
            className="btn-pro flex items-center justify-center gap-1.5 py-1.5 px-2 rounded-lg text-[11px] xl:text-xs font-medium whitespace-nowrap cursor-pointer disabled:opacity-50"
          >
            <RotateCw className="w-3 h-3 text-zinc-400 shrink-0" />
            <span>Restart</span>
          </button>

          <button
            onClick={() => onClearAuth(account.id)}
            disabled={busyAction !== null}
            title="Clears isolated Chromium OAuth/Session cookies for this account while preserving shared conversations & brain"
            className="btn-pro hover:bg-amber-500/10 hover:text-amber-200 hover:border-amber-500/20 flex items-center justify-center gap-1.5 py-1.5 px-2 rounded-lg text-[11px] xl:text-xs font-medium whitespace-nowrap cursor-pointer disabled:opacity-50"
          >
            <Trash2 className="w-3 h-3 text-zinc-400 shrink-0" />
            <span>Reset Auth</span>
          </button>
        </div>
      </div>
    </div>
  );
});
