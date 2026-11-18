import { HubConfig, HubStateSnapshot } from '../types/hub';

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
    __ANTIGRAVITY_NATIVE__?: {
      invoke: (cmd: string, args?: Record<string, unknown>) => Promise<unknown>;
    };
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
        addEventListener: (
          type: string,
          listener: (event: { data: unknown }) => void
        ) => void;
      };
    };
  }
}

type PendingRequest = {
  resolve: (value: any) => void;
  reject: (reason?: any) => void;
};

const pendingRequests = new Map<string, PendingRequest>();
let webviewInitialized = false;

function initWebViewBridge() {
  if (webviewInitialized) return;
  if (window.chrome?.webview) {
    webviewInitialized = true;
    window.chrome.webview.addEventListener('message', (event) => {
      try {
        const payload =
          typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (payload && payload.type === 'tray-event') {
          window.dispatchEvent(
            new CustomEvent('antigravity-tray-event', {
              detail: {
                message: payload.message ?? '',
                openSettings: Boolean(payload.openSettings),
              },
            })
          );
          return;
        }
        if (payload && payload.requestId && pendingRequests.has(payload.requestId)) {
          const req = pendingRequests.get(payload.requestId)!;
          pendingRequests.delete(payload.requestId);
          if (payload.error) {
            req.reject(new Error(payload.error));
          } else {
            req.resolve(payload.result);
          }
        }
      } catch (err) {
        console.error('Bridge message parse error:', err);
      }
    });
  }
}

async function callBackend<T>(
  command: string,
  args: Record<string, unknown> = {}
): Promise<T> {
  // 1. Native Windows WebView2 Host
  if (window.chrome?.webview) {
    initWebViewBridge();
    const requestId = `${command}_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
    return new Promise<T>((resolve, reject) => {
      pendingRequests.set(requestId, { resolve, reject });
      window.chrome!.webview!.postMessage(
        JSON.stringify({ requestId, command, args })
      );
      const timeoutMs = command.startsWith('browse_') ? 120000 : 15000;
      setTimeout(() => {
        if (pendingRequests.has(requestId)) {
          pendingRequests.delete(requestId);
          reject(new Error(`Backend timeout on command: ${command}`));
        }
      }, timeoutMs);
    });
  }

  // 2. Direct Native Injected Object
  if (window.__ANTIGRAVITY_NATIVE__) {
    return (await window.__ANTIGRAVITY_NATIVE__.invoke(command, args)) as T;
  }

  // 3. Tauri v2 Runtime
  if (window.__TAURI_INTERNALS__) {
    const { invoke } = await import('@tauri-apps/api/core');
    return await invoke<T>(command, args);
  }

  // 4. Dev Fallback State (if opened in browser)
  return devFallbackInvoke<T>(command, args);
}

// Local fallback state for standalone Vite browser testing
let fallbackState: HubStateSnapshot = {
  accounts: [
    {
      id: 1,
      label: 'Personal',
      badgeText: 'PRIMARY',
      accentColor: 'cyan',
      aumid: 'Antigravity.Account.1',
      profileDir: 'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Account1',
      shortcutPath:
        'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Shortcuts\\Antigravity - Account 1.lnk',
      iconPath:
        'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Icons\\Account1.ico',
      status: 'running',
      pid: 18420,
      processCount: 11,
      cpuPercent: 3.8,
      memoryMb: 482.4,
      uptimeSeconds: 5412,
      windowTitle: 'Antihub - Antigravity [Account 1: Personal]',
      hwnd: 131942,
      aumidStamped: true,
      authInitialized: true,
      userEmail: 'account1@example.com',
      profileSizeMb: 142.8,
      lastWorkspace: 'C:\\Projects\\Antihub',
    },
    {
      id: 2,
      label: 'Work',
      badgeText: 'ENTERPRISE',
      accentColor: 'violet',
      aumid: 'Antigravity.Account.2',
      profileDir: 'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Account2',
      shortcutPath:
        'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Shortcuts\\Antigravity - Account 2.lnk',
      iconPath:
        'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Icons\\Account2.ico',
      status: 'stopped',
      pid: null,
      processCount: 0,
      cpuPercent: 0.0,
      memoryMb: 0.0,
      uptimeSeconds: 0,
      windowTitle: '',
      hwnd: null,
      aumidStamped: true,
      authInitialized: true,
      userEmail: 'account2@example.com',
      profileSizeMb: 98.5,
      lastWorkspace: 'C:\\Projects\\Antihub',
    },
    {
      id: 3,
      label: 'Secondary',
      badgeText: 'RESEARCH',
      accentColor: 'amber',
      aumid: 'Antigravity.Account.3',
      profileDir: 'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Account3',
      shortcutPath:
        'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Shortcuts\\Antigravity - Account 3.lnk',
      iconPath:
        'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles\\Icons\\Account3.ico',
      status: 'stopped',
      pid: null,
      processCount: 0,
      cpuPercent: 0.0,
      memoryMb: 0.0,
      uptimeSeconds: 0,
      windowTitle: '',
      hwnd: null,
      aumidStamped: true,
      authInitialized: false,
      userEmail: null,
      profileSizeMb: 24.1,
      lastWorkspace: 'C:\\Projects\\Antihub',
    },
  ],
  brain: {
    appDataDir: 'C:\\Users\\Developer\\.gemini\\antigravity',
    dbPath: 'C:\\Users\\Developer\\.gemini\\antigravity\\conversation_summaries.db',
    dbExists: true,
    dbSizeMb: 6.65,
    walModeActive: true,
    walSizeKb: 128,
    isLocked: false,
    lockHolderPid: null,
    conversationCount: 42,
    brainFolderCount: 42,
    lastModifiedIso: new Date().toISOString(),
  },
  config: {
    antigravityExePath:
      'C:\\Users\\Developer\\AppData\\Local\\Programs\\Antigravity\\Antigravity.exe',
    sharedAppDataDir: 'C:\\Users\\Developer\\.gemini\\antigravity',
    profilesRootDir: 'C:\\Users\\Developer\\AppData\\Roaming\\Antigravity-Profiles',
    defaultWorkspaceDir: 'C:\\Projects\\Antihub',
    syncUserSettings: true,
    syncKeybindings: true,
    syncSnippets: true,
    autoStampWindowAumid: true,
    minimizeToTrayOnClose: true,
    pollIntervalMs: 1500,
    accounts: [
      {
        id: 1,
        label: 'Personal',
        badgeText: 'PRIMARY',
        accentColor: 'cyan',
        defaultWorkspace: 'C:\\Projects\\Antihub',
      },
      {
        id: 2,
        label: 'Work',
        badgeText: 'ENTERPRISE',
        accentColor: 'violet',
        defaultWorkspace: 'C:\\Projects\\Antihub',
      },
      {
        id: 3,
        label: 'Secondary',
        badgeText: 'RESEARCH',
        accentColor: 'amber',
        defaultWorkspace: 'C:\\Projects\\Antihub',
      },
    ],
  },
  shortcutsReady: true,
  exeExists: true,
  totalCpuPercent: 3.8,
  totalMemoryMb: 482.4,
  timestamp: new Date().toISOString(),
};

async function devFallbackInvoke<T>(
  command: string,
  args: Record<string, unknown>
): Promise<T> {
  const accountId = Number(args.accountId || 0);
  switch (command) {
    case 'get_hub_state':
      return structuredClone(fallbackState) as unknown as T;
    case 'launch_instance': {
      const acc = fallbackState.accounts.find((a) => a.id === accountId);
      if (acc) {
        acc.status = 'running';
        acc.pid = 20000 + accountId * 111;
        acc.processCount = 9;
        acc.cpuPercent = 2.4;
        acc.memoryMb = 415.0;
        acc.uptimeSeconds = 5;
        acc.windowTitle = `Antihub - Antigravity [Account ${acc.id}: ${acc.label}]`;
      }
      return { ok: true, message: `Launched Account ${accountId}` } as unknown as T;
    }
    case 'terminate_instance': {
      const acc = fallbackState.accounts.find((a) => a.id === accountId);
      if (acc) {
        acc.status = 'stopped';
        acc.pid = null;
        acc.processCount = 0;
        acc.cpuPercent = 0;
        acc.memoryMb = 0;
        acc.uptimeSeconds = 0;
      }
      return { ok: true, message: `Terminated Account ${accountId}` } as unknown as T;
    }
    case 'restart_instance': {
      const acc = fallbackState.accounts.find((a) => a.id === accountId);
      if (acc) {
        acc.status = 'running';
        acc.pid = 24000 + accountId * 137;
        acc.uptimeSeconds = 1;
      }
      return { ok: true, message: `Restarted Account ${accountId}` } as unknown as T;
    }
    case 'launch_all_instances':
      fallbackState.accounts.forEach((acc) => {
        acc.status = 'running';
        acc.pid = acc.pid || 21000 + acc.id * 250;
        acc.processCount = 10;
        acc.cpuPercent = 2.9;
        acc.memoryMb = 440;
      });
      return { ok: true, message: 'Launched all 3 accounts' } as unknown as T;
    case 'terminate_all_instances':
      fallbackState.accounts.forEach((acc) => {
        acc.status = 'stopped';
        acc.pid = null;
        acc.processCount = 0;
        acc.cpuPercent = 0;
        acc.memoryMb = 0;
        acc.uptimeSeconds = 0;
      });
      return { ok: true, message: 'Terminated all running instances' } as unknown as T;
    case 'browse_folder':
    case 'browse_file':
      return { ok: false, path: null } as unknown as T;
    default:
      return { ok: true, message: `Executed ${command}` } as unknown as T;
  }
}

export const hubBridge = {
  getHubState: () => callBackend<HubStateSnapshot>('get_hub_state'),
  launchInstance: (accountId: number, workspacePath?: string) =>
    callBackend<{ ok: boolean; message: string }>('launch_instance', {
      accountId,
      workspacePath,
    }),
  focusInstance: (accountId: number) =>
    callBackend<{ ok: boolean; message: string }>('focus_instance', { accountId }),
  restartInstance: (accountId: number) =>
    callBackend<{ ok: boolean; message: string }>('restart_instance', { accountId }),
  terminateInstance: (accountId: number) =>
    callBackend<{ ok: boolean; message: string }>('terminate_instance', {
      accountId,
    }),
  clearAuthCache: (accountId: number) =>
    callBackend<{ ok: boolean; message: string }>('clear_auth_cache', { accountId }),
  launchAllInstances: () =>
    callBackend<{ ok: boolean; message: string }>('launch_all_instances'),
  terminateAllInstances: () =>
    callBackend<{ ok: boolean; message: string }>('terminate_all_instances'),
  openSharedStorageFolder: () =>
    callBackend<{ ok: boolean; message: string }>('open_shared_storage'),
  openProfileFolder: (accountId: number) =>
    callBackend<{ ok: boolean; message: string }>('open_profile_folder', {
      accountId,
    }),
  regenerateShortcuts: () =>
    callBackend<{ ok: boolean; message: string }>('regenerate_shortcuts'),
  syncWorkspacesAndSettings: () =>
    callBackend<{ ok: boolean; message: string }>('sync_workspaces_and_settings'),
  optimizeSqliteWal: () =>
    callBackend<{ ok: boolean; message: string }>('optimize_sqlite_wal'),
  saveConfig: (config: HubConfig) =>
    callBackend<{ ok: boolean; message: string }>('save_config', { config }),
  browseFolder: (initialPath?: string, title?: string) =>
    callBackend<{ ok: boolean; path: string | null }>('browse_folder', {
      initialPath,
      title,
    }),
  browseFile: (initialPath?: string, title?: string) =>
    callBackend<{ ok: boolean; path: string | null }>('browse_file', {
      initialPath,
      title,
    }),
  minimizeToTray: () =>
    callBackend<{ ok: boolean; message: string }>('minimize_to_tray'),
  exitHub: () => callBackend<{ ok: boolean; message: string }>('exit_hub'),
};
