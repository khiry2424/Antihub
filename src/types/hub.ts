export type AccountStatus = 'running' | 'stopped' | 'starting' | 'stopping';

export interface AccountInstance {
  id: number; // 1 | 2 | 3
  label: string; // e.g., "Personal", "Work", "Secondary"
  badgeText: string; // e.g., "PRIMARY", "ENTERPRISE", "SANDBOX"
  accentColor: 'cyan' | 'violet' | 'amber';
  aumid: string; // "Antigravity.Account.1" | "Antigravity.Account.2" | "Antigravity.Account.3"
  profileDir: string; // "%APPDATA%\\Antigravity-Profiles\\Account1"
  shortcutPath: string; // "%APPDATA%\\Antigravity-Profiles\\Shortcuts\\Antigravity - Account 1.lnk"
  iconPath: string;
  status: AccountStatus;
  pid: number | null;
  processCount: number;
  cpuPercent: number;
  memoryMb: number;
  uptimeSeconds: number;
  windowTitle: string;
  hwnd: number | null;
  aumidStamped: boolean;
  authInitialized: boolean;
  userEmail: string | null;
  profileSizeMb: number;
  lastWorkspace: string;
}

export interface SharedBrainTelemetry {
  appDataDir: string; // "%USERPROFILE%\\.gemini\\antigravity"
  dbPath: string; // "%USERPROFILE%\\.gemini\\antigravity\\conversation_summaries.db"
  dbExists: boolean;
  dbSizeMb: number;
  walModeActive: boolean;
  walSizeKb: number;
  isLocked: boolean;
  lockHolderPid: number | null;
  conversationCount: number;
  brainFolderCount: number;
  lastModifiedIso: string;
}

export interface HubConfig {
  antigravityExePath: string;
  sharedAppDataDir: string;
  profilesRootDir: string;
  defaultWorkspaceDir: string;
  syncUserSettings: boolean;
  syncKeybindings: boolean;
  syncSnippets: boolean;
  autoStampWindowAumid: boolean;
  minimizeToTrayOnClose: boolean;
  pollIntervalMs: number;
  accounts: {
    id: number;
    label: string;
    badgeText: string;
    accentColor: 'cyan' | 'violet' | 'amber';
    defaultWorkspace: string;
  }[];
}

export interface HubEventLog {
  id: string;
  timestamp: string;
  level: 'info' | 'success' | 'warn' | 'error';
  source: string;
  message: string;
}

export interface HubStateSnapshot {
  accounts: AccountInstance[];
  brain: SharedBrainTelemetry;
  config: HubConfig;
  shortcutsReady: boolean;
  exeExists: boolean;
  totalCpuPercent: number;
  totalMemoryMb: number;
  timestamp: string;
}
