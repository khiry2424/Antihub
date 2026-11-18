import React, { useEffect, useState } from 'react';
import { X, Save, Settings, FolderCog, FolderOpen, ShieldCheck } from 'lucide-react';
import { HubConfig } from '../types/hub';
import { hubBridge } from '../bridge/hubBridge';

interface SettingsModalProps {
  config: HubConfig;
  isOpen: boolean;
  onClose: () => void;
  onSave: (updated: HubConfig) => void;
}

interface PathFieldProps {
  label: string;
  value: string;
  mode: 'file' | 'folder';
  dialogTitle: string;
  onChange: (next: string) => void;
  compact?: boolean;
}

const PathField: React.FC<PathFieldProps> = ({
  label,
  value,
  mode,
  dialogTitle,
  onChange,
  compact = false,
}) => {
  const handleBrowse = async () => {
    try {
      const res =
        mode === 'file'
          ? await hubBridge.browseFile(value, dialogTitle)
          : await hubBridge.browseFolder(value, dialogTitle);
      if (res.ok && res.path) {
        onChange(res.path);
      }
    } catch {
      // Ignore canceled/failed picker
    }
  };

  return (
    <div>
      <label className={compact ? 'block text-[10px] text-zinc-400 mb-0.5' : 'block text-zinc-400 mb-1 text-[11px]'}>
        {label}
      </label>
      <div className="flex items-center gap-1.5">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className={
            compact
              ? 'flex-1 min-w-0 px-2 py-1 rounded bg-black/40 border border-white/[0.08] text-zinc-200 font-mono text-[11px] focus:outline-none focus:border-sky-500/60'
              : 'flex-1 min-w-0 px-3 py-1.5 rounded-lg bg-black/40 border border-white/[0.08] font-mono text-zinc-200 focus:outline-none focus:border-sky-500/60'
          }
        />
        <button
          type="button"
          onClick={handleBrowse}
          title={dialogTitle}
          className={
            compact
              ? 'shrink-0 p-1.5 rounded bg-white/[0.05] hover:bg-sky-500/20 border border-white/[0.08] hover:border-sky-500/40 text-zinc-300 hover:text-sky-300 transition-colors cursor-pointer'
              : 'shrink-0 px-2.5 py-1.5 rounded-lg bg-white/[0.05] hover:bg-sky-500/20 border border-white/[0.08] hover:border-sky-500/40 text-zinc-300 hover:text-sky-300 transition-colors cursor-pointer flex items-center gap-1'
          }
        >
          <FolderOpen className="w-3.5 h-3.5" />
          {!compact && <span className="text-[11px]">Browse</span>}
        </button>
      </div>
    </div>
  );
};

export const SettingsModal: React.FC<SettingsModalProps> = ({
  config,
  isOpen,
  onClose,
  onSave,
}) => {
  const [draft, setDraft] = useState<HubConfig>(() => structuredClone(config));

  useEffect(() => {
    if (isOpen) {
      setDraft(structuredClone(config));
    }
  }, [isOpen, config]);

  if (!isOpen) return null;

  const updateAccountField = (
    id: number,
    field: 'label' | 'badgeText' | 'defaultWorkspace',
    value: string
  ) => {
    setDraft((prev) => ({
      ...prev,
      accounts: prev.accounts.map((acc) =>
        acc.id === id ? { ...acc, [field]: value } : acc
      ),
    }));
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4">
      <div className="glass-panel w-full max-w-2xl rounded-xl border border-white/[0.1] shadow-2xl overflow-hidden bg-[#0c0e15]">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-white/[0.06]">
          <div className="flex items-center gap-2">
            <Settings className="w-4 h-4 text-sky-400" />
            <h2 className="font-sans font-semibold text-sm sm:text-base text-zinc-100">
              Hub Settings & Paths
            </h2>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded-lg text-zinc-400 hover:text-white hover:bg-white/[0.05] transition-colors cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4 max-h-[75vh] overflow-y-auto text-xs">
          {/* Core Executable & Shared Brain Paths */}
          <div className="space-y-3">
            <div className="flex items-center gap-2 text-xs font-semibold text-sky-300">
              <FolderCog className="w-3.5 h-3.5" />
              <span>Core Executable & Storage Paths</span>
            </div>

            <PathField
              label="Antigravity Executable Path (Antigravity.exe)"
              value={draft.antigravityExePath}
              mode="file"
              dialogTitle="Select Antigravity.exe"
              onChange={(val) => setDraft({ ...draft, antigravityExePath: val })}
            />

            <PathField
              label="Shared Brain & Conversation Directory (ANTIGRAVITY_APP_DATA_DIR)"
              value={draft.sharedAppDataDir}
              mode="folder"
              dialogTitle="Select Shared Brain Directory"
              onChange={(val) => setDraft({ ...draft, sharedAppDataDir: val })}
            />

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <PathField
                label="Isolated Profiles Root (%APPDATA%\Antigravity-Profiles)"
                value={draft.profilesRootDir}
                mode="folder"
                dialogTitle="Select Isolated Profiles Root Directory"
                onChange={(val) => setDraft({ ...draft, profilesRootDir: val })}
              />

              <PathField
                label="Global Default Workspace Directory"
                value={draft.defaultWorkspaceDir}
                mode="folder"
                dialogTitle="Select Global Default Workspace Directory"
                onChange={(val) => setDraft({ ...draft, defaultWorkspaceDir: val })}
              />
            </div>
          </div>

          {/* Sync & Windows Integration Toggles */}
          <div className="pt-2 border-t border-white/[0.06] space-y-2.5">
            <div className="flex items-center gap-2 text-xs font-semibold text-violet-300">
              <ShieldCheck className="w-3.5 h-3.5" />
              <span>Workspace Sync & Windows Shell Options</span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px]">
              <label className="flex items-center gap-2 p-2 rounded-lg bg-black/25 border border-white/[0.04] cursor-pointer">
                <input
                  type="checkbox"
                  checked={draft.syncUserSettings}
                  onChange={(e) =>
                    setDraft({ ...draft, syncUserSettings: e.target.checked })
                  }
                  className="rounded border-zinc-700 text-sky-500 focus:ring-0"
                />
                <span className="text-zinc-300">Sync VS Code settings.json</span>
              </label>

              <label className="flex items-center gap-2 p-2 rounded-lg bg-black/25 border border-white/[0.04] cursor-pointer">
                <input
                  type="checkbox"
                  checked={draft.syncKeybindings}
                  onChange={(e) =>
                    setDraft({ ...draft, syncKeybindings: e.target.checked })
                  }
                  className="rounded border-zinc-700 text-sky-500 focus:ring-0"
                />
                <span className="text-zinc-300">Sync keybindings.json</span>
              </label>

              <label className="flex items-center gap-2 p-2 rounded-lg bg-black/25 border border-white/[0.04] cursor-pointer">
                <input
                  type="checkbox"
                  checked={draft.autoStampWindowAumid}
                  onChange={(e) =>
                    setDraft({
                      ...draft,
                      autoStampWindowAumid: e.target.checked,
                    })
                  }
                  className="rounded border-zinc-700 text-sky-500 focus:ring-0"
                />
                <span className="text-zinc-300">Auto-stamp HWND Taskbar AUMID</span>
              </label>

              <label className="flex items-center gap-2 p-2 rounded-lg bg-black/25 border border-white/[0.04] cursor-pointer">
                <input
                  type="checkbox"
                  checked={draft.minimizeToTrayOnClose}
                  onChange={(e) =>
                    setDraft({
                      ...draft,
                      minimizeToTrayOnClose: e.target.checked,
                    })
                  }
                  className="rounded border-zinc-700 text-sky-500 focus:ring-0"
                />
                <span className="text-zinc-300">Minimize to Tray on close [X]</span>
              </label>
            </div>
          </div>

          {/* Account Labels & Badges */}
          <div className="pt-2 border-t border-white/[0.06] space-y-3">
            <span className="text-xs font-semibold text-zinc-300">
              Account Labels & Default Workspaces
            </span>

            <div className="space-y-2">
              {draft.accounts.map((acc) => (
                <div
                  key={acc.id}
                  className="p-2.5 rounded-lg bg-black/25 border border-white/[0.04] grid grid-cols-1 sm:grid-cols-3 gap-2"
                >
                  <div>
                    <label className="block text-[10px] text-zinc-400 mb-0.5">
                      Account #{acc.id} Label
                    </label>
                    <input
                      type="text"
                      value={acc.label}
                      onChange={(e) =>
                        updateAccountField(acc.id, 'label', e.target.value)
                      }
                      className="w-full px-2 py-1 rounded bg-black/40 border border-white/[0.08] text-zinc-200"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] text-zinc-400 mb-0.5">
                      Badge Text
                    </label>
                    <input
                      type="text"
                      value={acc.badgeText}
                      onChange={(e) =>
                        updateAccountField(acc.id, 'badgeText', e.target.value)
                      }
                      className="w-full px-2 py-1 rounded bg-black/40 border border-white/[0.08] text-zinc-200 font-mono text-[11px]"
                    />
                  </div>

                  <PathField
                    label="Default Project Workspace"
                    value={acc.defaultWorkspace}
                    mode="folder"
                    dialogTitle={`Select Default Workspace for Account ${acc.id}`}
                    compact
                    onChange={(val) =>
                      updateAccountField(acc.id, 'defaultWorkspace', val)
                    }
                  />
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-white/[0.06] bg-black/20">
          <button
            onClick={onClose}
            className="btn-pro px-3 py-1.5 rounded-lg text-xs font-medium cursor-pointer"
          >
            Cancel
          </button>
          <button
            onClick={() => {
              onSave({
                ...draft,
                pollIntervalMs: Math.min(60000, Math.max(500, Number(draft.pollIntervalMs) || 1500)),
              });
              onClose();
            }}
            className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg text-xs font-medium bg-emerald-500/15 hover:bg-emerald-500/25 text-emerald-300 border border-emerald-500/30 transition-all cursor-pointer"
          >
            <Save className="w-3.5 h-3.5" />
            <span>Save Settings</span>
          </button>
        </div>
      </div>
    </div>
  );
};
