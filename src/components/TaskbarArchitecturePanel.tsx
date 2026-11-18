import React from 'react';
import {
  AppWindow,
  Link2,
  Shield,
  Terminal,
  CheckCircle2,
  Sparkles,
} from 'lucide-react';
import { AccountInstance, HubEventLog } from '../types/hub';

interface TaskbarArchitecturePanelProps {
  accounts: AccountInstance[];
  shortcutsReady: boolean;
  logs: HubEventLog[];
  onRegenerateShortcuts: () => void;
}

export const TaskbarArchitecturePanel: React.FC<TaskbarArchitecturePanelProps> = React.memo(({
  accounts,
  shortcutsReady,
  logs,
  onRegenerateShortcuts,
}) => {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
      {/* Left 7 cols: Windows Taskbar AUMID & Profile Mapping Matrix */}
      <div className="lg:col-span-7 glass-panel rounded-xl p-4 sm:p-5 border border-white/[0.08] flex flex-col justify-between">
        <div>
          <div className="flex flex-wrap items-center justify-between gap-2 mb-3.5">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-violet-500/10 border border-violet-500/20 flex items-center justify-center text-violet-400">
                <AppWindow className="w-4 h-4" />
              </div>
              <div>
                <h3 className="font-sans font-semibold text-sm text-zinc-100">
                  Taskbar AUMID Architecture & Process Separation
                </h3>
                <p className="text-[11px] text-zinc-400">
                  Independent <span className="font-mono text-zinc-300">System.AppUserModel.ID</span> tiles with HWND property stamping
                </p>
              </div>
            </div>

            <button
              onClick={onRegenerateShortcuts}
              className="btn-pro flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium whitespace-nowrap cursor-pointer"
            >
              <Link2 className="w-3.5 h-3.5 text-violet-400 shrink-0" />
              <span>Rebuild Shortcuts</span>
            </button>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-white/[0.06] text-zinc-400 font-medium">
                  <th className="py-2 pr-3">Account</th>
                  <th className="py-2 px-3">Windows AUMID</th>
                  <th className="py-2 px-3">Profile Directory</th>
                  <th className="py-2 pl-3 text-right">Taskbar Tile</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/[0.04] font-mono text-[11px]">
                {accounts.map((acc) => (
                  <tr key={acc.id} className="hover:bg-white/[0.02] transition-colors">
                    <td className="py-2 pr-3 font-sans font-medium text-zinc-200">
                      #{acc.id} {acc.label}
                    </td>
                    <td className="py-2 px-3 text-sky-400/90">{acc.aumid}</td>
                    <td className="py-2 px-3 text-zinc-400 truncate max-w-[200px]">
                      ...\Account{acc.id}
                    </td>
                    <td className="py-2 pl-3 text-right">
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20">
                        <CheckCircle2 className="w-2.5 h-2.5" />
                        {shortcutsReady ? 'Unstacked' : 'Ready'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="mt-3.5 pt-3 border-t border-white/[0.06] flex flex-wrap items-center justify-between gap-2 text-[11px] text-zinc-400">
          <span className="flex items-center gap-1.5">
            <Shield className="w-3.5 h-3.5 text-emerald-400" />
            OAuth & cookies isolated per profile
          </span>
          <span className="flex items-center gap-1.5">
            <Sparkles className="w-3.5 h-3.5 text-sky-400" />
            Shared brain synced via ANTIGRAVITY_APP_DATA_DIR
          </span>
        </div>
      </div>

      {/* Right 5 cols: Real-Time Process & Concurrency Event Log */}
      <div className="lg:col-span-5 glass-panel rounded-xl p-4 sm:p-5 border border-white/[0.08] flex flex-col justify-between">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <Terminal className="w-4 h-4 text-sky-400" />
            <h3 className="font-sans font-semibold text-sm text-zinc-100">
              Process & Concurrency Stream
            </h3>
          </div>
          <span className="text-[10px] font-mono text-zinc-400 uppercase tracking-wider">
            Win32 Live Monitor
          </span>
        </div>

        <div className="h-44 overflow-y-auto space-y-1 pr-1 font-mono text-[11px] bg-black/35 rounded-lg p-2.5 border border-white/[0.04]">
          {logs.length === 0 ? (
            <div className="text-zinc-500 py-6 text-center text-xs">
              Monitoring Antigravity instances and SQLite locks...
            </div>
          ) : (
            logs.map((entry) => {
              const colorClass =
                entry.level === 'error'
                  ? 'text-rose-400'
                  : entry.level === 'warn'
                    ? 'text-amber-400'
                    : entry.level === 'success'
                      ? 'text-emerald-400'
                      : 'text-sky-400';
              return (
                <div
                  key={entry.id}
                  className="flex items-start gap-1.5 leading-snug border-b border-white/[0.02] pb-0.5"
                >
                  <span className="text-zinc-600 shrink-0">[{entry.timestamp}]</span>
                  <span className={`font-semibold shrink-0 ${colorClass}`}>
                    [{entry.source}]
                  </span>
                  <span className="text-zinc-300 break-words">{entry.message}</span>
                </div>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
});
