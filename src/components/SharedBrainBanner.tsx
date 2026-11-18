import React from 'react';
import {
  Database,
  FolderOpen,
  Lock,
  Unlock,
  Sparkles,
  RefreshCw,
  CheckCircle2,
  Layers,
  HardDrive,
} from 'lucide-react';
import { SharedBrainTelemetry } from '../types/hub';

interface SharedBrainBannerProps {
  brain: SharedBrainTelemetry;
  onOpenSharedStorage: () => void;
  onOptimizeSqlite: () => void;
  onSyncWorkspaces: () => void;
}

export const SharedBrainBanner: React.FC<SharedBrainBannerProps> = React.memo(({
  brain,
  onOpenSharedStorage,
  onOptimizeSqlite,
  onSyncWorkspaces,
}) => {
  return (
    <div className="glass-panel rounded-xl p-3.5 sm:p-4 border border-white/[0.08] relative overflow-hidden">
      <div className="flex flex-wrap items-center justify-between gap-3.5">
        {/* Left: Identity + Path */}
        <div className="flex items-center gap-3 min-w-0">
          <div className="w-9 h-9 rounded-lg bg-sky-500/10 border border-sky-500/20 flex items-center justify-center shrink-0 text-sky-400">
            <Database className="w-4 h-4" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h2 className="font-sans font-semibold text-sm text-zinc-100">
                Unified Agent Brain & Conversation Synchronizer
              </h2>
              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-mono font-medium bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 shrink-0">
                <CheckCircle2 className="w-2.5 h-2.5" />
                ENFORCED
              </span>
            </div>
            <p
              className="text-[11px] text-zinc-400 font-mono mt-0.5 truncate max-w-lg"
              title={brain.appDataDir}
            >
              Shared Root: {brain.appDataDir}
            </p>
          </div>
        </div>

        {/* Middle: Metrics in sleek horizontal chips */}
        <div className="flex flex-wrap items-center gap-2 text-xs">
          <div className="px-2.5 py-1 rounded-md bg-black/30 border border-white/[0.05] flex items-center gap-1.5 shrink-0">
            <Sparkles className="w-3 h-3 text-sky-400" />
            <span className="text-zinc-400 text-[11px]">Conversations:</span>
            <span className="font-mono font-semibold text-zinc-200 text-xs">
              {brain.conversationCount}
            </span>
          </div>

          <div className="px-2.5 py-1 rounded-md bg-black/30 border border-white/[0.05] flex items-center gap-1.5 shrink-0">
            <Layers className="w-3 h-3 text-violet-400" />
            <span className="text-zinc-400 text-[11px]">Brain Contexts:</span>
            <span className="font-mono font-semibold text-zinc-200 text-xs">
              {brain.brainFolderCount}
            </span>
          </div>

          <div className="px-2.5 py-1 rounded-md bg-black/30 border border-white/[0.05] flex items-center gap-1.5 shrink-0">
            <HardDrive className="w-3 h-3 text-emerald-400" />
            <span className="text-zinc-400 text-[11px]">DB Size:</span>
            <span className="font-mono font-semibold text-zinc-200 text-xs">
              {brain.dbSizeMb.toFixed(2)} MB
            </span>
          </div>

          <div
            className={`px-2.5 py-1 rounded-md border flex items-center gap-1.5 text-[11px] shrink-0 ${
              brain.isLocked
                ? 'bg-amber-500/10 border-amber-500/25 text-amber-300'
                : 'bg-emerald-500/10 border-emerald-500/20 text-emerald-300'
            }`}
          >
            {brain.isLocked ? (
              <>
                <Lock className="w-3 h-3 text-amber-400" />
                <span className="font-mono font-medium">
                  Busy (PID {brain.lockHolderPid || 'Active'})
                </span>
              </>
            ) : (
              <>
                <Unlock className="w-3 h-3 text-emerald-400" />
                <span className="font-mono font-medium">
                  {brain.walModeActive ? 'WAL Mode Active' : 'Unlocked'}
                </span>
              </>
            )}
          </div>
        </div>

        {/* Right: Actions */}
        <div className="flex items-center gap-1.5 shrink-0 flex-wrap">
          <button
            onClick={onSyncWorkspaces}
            className="btn-pro flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium whitespace-nowrap cursor-pointer"
            title="Synchronize settings, snippets, and project paths across accounts"
          >
            <RefreshCw className="w-3 h-3 text-sky-400 shrink-0" />
            <span>Sync Workspaces</span>
          </button>

          <button
            onClick={onOptimizeSqlite}
            className="btn-pro flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium whitespace-nowrap cursor-pointer"
            title="Verify SQLite WAL journal integrity & clear stale locks"
          >
            <Database className="w-3 h-3 text-emerald-400 shrink-0" />
            <span>Verify Lock</span>
          </button>

          <button
            onClick={onOpenSharedStorage}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium whitespace-nowrap bg-sky-500/15 hover:bg-sky-500/25 text-sky-200 border border-sky-500/30 transition-all cursor-pointer"
          >
            <FolderOpen className="w-3.5 h-3.5 text-sky-400 shrink-0" />
            <span>Open Storage</span>
          </button>
        </div>
      </div>
    </div>
  );
});
