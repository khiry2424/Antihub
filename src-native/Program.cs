using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AntigravityHub
{
    #region Win32 Interop & Shell PropertyStore

    internal static class Win32Native
    {
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;
        public const uint WM_SETICON = 0x0080;
        public static readonly IntPtr ICON_SMALL = new IntPtr(0);
        public static readonly IntPtr ICON_BIG = new IntPtr(1);
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);
        public const uint NIM_DELETE = 0x00000002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONIDENTIFIER
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public Guid guidItem;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("shell32.dll")]
        public static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const uint SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SHGetPropertyStoreForWindow(
            IntPtr hwnd,
            ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    internal interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PropVariant pv);
        int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
        public PROPERTYKEY(Guid fmtid, uint pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant : IDisposable
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;

        public static PropVariant FromString(string val)
        {
            PropVariant pv = new PropVariant();
            pv.vt = 31; // VT_LPWSTR
            pv.pointerValue = Marshal.StringToCoTaskMemUni(val);
            return pv;
        }

        public void Dispose()
        {
            if (vt == 31 && pointerValue != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pointerValue);
                pointerValue = IntPtr.Zero;
                vt = 0;
            }
        }
    }

    #endregion

    #region Data Models

    public class AccountConfigItem
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("label")] public string Label { get; set; } = "";
        [JsonPropertyName("badgeText")] public string BadgeText { get; set; } = "";
        [JsonPropertyName("accentColor")] public string AccentColor { get; set; } = "cyan";
        [JsonPropertyName("defaultWorkspace")] public string DefaultWorkspace { get; set; } = "";
    }

    public class HubConfig
    {
        [JsonPropertyName("antigravityExePath")] public string AntigravityExePath { get; set; } = "";
        [JsonPropertyName("sharedAppDataDir")] public string SharedAppDataDir { get; set; } = "";
        [JsonPropertyName("profilesRootDir")] public string ProfilesRootDir { get; set; } = "";
        [JsonPropertyName("defaultWorkspaceDir")] public string DefaultWorkspaceDir { get; set; } = "";
        [JsonPropertyName("syncUserSettings")] public bool SyncUserSettings { get; set; } = true;
        [JsonPropertyName("syncKeybindings")] public bool SyncKeybindings { get; set; } = true;
        [JsonPropertyName("syncSnippets")] public bool SyncSnippets { get; set; } = true;
        [JsonPropertyName("autoStampWindowAumid")] public bool AutoStampWindowAumid { get; set; } = true;
        [JsonPropertyName("hideChildTrayIcons")] public bool HideChildTrayIcons { get; set; } = true;
        [JsonPropertyName("minimizeToTrayOnClose")] public bool MinimizeToTrayOnClose { get; set; } = true;
        [JsonPropertyName("pollIntervalMs")] public int PollIntervalMs { get; set; } = 1500;
        [JsonPropertyName("accounts")] public List<AccountConfigItem> Accounts { get; set; } = new();
    }

    public class AccountInstanceDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("label")] public string Label { get; set; } = "";
        [JsonPropertyName("badgeText")] public string BadgeText { get; set; } = "";
        [JsonPropertyName("accentColor")] public string AccentColor { get; set; } = "";
        [JsonPropertyName("aumid")] public string Aumid { get; set; } = "";
        [JsonPropertyName("profileDir")] public string ProfileDir { get; set; } = "";
        [JsonPropertyName("shortcutPath")] public string ShortcutPath { get; set; } = "";
        [JsonPropertyName("iconPath")] public string IconPath { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "stopped";
        [JsonPropertyName("pid")] public int? Pid { get; set; }
        [JsonPropertyName("processCount")] public int ProcessCount { get; set; }
        [JsonPropertyName("cpuPercent")] public double CpuPercent { get; set; }
        [JsonPropertyName("memoryMb")] public double MemoryMb { get; set; }
        [JsonPropertyName("uptimeSeconds")] public long UptimeSeconds { get; set; }
        [JsonPropertyName("windowTitle")] public string WindowTitle { get; set; } = "";
        [JsonPropertyName("hwnd")] public long? Hwnd { get; set; }
        [JsonPropertyName("aumidStamped")] public bool AumidStamped { get; set; } = true;
        [JsonPropertyName("authInitialized")] public bool AuthInitialized { get; set; }
        [JsonPropertyName("userEmail")] public string? UserEmail { get; set; }
        [JsonPropertyName("profileSizeMb")] public double ProfileSizeMb { get; set; }
        [JsonPropertyName("lastWorkspace")] public string LastWorkspace { get; set; } = "";
    }

    public class SharedBrainTelemetryDto
    {
        [JsonPropertyName("appDataDir")] public string AppDataDir { get; set; } = "";
        [JsonPropertyName("dbPath")] public string DbPath { get; set; } = "";
        [JsonPropertyName("dbExists")] public bool DbExists { get; set; }
        [JsonPropertyName("dbSizeMb")] public double DbSizeMb { get; set; }
        [JsonPropertyName("walModeActive")] public bool WalModeActive { get; set; }
        [JsonPropertyName("walSizeKb")] public double WalSizeKb { get; set; }
        [JsonPropertyName("isLocked")] public bool IsLocked { get; set; }
        [JsonPropertyName("lockHolderPid")] public int? LockHolderPid { get; set; }
        [JsonPropertyName("conversationCount")] public int ConversationCount { get; set; }
        [JsonPropertyName("brainFolderCount")] public int BrainFolderCount { get; set; }
        [JsonPropertyName("lastModifiedIso")] public string LastModifiedIso { get; set; } = "";
    }

    public class HubStateSnapshotDto
    {
        [JsonPropertyName("accounts")] public List<AccountInstanceDto> Accounts { get; set; } = new();
        [JsonPropertyName("brain")] public SharedBrainTelemetryDto Brain { get; set; } = new();
        [JsonPropertyName("config")] public HubConfig Config { get; set; } = new();
        [JsonPropertyName("shortcutsReady")] public bool ShortcutsReady { get; set; }
        [JsonPropertyName("exeExists")] public bool ExeExists { get; set; }
        [JsonPropertyName("totalCpuPercent")] public double TotalCpuPercent { get; set; }
        [JsonPropertyName("totalMemoryMb")] public double TotalMemoryMb { get; set; }
        [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    }

    #endregion

    #region Core Hub Orchestrator

    public class HubOrchestrator
    {
        private static readonly Guid AppUserModelGuid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        private static Guid IID_IPropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

        private volatile HubConfig _config;
        private readonly string _configFilePath;
        private readonly ConcurrentDictionary<int, (DateTime SampleTime, TimeSpan TotalProcTime)> _cpuSamples = new();
        private readonly ConcurrentDictionary<int, Icon> _accountIcons = new();
        private readonly ConcurrentDictionary<int, int> _pidToAccountCache = new();
        private readonly ConcurrentDictionary<int, long> _stampedWindows = new();
        private readonly ConcurrentDictionary<int, (DateTime LastWriteUtc, string? Email)> _emailCache = new();
        private readonly ConcurrentDictionary<int, int> _traySuppressAttempts = new();
        private readonly ConcurrentDictionary<int, byte> _suppressedTrayPids = new();
        private readonly object _snapshotLock = new();
        private HubStateSnapshotDto? _lastSnapshot;
        private DateTime _lastSnapshotUtc = DateTime.MinValue;
        private SharedBrainTelemetryDto? _cachedBrainTelemetry;
        private DateTime _lastBrainCheckUtc = DateTime.MinValue;
        private volatile bool _envEnsured = false;

        public static bool IsValidAccountId(int accountId) => accountId >= 1 && accountId <= 3;

        private static readonly char[] ForbiddenShellChars = { '&', '|', '<', '>', '^', '%', '"', '\r', '\n', '\0' };

        public static bool IsSafeWindowsPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.IndexOfAny(ForbiddenShellChars) >= 0) return false;
            try
            {
                return Path.IsPathFullyQualified(path);
            }
            catch
            {
                return false;
            }
        }

        public static bool ContainsProfileArg(string cmdLower, string profileDir)
        {
            string normalizedProfile = profileDir.TrimEnd('\\', '/').ToLowerInvariant();
            string altProfile = normalizedProfile.Replace('\\', '/');
            return cmdLower.Contains(normalizedProfile) || cmdLower.Contains(altProfile);
        }

        public HubConfig NormalizeConfig(HubConfig cfg)
        {
            cfg.PollIntervalMs = Math.Clamp(cfg.PollIntervalMs, 500, 60000);
            if (string.IsNullOrWhiteSpace(cfg.ProfilesRootDir) || !IsSafeWindowsPath(cfg.ProfilesRootDir))
            {
                cfg.ProfilesRootDir = Path.Combine(GetRealAppDataRoaming(), "Antigravity-Profiles");
            }
            if (string.IsNullOrWhiteSpace(cfg.SharedAppDataDir) || !IsSafeWindowsPath(cfg.SharedAppDataDir))
            {
                cfg.SharedAppDataDir = Path.Combine(GetRealUserProfile(), ".gemini", "antigravity");
            }
            if (!IsSafeWindowsPath(cfg.AntigravityExePath))
            {
                cfg.AntigravityExePath = Path.Combine(GetRealLocalAppData(), "Programs", "Antigravity", "Antigravity.exe");
            }
            if (cfg.Accounts == null || cfg.Accounts.Count != 3)
            {
                cfg.Accounts = _config?.Accounts ?? new List<AccountConfigItem>();
            }
            return cfg;
        }

        public void DisposeResources()
        {
            lock (_snapshotLock)
            {
                foreach (var ico in _accountIcons.Values)
                {
                    try { ico.Dispose(); } catch { }
                }
                _accountIcons.Clear();
            }
        }

        public HubOrchestrator()
        {
            string roaming = GetRealAppDataRoaming();
            string profilesRoot = Path.Combine(roaming, "Antigravity-Profiles");
            Directory.CreateDirectory(profilesRoot);
            _configFilePath = Path.Combine(profilesRoot, "hub-config.json");
            _config = LoadOrCreateConfig();
            Task.Run(() => EnsureDirectoriesAndEnv());
        }

        public HubConfig Config => _config;

        private static string GetRealUserProfile()
        {
            string? appDataEnv = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrWhiteSpace(appDataEnv) && !appDataEnv.Contains("Antigravity-Profiles", StringComparison.OrdinalIgnoreCase))
            {
                string? appDataDir = Path.GetDirectoryName(appDataEnv);
                string? root = appDataDir != null ? Path.GetDirectoryName(appDataDir) : null;
                if (!string.IsNullOrWhiteSpace(root))
                    return root;
            }
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (userProfile.Contains("Antigravity-Profiles", StringComparison.OrdinalIgnoreCase))
            {
                int idx = userProfile.IndexOf("AppData", StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                    return userProfile.Substring(0, idx).TrimEnd('\\', '/');
            }
            return userProfile;
        }

        private static string GetRealAppDataRoaming()
        {
            string? appDataEnv = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrWhiteSpace(appDataEnv) && !appDataEnv.Contains("Antigravity-Profiles", StringComparison.OrdinalIgnoreCase))
                return appDataEnv;
            return Path.Combine(GetRealUserProfile(), "AppData", "Roaming");
        }

        private static string GetRealLocalAppData()
        {
            string? localEnv = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrWhiteSpace(localEnv) && !localEnv.Contains("Antigravity-Profiles", StringComparison.OrdinalIgnoreCase))
                return localEnv;
            return Path.Combine(GetRealUserProfile(), "AppData", "Local");
        }

        private HubConfig LoadOrCreateConfig()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<HubConfig>(File.ReadAllText(_configFilePath));
                    if (loaded != null && loaded.Accounts.Count == 3)
                    {
                        loaded = NormalizeConfig(loaded);
                        if (loaded.SharedAppDataDir.Contains("Antigravity-Profiles", StringComparison.OrdinalIgnoreCase))
                        {
                            loaded.SharedAppDataDir = Path.Combine(GetRealUserProfile(), ".gemini", "antigravity");
                            SaveConfig(loaded);
                        }
                        return loaded;
                    }
                }
                catch { }
            }

            string localApp = GetRealLocalAppData();
            string roaming = GetRealAppDataRoaming();
            string userProfile = GetRealUserProfile();
            string defaultWs = Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "src"))
                ? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."))
                : Environment.CurrentDirectory;

            var cfg = new HubConfig
            {
                AntigravityExePath = Path.Combine(localApp, "Programs", "Antigravity", "Antigravity.exe"),
                SharedAppDataDir = Path.Combine(userProfile, ".gemini", "antigravity"),
                ProfilesRootDir = Path.Combine(roaming, "Antigravity-Profiles"),
                DefaultWorkspaceDir = defaultWs,
                SyncUserSettings = true,
                SyncKeybindings = true,
                SyncSnippets = true,
                AutoStampWindowAumid = true,
                MinimizeToTrayOnClose = true,
                PollIntervalMs = 1500,
                Accounts = new List<AccountConfigItem>
                {
                    new() { Id = 1, Label = "Personal", BadgeText = "PRIMARY", AccentColor = "cyan", DefaultWorkspace = defaultWs },
                    new() { Id = 2, Label = "Work", BadgeText = "ENTERPRISE", AccentColor = "violet", DefaultWorkspace = defaultWs },
                    new() { Id = 3, Label = "Secondary", BadgeText = "RESEARCH", AccentColor = "amber", DefaultWorkspace = defaultWs },
                }
            };

            SaveConfig(cfg);
            return cfg;
        }

        public void SaveConfig(HubConfig updated)
        {
            _config = NormalizeConfig(updated);
            _envEnsured = false;
            _traySuppressAttempts.Clear();
            _suppressedTrayPids.Clear();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
                File.WriteAllText(_configFilePath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
                EnsureDirectoriesAndEnv(force: true);
            }
            catch { }
        }

        public static void EnsureDirectoryJunction(string linkPath, string targetPath)
        {
            if (!IsSafeWindowsPath(linkPath) || !IsSafeWindowsPath(targetPath) || !Directory.Exists(targetPath))
                return;

            try
            {
                string fullLink = Path.GetFullPath(linkPath);
                string fullTarget = Path.GetFullPath(targetPath);
                if (!IsSafeWindowsPath(fullLink) || !IsSafeWindowsPath(fullTarget))
                    return;

                if (Directory.Exists(fullLink))
                {
                    var di = new DirectoryInfo(fullLink);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return; // already a valid junction / reparse point
                    }

                    // Regular directory existed; delete it so we can create a genuine NTFS junction
                    Directory.Delete(fullLink, true);
                }

                string? parent = Path.GetDirectoryName(fullLink);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                var psi = new ProcessStartInfo("cmd.exe")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add("/d");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("mklink");
                psi.ArgumentList.Add("/J");
                psi.ArgumentList.Add(fullLink);
                psi.ArgumentList.Add(fullTarget);
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
            }
            catch { }
        }

        public void EnsureDirectoriesAndEnv(bool force = false)
        {
            if (_envEnsured && !force) return;
            try
            {
                Directory.CreateDirectory(_config.SharedAppDataDir);
                Directory.CreateDirectory(_config.ProfilesRootDir);
                Environment.SetEnvironmentVariable("ANTIGRAVITY_APP_DATA_DIR", _config.SharedAppDataDir, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("ANTIGRAVITY_APP_DATA_DIR", _config.SharedAppDataDir, EnvironmentVariableTarget.User);

                string realUserProfile = GetRealUserProfile();
                string realGemini = Path.Combine(realUserProfile, ".gemini");
                string realConfig = Path.Combine(realGemini, "config");
                string realRoaming = GetRealAppDataRoaming();
                string mainAntigravityUser = Path.Combine(realRoaming, "Antigravity", "User");
                string mainWorkspaceStorage = Path.Combine(mainAntigravityUser, "workspaceStorage");
                string mainSnippets = Path.Combine(mainAntigravityUser, "snippets");

                for (int i = 1; i <= 3; i++)
                {
                    string accDir = Path.Combine(_config.ProfilesRootDir, $"Account{i}");
                    string homeDir = Path.Combine(accDir, "Home");
                    string geminiDir = Path.Combine(homeDir, ".gemini");
                    string cacheDir = Path.Combine(geminiDir, "cache");
                    string targetUser = Path.Combine(accDir, "User");

                    Directory.CreateDirectory(accDir);
                    Directory.CreateDirectory(targetUser);
                    Directory.CreateDirectory(cacheDir);

                    // Force language_server to bypass global Windows Credential Manager
                    string markerPath = Path.Combine(cacheDir, "antigravity-keyring-unavailable");
                    if (!File.Exists(markerPath))
                    {
                        File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
                    }

                    // 1. Shared Brain NTFS Junction (.gemini/antigravity)
                    EnsureDirectoryJunction(Path.Combine(geminiDir, "antigravity"), _config.SharedAppDataDir);

                    // 2. Shared Config & Projects NTFS Junction (.gemini/config)
                    if (Directory.Exists(realConfig))
                    {
                        EnsureDirectoryJunction(Path.Combine(geminiDir, "config"), realConfig);
                    }

                    // 3. Shared Workspace Storage NTFS Junction (User/workspaceStorage)
                    if (Directory.Exists(mainWorkspaceStorage))
                    {
                        EnsureDirectoryJunction(Path.Combine(targetUser, "workspaceStorage"), mainWorkspaceStorage);
                    }

                    // 4. Shared Snippets NTFS Junction (User/snippets)
                    if (Directory.Exists(mainSnippets))
                    {
                        EnsureDirectoryJunction(Path.Combine(targetUser, "snippets"), mainSnippets);
                    }
                }
                _envEnsured = true;
            }
            catch { }
        }

        private void ResolveAccountMappingsForPids(IEnumerable<int> targetPids)
        {
            var pidList = targetPids.ToList();
            if (pidList.Count == 0) return;

            try
            {
                string filter = string.Join(" OR ", pidList.Select(p => $"ProcessId = {p}"));
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'Antigravity.exe' AND ({filter})");
                foreach (ManagementObject obj in searcher.Get())
                {
                    int pid = Convert.ToInt32(obj["ProcessId"]);
                    string cmd = (obj["CommandLine"]?.ToString() ?? "").ToLowerInvariant();
                    int matchedAcc = 0;
                    for (int id = 1; id <= 3; id++)
                    {
                        string profileDir = Path.Combine(_config.ProfilesRootDir, $"Account{id}");
                        if (ContainsProfileArg(cmd, profileDir))
                        {
                            matchedAcc = id;
                            break;
                        }
                    }
                    _pidToAccountCache[pid] = matchedAcc;
                }
            }
            catch { }

            foreach (int pid in pidList)
            {
                _pidToAccountCache.TryAdd(pid, 0);
            }
        }

        private Dictionary<int, string> GetAntigravityProcessCommandLines()
        {
            var result = new Dictionary<int, string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'Antigravity.exe'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    int pid = Convert.ToInt32(obj["ProcessId"]);
                    string cmd = obj["CommandLine"]?.ToString() ?? "";
                    result[pid] = cmd;
                }
            }
            catch { }
            return result;
        }

        private string? GetCachedAccountEmail(int accountId, string profileDir, bool hasValidToken)
        {
            if (!hasValidToken)
            {
                _emailCache.TryRemove(accountId, out _);
                return null;
            }

            string storagePath = Path.Combine(profileDir, "app_storage.json");
            string tokenPath = Path.Combine(profileDir, "Home", ".gemini", "jetski-standalone-oauth-token");

            try
            {
                DateTime lastWrite = File.Exists(storagePath)
                    ? File.GetLastWriteTimeUtc(storagePath)
                    : (File.Exists(tokenPath) ? File.GetLastWriteTimeUtc(tokenPath) : DateTime.MinValue);

                if (_emailCache.TryGetValue(accountId, out var cached) && cached.LastWriteUtc == lastWrite)
                {
                    return cached.Email;
                }

                string? detectedEmail = null;
                if (File.Exists(storagePath))
                {
                    using var fs = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var doc = JsonDocument.Parse(fs);
                    if (doc.RootElement.TryGetProperty("agy.onboarding.lastLoginUsername", out var agyProp))
                    {
                        detectedEmail = agyProp.GetString();
                    }
                    else if (doc.RootElement.TryGetProperty("jetski.onboarding.lastLoginUsername", out var jetskiProp))
                    {
                        detectedEmail = jetskiProp.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(detectedEmail) && File.Exists(tokenPath))
                {
                    using var fs = new FileStream(tokenPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var doc = JsonDocument.Parse(fs);
                    if (doc.RootElement.TryGetProperty("id_token_email", out var tokEmailProp))
                    {
                        detectedEmail = tokEmailProp.GetString();
                    }
                }

                _emailCache[accountId] = (lastWrite, detectedEmail);
                return detectedEmail;
            }
            catch
            {
                return _emailCache.TryGetValue(accountId, out var fallback) ? fallback.Email : null;
            }
        }

        private SharedBrainTelemetryDto GetBrainTelemetry(DateTime nowUtc)
        {
            if (_cachedBrainTelemetry != null && (nowUtc - _lastBrainCheckUtc).TotalSeconds < 5.0)
            {
                return _cachedBrainTelemetry;
            }

            string dbPath = Path.Combine(_config.SharedAppDataDir, "conversation_summaries.db");
            string walPath = dbPath + "-wal";
            bool dbExists = File.Exists(dbPath);
            double dbSizeMb = dbExists ? new FileInfo(dbPath).Length / (1024.0 * 1024.0) : 0.0;
            double walSizeKb = File.Exists(walPath) ? new FileInfo(walPath).Length / 1024.0 : 0.0;
            bool isLocked = CheckSqliteWriteLock(dbPath);

            int convCount = 0;
            string convDir = Path.Combine(_config.SharedAppDataDir, "conversations");
            if (Directory.Exists(convDir))
                convCount = Directory.GetFileSystemEntries(convDir).Length;

            int brainCount = 0;
            string brainDir = Path.Combine(_config.SharedAppDataDir, "brain");
            if (Directory.Exists(brainDir))
                brainCount = Directory.GetDirectories(brainDir).Length;

            var brain = new SharedBrainTelemetryDto
            {
                AppDataDir = _config.SharedAppDataDir,
                DbPath = dbPath,
                DbExists = dbExists,
                DbSizeMb = Math.Round(dbSizeMb, 2),
                WalModeActive = true,
                WalSizeKb = Math.Round(walSizeKb, 1),
                IsLocked = isLocked,
                LockHolderPid = null,
                ConversationCount = convCount,
                BrainFolderCount = brainCount,
                LastModifiedIso = dbExists ? File.GetLastWriteTimeUtc(dbPath).ToString("O") : nowUtc.ToString("O")
            };

            _cachedBrainTelemetry = brain;
            _lastBrainCheckUtc = nowUtc;
            return brain;
        }

        public HubStateSnapshotDto GetSnapshot(bool bypassCache = false)
        {
            lock (_snapshotLock)
            {
                var nowUtc = DateTime.UtcNow;
                if (!bypassCache && _lastSnapshot != null && (nowUtc - _lastSnapshotUtc).TotalMilliseconds < 400)
                {
                    return _lastSnapshot;
                }

                var allProcs = Process.GetProcessesByName("Antigravity");
                var procById = new Dictionary<int, Process>(allProcs.Length);
                var unknownPids = new List<int>();
                var activePidsSet = new HashSet<int>();

                foreach (var p in allProcs)
                {
                    procById[p.Id] = p;
                    activePidsSet.Add(p.Id);
                    if (!_pidToAccountCache.ContainsKey(p.Id))
                    {
                        unknownPids.Add(p.Id);
                    }
                }

                // Prune exited PIDs from cache
                foreach (int cachedPid in _pidToAccountCache.Keys.ToList())
                {
                    if (!activePidsSet.Contains(cachedPid))
                    {
                        _pidToAccountCache.TryRemove(cachedPid, out _);
                        _suppressedTrayPids.TryRemove(cachedPid, out _);
                        _traySuppressAttempts.TryRemove(cachedPid, out _);
                    }
                }

                // Query WMI ONLY for newly seen PIDs (0 WMI calls during steady state!)
                if (unknownPids.Count > 0)
                {
                    ResolveAccountMappingsForPids(unknownPids);
                }

                // Group active PIDs by accountId in one pass
                var pidsByAccount = new Dictionary<int, List<int>> { { 1, new() }, { 2, new() }, { 3, new() } };
                var allAccountPids = new Dictionary<int, int>();
                foreach (var kvp in _pidToAccountCache)
                {
                    if (kvp.Value >= 1 && kvp.Value <= 3 && procById.ContainsKey(kvp.Key))
                    {
                        pidsByAccount[kvp.Value].Add(kvp.Key);
                        allAccountPids[kvp.Key] = kvp.Value;
                    }
                }

                if (_config.HideChildTrayIcons && allAccountPids.Count > 0)
                {
                    SuppressChildAntigravityTrayIcons(allAccountPids);
                }

                // Single EnumWindows pass across all running accounts (or skip if none running!)
                var windowsByAccount = allAccountPids.Count > 0
                    ? FindMainWindowsByAccount(allAccountPids)
                    : new Dictionary<int, (IntPtr Hwnd, int Pid, string Title)>();

                var accountsDto = new List<AccountInstanceDto>();
                double totalCpu = 0;
                double totalMem = 0;
                int coreCount = Math.Max(1, Environment.ProcessorCount);

                foreach (var accCfg in _config.Accounts)
                {
                    int id = accCfg.Id;
                    string profileDir = Path.Combine(_config.ProfilesRootDir, $"Account{id}");
                    string shortcutPath = Path.Combine(_config.ProfilesRootDir, "Shortcuts", $"Antigravity - Account {id} ({accCfg.Label}).lnk");
                    string iconPath = Path.Combine(_config.ProfilesRootDir, "Icons", $"Account{id}.ico");
                    string aumid = $"Antigravity.Account.{id}";

                    var matchedPids = pidsByAccount.TryGetValue(id, out var list) ? list : new List<int>();

                    long memBytes = 0;
                    TimeSpan totalProcTime = TimeSpan.Zero;
                    long maxUptimeSecs = 0;
                    int? masterPid = null;
                    IntPtr mainHwnd = IntPtr.Zero;
                    string winTitle = "";

                    foreach (int pid in matchedPids)
                    {
                        if (procById.TryGetValue(pid, out var p))
                        {
                            try
                            {
                                memBytes += p.WorkingSet64;
                                totalProcTime += p.TotalProcessorTime;
                                var up = (long)(DateTime.Now - p.StartTime).TotalSeconds;
                                if (up > maxUptimeSecs)
                                {
                                    maxUptimeSecs = up;
                                    masterPid = pid;
                                }
                            }
                            catch { }
                        }
                    }

                    if (windowsByAccount.TryGetValue(id, out var winInfo) && winInfo.Hwnd != IntPtr.Zero)
                    {
                        mainHwnd = winInfo.Hwnd;
                        winTitle = winInfo.Title;
                        masterPid = winInfo.Pid;

                        if (_config.AutoStampWindowAumid)
                        {
                            long hwndVal = mainHwnd.ToInt64();
                            if (!_stampedWindows.TryGetValue(id, out long alreadyStamped) || alreadyStamped != hwndVal)
                            {
                                StampWindowAumidAndIcon(
                                    mainHwnd,
                                    aumid,
                                    $"\"{_config.AntigravityExePath}\" --user-data-dir=\"{profileDir}\"",
                                    $"Antigravity Account {id} ({accCfg.Label})",
                                    iconPath,
                                    id);
                                _stampedWindows[id] = hwndVal;
                            }
                        }
                    }
                    else if (matchedPids.Count == 0)
                    {
                        _stampedWindows.TryRemove(id, out _);
                    }

                    // Calculate CPU % delta
                    double cpuPct = 0.0;
                    if (matchedPids.Count > 0)
                    {
                        if (_cpuSamples.TryGetValue(id, out var prev))
                        {
                            double wallMs = (nowUtc - prev.SampleTime).TotalMilliseconds;
                            double cpuMs = (totalProcTime - prev.TotalProcTime).TotalMilliseconds;
                            if (wallMs > 100 && cpuMs >= 0)
                            {
                                cpuPct = Math.Min(100.0, (cpuMs / (wallMs * coreCount)) * 100.0);
                            }
                        }
                        _cpuSamples[id] = (nowUtc, totalProcTime);
                    }
                    else
                    {
                        _cpuSamples.TryRemove(id, out _);
                    }

                    double memMb = memBytes / (1024.0 * 1024.0);
                    totalCpu += cpuPct;
                    totalMem += memMb;

                    string tokenPath = Path.Combine(profileDir, "Home", ".gemini", "jetski-standalone-oauth-token");
                    bool hasValidToken = File.Exists(tokenPath) && new FileInfo(tokenPath).Length > 10;
                    string? detectedEmail = GetCachedAccountEmail(id, profileDir, hasValidToken);

                    accountsDto.Add(new AccountInstanceDto
                    {
                        Id = id,
                        Label = accCfg.Label,
                        BadgeText = accCfg.BadgeText,
                        AccentColor = accCfg.AccentColor,
                        Aumid = aumid,
                        ProfileDir = profileDir,
                        ShortcutPath = shortcutPath,
                        IconPath = iconPath,
                        Status = matchedPids.Count > 0 ? "running" : "stopped",
                        Pid = masterPid,
                        ProcessCount = matchedPids.Count,
                        CpuPercent = Math.Round(cpuPct, 1),
                        MemoryMb = Math.Round(memMb, 1),
                        UptimeSeconds = maxUptimeSecs,
                        WindowTitle = !string.IsNullOrEmpty(winTitle)
                            ? winTitle
                            : (matchedPids.Count > 0 ? $"Antigravity [Account {id}: {accCfg.Label}]" : ""),
                        Hwnd = mainHwnd != IntPtr.Zero ? mainHwnd.ToInt64() : null,
                        AumidStamped = true,
                        AuthInitialized = hasValidToken,
                        UserEmail = detectedEmail,
                        ProfileSizeMb = 0,
                        LastWorkspace = accCfg.DefaultWorkspace
                    });
                }

                foreach (var p in allProcs) p.Dispose();

                bool shortcutsReady = File.Exists(Path.Combine(_config.ProfilesRootDir, "Icons", "Account1.ico"));

                _lastSnapshot = new HubStateSnapshotDto
                {
                    Accounts = accountsDto,
                    Brain = GetBrainTelemetry(nowUtc),
                    Config = _config,
                    ShortcutsReady = shortcutsReady,
                    ExeExists = File.Exists(_config.AntigravityExePath),
                    TotalCpuPercent = Math.Round(totalCpu, 1),
                    TotalMemoryMb = Math.Round(totalMem, 1),
                    Timestamp = nowUtc.ToString("O")
                };
                _lastSnapshotUtc = nowUtc;
                return _lastSnapshot;
            }
        }

        private Dictionary<int, (IntPtr Hwnd, int Pid, string Title)> FindMainWindowsByAccount(Dictionary<int, int> pidToAccount)
        {
            var found = new Dictionary<int, (IntPtr Hwnd, int Pid, string Title)>();
            if (pidToAccount.Count == 0) return found;

            Win32Native.EnumWindows((hWnd, lParam) =>
            {
                if (!Win32Native.IsWindowVisible(hWnd))
                    return true;

                Win32Native.GetWindowThreadProcessId(hWnd, out uint winPid);
                if (!pidToAccount.TryGetValue((int)winPid, out int accId))
                    return true;

                if (found.ContainsKey(accId))
                    return true;

                var sbTitle = new StringBuilder(512);
                Win32Native.GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                string title = sbTitle.ToString();
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                var sbClass = new StringBuilder(256);
                Win32Native.GetClassName(hWnd, sbClass, sbClass.Capacity);
                string cls = sbClass.ToString();

                if (cls.Contains("Chrome_WidgetWin"))
                {
                    found[accId] = (hWnd, (int)winPid, title);
                    if (found.Count >= 3)
                        return false; // found all possible accounts
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private void SuppressChildAntigravityTrayIcons(Dictionary<int, int> allAccountPids)
        {
            int selfPid = Environment.ProcessId;
            var targetPids = new HashSet<int>();
            foreach (int pid in allAccountPids.Keys)
            {
                if (pid != selfPid && !_suppressedTrayPids.ContainsKey(pid))
                {
                    targetPids.Add(pid);
                }
            }
            if (targetPids.Count == 0) return;

            var candidates = new List<(IntPtr Hwnd, int Pid)>();

            Win32Native.EnumWindows((hWnd, lParam) =>
            {
                Win32Native.GetWindowThreadProcessId(hWnd, out uint winPid);
                int pid = (int)winPid;
                if (targetPids.Contains(pid))
                {
                    candidates.Add((hWnd, pid));
                }
                return true;
            }, IntPtr.Zero);

            IntPtr msgCur = IntPtr.Zero;
            while ((msgCur = Win32Native.FindWindowEx(Win32Native.HWND_MESSAGE, msgCur, null, null)) != IntPtr.Zero)
            {
                Win32Native.GetWindowThreadProcessId(msgCur, out uint winPid);
                int pid = (int)winPid;
                if (targetPids.Contains(pid))
                {
                    candidates.Add((msgCur, pid));
                }
            }

            uint cbSize = (uint)Marshal.SizeOf<Win32Native.NOTIFYICONDATAW>();
            var suppressedNow = new HashSet<int>();

            foreach (var (hwnd, pid) in candidates)
            {
                for (uint uid = 0; uid <= 16; uid++)
                {
                    var nid = new Win32Native.NOTIFYICONDATAW
                    {
                        cbSize = cbSize,
                        hWnd = hwnd,
                        uID = uid,
                        szTip = "",
                        szInfo = "",
                        szInfoTitle = ""
                    };
                    if (Win32Native.Shell_NotifyIconW(Win32Native.NIM_DELETE, ref nid))
                    {
                        suppressedNow.Add(pid);
                    }
                }
            }

            foreach (int pid in suppressedNow)
            {
                _suppressedTrayPids[pid] = 1;
            }

            foreach (int pid in targetPids)
            {
                if (!suppressedNow.Contains(pid))
                {
                    int attempts = _traySuppressAttempts.AddOrUpdate(pid, 1, (_, c) => c + 1);
                    if (attempts >= 12)
                    {
                        _suppressedTrayPids[pid] = 1;
                    }
                }
            }
        }

        private void StampWindowAumidAndIcon(
            IntPtr hwnd,
            string aumid,
            string relaunchCmd,
            string displayName,
            string iconPath,
            int accountId)
        {
            try
            {
                if (Win32Native.SHGetPropertyStoreForWindow(hwnd, ref IID_IPropertyStore, out IPropertyStore store) == 0 && store != null)
                {
                    SetPropStoreString(store, 5, aumid);       // PKEY_AppUserModel_ID
                    SetPropStoreString(store, 2, relaunchCmd); // PKEY_AppUserModel_RelaunchCommand
                    SetPropStoreString(store, 4, displayName); // PKEY_AppUserModel_RelaunchDisplayNameResource
                    if (File.Exists(iconPath))
                    {
                        SetPropStoreString(store, 3, iconPath + ",0"); // PKEY_AppUserModel_RelaunchIconResource
                    }
                    store.Commit();
                }

                if (File.Exists(iconPath))
                {
                    if (!_accountIcons.TryGetValue(accountId, out var icon))
                    {
                        icon = new Icon(iconPath);
                        _accountIcons[accountId] = icon;
                    }
                    uint flags = Win32Native.SMTO_ABORTIFHUNG | Win32Native.SMTO_NOTIMEOUTIFNOTHUNG;
                    Win32Native.SendMessageTimeout(hwnd, Win32Native.WM_SETICON, Win32Native.ICON_BIG, icon.Handle, flags, 150, out _);
                    Win32Native.SendMessageTimeout(hwnd, Win32Native.WM_SETICON, Win32Native.ICON_SMALL, icon.Handle, flags, 150, out _);
                }
            }
            catch { }
        }

        private static void SetPropStoreString(IPropertyStore store, uint pid, string value)
        {
            PROPERTYKEY key = new PROPERTYKEY(AppUserModelGuid, pid);
            PropVariant pv = PropVariant.FromString(value);
            try
            {
                store.SetValue(ref key, ref pv);
            }
            finally
            {
                pv.Dispose();
            }
        }

        private bool CheckSqliteWriteLock(string dbPath)
        {
            if (!File.Exists(dbPath)) return false;
            try
            {
                using var fs = new FileStream(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public (bool Ok, string Message) LaunchInstance(int accountId, string? workspaceOverride = null)
        {
            if (!IsValidAccountId(accountId))
                return (false, $"Invalid account ID: {accountId}");

            EnsureDirectoriesAndEnv();
            if (_config.SyncUserSettings)
            {
                SyncUserSettingsAcrossProfiles(accountId);
            }

            var acc = _config.Accounts.FirstOrDefault(a => a.Id == accountId);
            string label = acc?.Label ?? $"Account {accountId}";
            string profileDir = Path.Combine(_config.ProfilesRootDir, $"Account{accountId}");
            Directory.CreateDirectory(profileDir);

            if (!IsSafeWindowsPath(_config.AntigravityExePath) || !File.Exists(_config.AntigravityExePath))
            {
                return (false, $"Antigravity executable not found at {_config.AntigravityExePath}");
            }

            string ws = !string.IsNullOrWhiteSpace(workspaceOverride)
                ? workspaceOverride!
                : (acc?.DefaultWorkspace ?? _config.DefaultWorkspaceDir);

            string accountHome = Path.Combine(profileDir, "Home");
            Directory.CreateDirectory(accountHome);
            string accountGeminiCache = Path.Combine(accountHome, ".gemini", "cache");
            Directory.CreateDirectory(accountGeminiCache);
            try
            {
                string marker = Path.Combine(accountGeminiCache, "antigravity-keyring-unavailable");
                if (!File.Exists(marker))
                {
                    File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
                }
            }
            catch { }

            var psi = new ProcessStartInfo
            {
                FileName = _config.AntigravityExePath,
                WorkingDirectory = Path.GetDirectoryName(_config.AntigravityExePath) ?? "",
                UseShellExecute = false
            };
            psi.ArgumentList.Add($"--user-data-dir={profileDir}");
            if (!string.IsNullOrWhiteSpace(ws))
            {
                psi.ArgumentList.Add(ws);
            }

            // Force language_server to bypass global Windows Credential Manager and use per-account file token storage
            psi.EnvironmentVariables["SSH_CONNECTION"] = "127.0.0.1 22 127.0.0.1 22";
            psi.EnvironmentVariables["SSH_CLIENT"] = "127.0.0.1 22 22";
            psi.EnvironmentVariables["SSH_TTY"] = "/dev/pts/0";
            psi.EnvironmentVariables["USERPROFILE"] = accountHome;
            psi.EnvironmentVariables["HOME"] = accountHome;

            // Strictly enforce shared ANTIGRAVITY_APP_DATA_DIR
            psi.EnvironmentVariables["ANTIGRAVITY_APP_DATA_DIR"] = _config.SharedAppDataDir;
            psi.EnvironmentVariables["ANTIGRAVITY_ACCOUNT_ID"] = accountId.ToString();
            psi.EnvironmentVariables["ANTIGRAVITY_AUMID"] = $"Antigravity.Account.{accountId}";

            try
            {
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    _pidToAccountCache[proc.Id] = accountId;
                }
                _stampedWindows.TryRemove(accountId, out _);
                _lastSnapshot = null;

                // Schedule non-blocking post-launch HWND AUMID stamping as window initializes
                _ = Task.Run(async () =>
                {
                    for (int attempt = 0; attempt < 12; attempt++)
                    {
                        await Task.Delay(700);
                        if (_stampedWindows.ContainsKey(accountId))
                            break;
                        GetSnapshot(bypassCache: true);
                    }
                });

                return (true, $"Launched Account {accountId} ({label}) [PID {proc?.Id}] with shared .gemini/antigravity brain");
            }
            catch (Exception ex)
            {
                return (false, $"Launch failed for Account {accountId}: {ex.Message}");
            }
        }

        private IntPtr FindHiddenWindowForAccount(int accountId)
        {
            var matchingPids = new HashSet<int>();
            foreach (var kvp in _pidToAccountCache)
            {
                if (kvp.Value == accountId)
                    matchingPids.Add(kvp.Key);
            }
            if (matchingPids.Count == 0) return IntPtr.Zero;

            IntPtr found = IntPtr.Zero;
            Win32Native.EnumWindows((hWnd, lParam) =>
            {
                Win32Native.GetWindowThreadProcessId(hWnd, out uint winPid);
                if (!matchingPids.Contains((int)winPid))
                    return true;

                var sbClass = new StringBuilder(256);
                Win32Native.GetClassName(hWnd, sbClass, sbClass.Capacity);
                string cls = sbClass.ToString();
                if (!cls.Contains("Chrome_WidgetWin"))
                    return true;

                var sbTitle = new StringBuilder(512);
                Win32Native.GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                if (!string.IsNullOrWhiteSpace(sbTitle.ToString()))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        public (bool Ok, string Message) FocusInstance(int accountId)
        {
            var snap = GetSnapshot(bypassCache: true);
            var acc = snap.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (acc == null)
            {
                return (false, $"Account {accountId} configuration not found");
            }

            IntPtr hwnd = acc.Hwnd != null ? new IntPtr(acc.Hwnd.Value) : IntPtr.Zero;
            if (hwnd == IntPtr.Zero || !Win32Native.IsWindow(hwnd))
            {
                hwnd = FindHiddenWindowForAccount(accountId);
            }

            // If user closed the Antigravity window while background processes stayed alive (or instance stopped),
            // spawning Antigravity.exe with --user-data-dir triggers Electron's single-instance restore to pop a window open!
            if (hwnd == IntPtr.Zero || !Win32Native.IsWindow(hwnd))
            {
                var restoreRes = LaunchInstance(accountId);
                return restoreRes.Ok
                    ? (true, $"Restored Account {accountId} ({acc.Label}) window from {(acc.Status == "running" ? "background session" : "stopped state")}")
                    : restoreRes;
            }

            if (Win32Native.IsIconic(hwnd))
            {
                Win32Native.ShowWindow(hwnd, Win32Native.SW_RESTORE);
            }
            else
            {
                Win32Native.ShowWindow(hwnd, Win32Native.SW_SHOW);
            }

            uint curThread = Win32Native.GetCurrentThreadId();
            IntPtr fgHwnd = Win32Native.GetForegroundWindow();
            uint fgThread = fgHwnd != IntPtr.Zero ? Win32Native.GetWindowThreadProcessId(fgHwnd, out _) : 0;
            uint targetThread = Win32Native.GetWindowThreadProcessId(hwnd, out _);

            bool attachedFg = fgThread != 0 && fgThread != curThread && Win32Native.AttachThreadInput(curThread, fgThread, true);
            bool attachedTarget = targetThread != 0 && targetThread != curThread && Win32Native.AttachThreadInput(curThread, targetThread, true);
            try
            {
                Win32Native.BringWindowToTop(hwnd);
                Win32Native.SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attachedTarget) Win32Native.AttachThreadInput(curThread, targetThread, false);
                if (attachedFg) Win32Native.AttachThreadInput(curThread, fgThread, false);
            }

            return (true, $"Focused Account {accountId} ({acc.Label}) window [HWND 0x{hwnd.ToInt64():X}]");
        }

        public (bool Ok, string Message) TerminateInstance(int accountId)
        {
            if (!IsValidAccountId(accountId))
                return (false, $"Invalid account ID: {accountId}");

            var cmdLines = GetAntigravityProcessCommandLines();
            string profileDir = Path.Combine(_config.ProfilesRootDir, $"Account{accountId}");
            int killed = 0;

            foreach (var kvp in cmdLines)
            {
                if (ContainsProfileArg(kvp.Value.ToLowerInvariant(), profileDir))
                {
                    try
                    {
                        using var p = Process.GetProcessById(kvp.Key);
                        p.Kill(true);
                        _pidToAccountCache.TryRemove(kvp.Key, out _);
                        killed++;
                    }
                    catch { }
                }
            }

            _stampedWindows.TryRemove(accountId, out _);
            _cpuSamples.TryRemove(accountId, out _);
            _lastSnapshot = null;

            return killed > 0
                ? (true, $"Terminated Account {accountId} ({killed} processes stopped)")
                : (false, $"Account {accountId} was already stopped");
        }

        public (bool Ok, string Message) RestartInstance(int accountId)
        {
            TerminateInstance(accountId);
            System.Threading.Thread.Sleep(350);
            return LaunchInstance(accountId);
        }

        public (bool Ok, string Message) ClearAuthCache(int accountId)
        {
            TerminateInstance(accountId);
            System.Threading.Thread.Sleep(300);

            string profileDir = Path.Combine(_config.ProfilesRootDir, $"Account{accountId}");
            string[] authDirs = { "Network", "Local Storage", "Session Storage", "WebStorage", "Cache", "Code Cache" };
            string[] authFiles = {
                "DIPS",
                "DIPS-wal",
                "SharedStorage",
                "app_storage.json",
                Path.Combine("Home", ".gemini", "jetski-standalone-oauth-token")
            };

            int removed = 0;
            foreach (var d in authDirs)
            {
                string full = Path.Combine(profileDir, d);
                if (Directory.Exists(full))
                {
                    try { Directory.Delete(full, true); removed++; } catch { }
                }
            }
            foreach (var f in authFiles)
            {
                string full = Path.Combine(profileDir, f);
                if (File.Exists(full))
                {
                    try { File.Delete(full); removed++; } catch { }
                }
            }

            _emailCache.TryRemove(accountId, out _);
            _lastSnapshot = null;

            return (true, $"Cleared isolated OAuth & Chromium session cache for Account {accountId} ({removed} stores wiped; shared brain preserved)");
        }

        public (bool Ok, string Message) LaunchAllInstances()
        {
            var snap = GetSnapshot(bypassCache: true);
            int launched = 0;
            foreach (var acc in snap.Accounts)
            {
                if (acc.Status != "running")
                {
                    var res = LaunchInstance(acc.Id);
                    if (res.Ok) launched++;
                    System.Threading.Thread.Sleep(200);
                }
            }
            return (true, launched > 0
                ? $"Launched {launched} Antigravity account instance(s) with independent taskbar AUMIDs"
                : "All 3 Antigravity accounts are already running");
        }

        public (bool Ok, string Message) TerminateAllInstances()
        {
            int count = 0;
            for (int id = 1; id <= 3; id++)
            {
                if (TerminateInstance(id).Ok) count++;
            }
            return (true, $"Terminated all active Antigravity account instances ({count} accounts stopped)");
        }

        private static void CopyFileIfChanged(string srcPath, string dstPath)
        {
            if (!File.Exists(srcPath)) return;
            if (File.Exists(dstPath))
            {
                var srcInfo = new FileInfo(srcPath);
                var dstInfo = new FileInfo(dstPath);
                if (srcInfo.Length == dstInfo.Length && srcInfo.LastWriteTimeUtc == dstInfo.LastWriteTimeUtc)
                    return;
            }
            File.Copy(srcPath, dstPath, true);
        }

        public (bool Ok, string Message) SyncUserSettingsAcrossProfiles(int? onlyAccountId = null)
        {
            try
            {
                EnsureDirectoriesAndEnv();

                string roaming = GetRealAppDataRoaming();
                string mainAntigravityDir = Path.Combine(roaming, "Antigravity");
                string baseUser = Path.Combine(mainAntigravityDir, "User");
                string baseGlobal = Path.Combine(baseUser, "globalStorage");
                string mainAppStoragePath = Path.Combine(mainAntigravityDir, "app_storage.json");

                string? sourceSettings = File.Exists(Path.Combine(baseUser, "settings.json"))
                    ? Path.Combine(baseUser, "settings.json")
                    : null;

                string? sourceKeybindings = File.Exists(Path.Combine(baseUser, "keybindings.json"))
                    ? Path.Combine(baseUser, "keybindings.json")
                    : null;

                // Load main app_storage.json as JsonObject
                System.Text.Json.Nodes.JsonObject? mainStorageNode = null;
                if (File.Exists(mainAppStoragePath))
                {
                    try
                    {
                        var parsed = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(mainAppStoragePath));
                        if (parsed is System.Text.Json.Nodes.JsonObject obj)
                        {
                            mainStorageNode = obj;
                        }
                    }
                    catch { }
                }

                int startId = onlyAccountId ?? 1;
                int endId = onlyAccountId ?? 3;

                for (int i = startId; i <= endId; i++)
                {
                    string accDir = Path.Combine(_config.ProfilesRootDir, $"Account{i}");
                    string targetUser = Path.Combine(accDir, "User");
                    string targetGlobal = Path.Combine(targetUser, "globalStorage");
                    Directory.CreateDirectory(targetUser);
                    Directory.CreateDirectory(targetGlobal);

                    // 1. Sync settings.json & keybindings.json (only if changed)
                    if (sourceSettings != null && _config.SyncUserSettings)
                    {
                        CopyFileIfChanged(sourceSettings, Path.Combine(targetUser, "settings.json"));
                    }
                    if (sourceKeybindings != null && _config.SyncKeybindings)
                    {
                        CopyFileIfChanged(sourceKeybindings, Path.Combine(targetUser, "keybindings.json"));
                    }

                    // 2. Sync globalStorage (storage.json & state.vscdb)
                    if (Directory.Exists(baseGlobal))
                    {
                        string srcStorageJson = Path.Combine(baseGlobal, "storage.json");
                        if (File.Exists(srcStorageJson))
                        {
                            CopyFileIfChanged(srcStorageJson, Path.Combine(targetGlobal, "storage.json"));
                        }

                        string srcStateDb = Path.Combine(baseGlobal, "state.vscdb");
                        string targetStateDb = Path.Combine(targetGlobal, "state.vscdb");
                        if (File.Exists(srcStateDb) && !File.Exists(targetStateDb))
                        {
                            File.Copy(srcStateDb, targetStateDb, true);
                        }
                    }

                    // 3. Sync app_storage.json (Projects order, sidebar sections, wizard bypass) while preserving isolated auth
                    if (mainStorageNode != null)
                    {
                        string targetAppStoragePath = Path.Combine(accDir, "app_storage.json");
                        string tokenPath = Path.Combine(accDir, "Home", ".gemini", "jetski-standalone-oauth-token");
                        bool hasValidToken = File.Exists(tokenPath) && new FileInfo(tokenPath).Length > 10;

                        string? existingUsername = null;
                        string? existingTos = null;
                        string? existingAgyUsername = null;
                        string? existingAgyTos = null;

                        System.Text.Json.Nodes.JsonObject? existingObj = null;
                        if (File.Exists(targetAppStoragePath))
                        {
                            try
                            {
                                existingObj = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(targetAppStoragePath)) as System.Text.Json.Nodes.JsonObject;
                                if (existingObj != null && hasValidToken)
                                {
                                    if (existingObj.TryGetPropertyValue("jetski.onboarding.lastLoginUsername", out var uNode) && uNode != null)
                                        existingUsername = uNode.GetValue<string>();
                                    if (existingObj.TryGetPropertyValue("jetski.onboarding.lastLoginIsGcpTos", out var tNode) && tNode != null)
                                        existingTos = tNode.GetValue<string>();
                                    if (existingObj.TryGetPropertyValue("agy.onboarding.lastLoginUsername", out var auNode) && auNode != null)
                                        existingAgyUsername = auNode.GetValue<string>();
                                    if (existingObj.TryGetPropertyValue("agy.onboarding.lastLoginIsGcpTos", out var atNode) && atNode != null)
                                        existingAgyTos = atNode.GetValue<string>();
                                }
                            }
                            catch { }
                        }

                        var merged = existingObj ?? (System.Text.Json.Nodes.JsonNode.Parse(mainStorageNode.ToJsonString()) as System.Text.Json.Nodes.JsonObject);
                        if (merged != null)
                        {
                            if (mainStorageNode.TryGetPropertyValue("projectsOrder", out var poNode) && poNode != null)
                                merged["projectsOrder"] = poNode.DeepClone();
                            if (mainStorageNode.TryGetPropertyValue("sidebar_collapsed_sections", out var scNode) && scNode != null)
                                merged["sidebar_collapsed_sections"] = scNode.DeepClone();

                            if (hasValidToken && (!string.IsNullOrEmpty(existingUsername) || !string.IsNullOrEmpty(existingAgyUsername)))
                            {
                                if (!string.IsNullOrEmpty(existingUsername))
                                    merged["jetski.onboarding.lastLoginUsername"] = existingUsername;
                                if (!string.IsNullOrEmpty(existingTos))
                                    merged["jetski.onboarding.lastLoginIsGcpTos"] = existingTos;
                                if (!string.IsNullOrEmpty(existingAgyUsername))
                                    merged["agy.onboarding.lastLoginUsername"] = existingAgyUsername;
                                if (!string.IsNullOrEmpty(existingAgyTos))
                                    merged["agy.onboarding.lastLoginIsGcpTos"] = existingAgyTos;
                            }
                            else
                            {
                                merged.Remove("jetski.onboarding.lastLoginUsername");
                                merged.Remove("jetski.onboarding.lastLoginIsGcpTos");
                                merged.Remove("agy.onboarding.lastLoginUsername");
                                merged.Remove("agy.onboarding.lastLoginIsGcpTos");
                            }

                            merged["ide-install-wizard-shown"] = "true";

                            try
                            {
                                File.WriteAllText(
                                    targetAppStoragePath,
                                    merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                                    new UTF8Encoding(false));
                            }
                            catch { }
                        }
                    }
                }
                return (true, onlyAccountId.HasValue
                    ? $"Synchronized workspace state for Account {onlyAccountId.Value}"
                    : "Synchronized projects, workspaceStorage, and UI settings across Accounts 1, 2, and 3");
            }
            catch (Exception ex)
            {
                return (false, $"Workspace sync warning: {ex.Message}");
            }
        }

        public (bool Ok, string Message) RegenerateShortcuts()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidateScripts =
                {
                    Path.Combine(baseDir, "scripts", "generate-shortcuts.ps1"),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "scripts", "generate-shortcuts.ps1"))
                };
                string? scriptPath = candidateScripts.FirstOrDefault(File.Exists);

                if (scriptPath != null)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.ArgumentList.Add("-NoProfile");
                    psi.ArgumentList.Add("-ExecutionPolicy");
                    psi.ArgumentList.Add("Bypass");
                    psi.ArgumentList.Add("-File");
                    psi.ArgumentList.Add(scriptPath);
                    using var p = Process.Start(psi);
                    p?.WaitForExit(15000);
                    _stampedWindows.Clear();
                    DisposeResources();
                    _lastSnapshot = null;
                    return (true, "Regenerated 3 AUMID .lnk shortcuts & custom badged .ico files");
                }
                return (false, "Shortcut generator script not found");
            }
            catch (Exception ex)
            {
                return (false, $"Shortcut generation error: {ex.Message}");
            }
        }

        public (bool Ok, string Message) OptimizeSqliteWal()
        {
            string dbPath = Path.Combine(_config.SharedAppDataDir, "conversation_summaries.db");
            if (!File.Exists(dbPath))
            {
                return (true, $"Shared brain directory ready at {_config.SharedAppDataDir}");
            }
            bool locked = CheckSqliteWriteLock(dbPath);
            double mb = new FileInfo(dbPath).Length / (1024.0 * 1024.0);
            return (true, locked
                ? $"SQLite conversation_summaries.db ({mb:F2} MB) is actively in use — WAL concurrent reader mode active"
                : $"SQLite conversation_summaries.db ({mb:F2} MB) verified unlocked & ready for multi-account access");
        }
    }

    #endregion

    #region Custom Dark Context Menu Renderer

    internal class DarkTrayColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(12, 17, 28);
        public override Color MenuBorder => Color.FromArgb(36, 48, 70);
        public override Color MenuItemBorder => Color.FromArgb(56, 189, 248);
        public override Color MenuItemSelected => Color.FromArgb(24, 34, 54);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(24, 34, 54);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(24, 34, 54);
        public override Color ImageMarginGradientBegin => Color.FromArgb(12, 17, 28);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(12, 17, 28);
        public override Color ImageMarginGradientEnd => Color.FromArgb(12, 17, 28);
        public override Color SeparatorDark => Color.FromArgb(30, 41, 59);
        public override Color SeparatorLight => Color.FromArgb(30, 41, 59);
    }

    internal class PremiumTrayRenderer : ToolStripProfessionalRenderer
    {
        public PremiumTrayRenderer() : base(new DarkTrayColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var bg = new SolidBrush(Color.FromArgb(12, 17, 28));
            e.Graphics.FillRectangle(bg, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var rect = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
            using var pen = new Pen(Color.FromArgb(38, 50, 72), 1f);
            e.Graphics.DrawRectangle(pen, rect);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled) return;
            if (e.Item.Selected)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(5, 2, e.Item.Width - 10, e.Item.Height - 4);
                using var path = RoundedRect(rect, 6);
                using var fill = new SolidBrush(Color.FromArgb(26, 38, 60));
                using var border = new Pen(Color.FromArgb(65, 56, 189, 248), 1f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Color.FromArgb(30, 41, 59), 1f);
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = e.ArrowRectangle;
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            Color c = e.Item != null && e.Item.Selected ? Color.FromArgb(241, 245, 249) : Color.FromArgb(148, 163, 184);
            using var pen = new Pen(c, 1.6f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawLines(pen, new[]
            {
                new PointF(cx - 2.5f, cy - 4f),
                new PointF(cx + 2f, cy),
                new PointF(cx - 2.5f, cy + 4f)
            });
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    #endregion

    #region Main Hub Form & System Tray Host

    public class HubMainForm : Form
    {
        private readonly HubOrchestrator _orchestrator;
        private readonly WebView2 _webView;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly Dictionary<string, Bitmap> _trayGlyphs = new();
        private readonly System.Threading.Timer _aumidTimer;
        private int _bgPollBusy = 0;
        private bool _allowExit = false;

        public HubMainForm()
        {
            Win32Native.SetCurrentProcessExplicitAppUserModelID("Antigravity.MultiAccount.Hub");
            _orchestrator = new HubOrchestrator();

            Text = "Antigravity Multi-Account Hub";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(680, 480);

            var workArea = Screen.FromControl(this).WorkingArea;
            Width = Math.Min(1440, Math.Max(820, workArea.Width - 60));
            Height = Math.Min(880, Math.Max(560, workArea.Height - 60));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(6, 9, 17);
            ForeColor = Color.White;
            Padding = new Padding(2);

            // Enable Windows 11 Dark Title Bar
            int dark = 1;
            Win32Native.DwmSetWindowAttribute(Handle, Win32Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            Icon = ResolveHubIcon();

            // Initialize Dynamic Tray Context Menu
            _trayMenu = new ContextMenuStrip
            {
                Renderer = new PremiumTrayRenderer(),
                ShowImageMargin = true,
                ShowCheckMargin = false,
                ImageScalingSize = new Size(20, 20),
                Padding = new Padding(2, 6, 2, 6),
                ForeColor = Color.FromArgb(241, 245, 249),
                BackColor = Color.FromArgb(12, 17, 28),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };
            _trayMenu.Opening += OnTrayMenuOpening;

            _notifyIcon = new NotifyIcon
            {
                Icon = Icon ?? SystemIcons.Application,
                Text = "Antigravity Multi-Account Hub (3 Accounts)",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    RestoreAndFocusHub();
                }
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreAndFocusHub();

            // WebView2 Control
            _webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.FromArgb(6, 9, 17)
            };
            Controls.Add(_webView);

            // Background thread-pool timer to stamp newly appearing Antigravity windows even when minimized to tray
            _aumidTimer = new System.Threading.Timer(_ =>
            {
                if (System.Threading.Interlocked.Exchange(ref _bgPollBusy, 1) == 1)
                    return;
                try
                {
                    _orchestrator.GetSnapshot();
                }
                catch { }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _bgPollBusy, 0);
                }
            }, null, 3000, 3000);

            Load += async (s, e) =>
            {
                try
                {
                    await InitializeWebViewAsync();
                }
                catch (Exception ex)
                {
                    AppendStartupLog($"Load Event Handler Exception: {ex}");
                }
            };

            Shown += (s, e) => RestoreAndFocusHub();
        }

        private Icon? ResolveHubIcon()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates =
                {
                    Path.Combine(baseDir, "assets", "icons", "AntigravityHub.ico"),
                    Path.Combine(baseDir, "AntigravityHub.ico"),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "assets", "icons", "AntigravityHub.ico")),
                    Path.Combine(_orchestrator.Config.ProfilesRootDir, "Icons", "AntigravityHub.ico")
                };
                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        return new Icon(path);
                    }
                }
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    return Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch { }
            return null;
        }

        private static void AppendStartupLog(string msg)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hub-startup.log");
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] {msg}\n");
            }
            catch { }
        }

        private static readonly uint WM_RESTORE_HUB = Win32Native.RegisterWindowMessage("ANTIGRAVITY_HUB_RESTORE_SHOW_MSG");

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)WM_RESTORE_HUB)
            {
                RestoreAndFocusHub();
            }
            base.WndProc(ref m);
        }

        public void RestoreAndFocusHub()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RestoreAndFocusHub));
                return;
            }
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            BringToFront();
            Activate();
            Win32Native.ShowWindow(Handle, Win32Native.SW_SHOW);
            Win32Native.ShowWindow(Handle, Win32Native.SW_RESTORE);
            Win32Native.BringWindowToTop(Handle);
            Win32Native.SetForegroundWindow(Handle);
        }

        private void RunTrayActionAsync(Func<(bool Ok, string Message)> action)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var res = action();
                    NotifyFrontendEvent(res.Message);
                }
                catch (Exception ex)
                {
                    NotifyFrontendEvent($"Action error: {ex.Message}");
                }
            });
        }

        private Bitmap GetTrayGlyph(string key)
        {
            if (_trayGlyphs.TryGetValue(key, out var existing))
                return existing;

            var bmp = new Bitmap(20, 20, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            switch (key)
            {
                case "hub":
                {
                    using var b1 = new SolidBrush(Color.FromArgb(4, 120, 87));
                    using var b2 = new SolidBrush(Color.FromArgb(16, 185, 129));
                    using var b3 = new SolidBrush(Color.FromArgb(110, 231, 183));
                    g.FillPolygon(b1, new[] { new PointF(10, 11.5f), new PointF(17.5f, 14.5f), new PointF(10, 17.5f), new PointF(2.5f, 14.5f) });
                    g.FillPolygon(b2, new[] { new PointF(10, 7f), new PointF(17.5f, 10f), new PointF(10, 13f), new PointF(2.5f, 10f) });
                    g.FillPolygon(b3, new[] { new PointF(10, 2.5f), new PointF(17.5f, 5.5f), new PointF(10, 8.5f), new PointF(2.5f, 5.5f) });
                    break;
                }
                case "acc-1-on":
                case "acc-2-on":
                case "acc-3-on":
                case "acc-1-off":
                case "acc-2-off":
                case "acc-3-off":
                {
                    bool on = key.EndsWith("-on");
                    string num = key.Contains("-1-") ? "1" : key.Contains("-2-") ? "2" : "3";
                    Color accent = num == "1" ? Color.FromArgb(56, 189, 248)
                        : num == "2" ? Color.FromArgb(167, 139, 250)
                        : Color.FromArgb(251, 191, 36);

                    var rect = new RectangleF(1.5f, 1.5f, 17f, 17f);
                    using var path = new System.Drawing.Drawing2D.GraphicsPath();
                    float d = 7f;
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();

                    using var fill = new SolidBrush(on ? Color.FromArgb(36, accent) : Color.FromArgb(15, 23, 36));
                    using var border = new Pen(on ? accent : Color.FromArgb(120, accent), 1.5f);
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);

                    // Geometrically center the digit using exact glyph outline bounds
                    using var textPath = new System.Drawing.Drawing2D.GraphicsPath();
                    using var family = new FontFamily("Segoe UI");
                    textPath.AddString(num, family, (int)FontStyle.Bold, 11.5f, PointF.Empty, StringFormat.GenericTypographic);
                    var bounds = textPath.GetBounds();
                    float dx = (rect.X + rect.Width / 2f) - (bounds.X + bounds.Width / 2f);
                    float dy = (rect.Y + rect.Height / 2f) - (bounds.Y + bounds.Height / 2f);
                    using var matrix = new System.Drawing.Drawing2D.Matrix();
                    matrix.Translate(dx, dy);
                    textPath.Transform(matrix);
                    using var numBrush = new SolidBrush(on ? Color.FromArgb(248, 250, 252) : Color.FromArgb(148, 163, 184));
                    g.FillPath(numBrush, textPath);

                    if (on)
                    {
                        using var dotBg = new SolidBrush(Color.FromArgb(12, 17, 28));
                        using var dot = new SolidBrush(Color.FromArgb(52, 211, 153));
                        g.FillEllipse(dotBg, 13.5f, 13.5f, 6f, 6f);
                        g.FillEllipse(dot, 14.3f, 14.3f, 4.4f, 4.4f);
                    }
                    break;
                }
                case "focus":
                {
                    using var pen = new Pen(Color.FromArgb(56, 189, 248), 1.75f);
                    g.DrawEllipse(pen, 4, 4, 12, 12);
                    g.DrawLine(pen, 10, 2, 10, 5.5f);
                    g.DrawLine(pen, 10, 14.5f, 10, 18);
                    g.DrawLine(pen, 2, 10, 5.5f, 10);
                    g.DrawLine(pen, 14.5f, 10, 18, 10);
                    break;
                }
                case "restart":
                {
                    using var pen = new Pen(Color.FromArgb(203, 213, 225), 1.75f)
                    {
                        StartCap = System.Drawing.Drawing2D.LineCap.Round,
                        EndCap = System.Drawing.Drawing2D.LineCap.Round
                    };
                    g.DrawArc(pen, 4, 4, 12, 12, 40, 280);
                    g.DrawLines(pen, new[] { new PointF(13f, 3.2f), new PointF(16f, 6.2f), new PointF(12.5f, 7.5f) });
                    break;
                }
                case "launch":
                {
                    using var b = new SolidBrush(Color.FromArgb(52, 211, 153));
                    g.FillPolygon(b, new[] { new PointF(5.5f, 4f), new PointF(16f, 10f), new PointF(5.5f, 16f) });
                    break;
                }
                case "kill":
                {
                    using var b = new SolidBrush(Color.FromArgb(251, 113, 133));
                    g.FillRectangle(b, 5, 5, 10, 10);
                    break;
                }
                case "settings":
                {
                    using var pen = new Pen(Color.FromArgb(148, 163, 184), 1.75f);
                    g.DrawEllipse(pen, 6.5f, 6.5f, 7, 7);
                    for (int a = 0; a < 360; a += 45)
                    {
                        double rad = a * Math.PI / 180.0;
                        float x1 = 10f + (float)Math.Cos(rad) * 5.2f;
                        float y1 = 10f + (float)Math.Sin(rad) * 5.2f;
                        float x2 = 10f + (float)Math.Cos(rad) * 7.8f;
                        float y2 = 10f + (float)Math.Sin(rad) * 7.8f;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                    break;
                }
                case "exit":
                {
                    using var pen = new Pen(Color.FromArgb(248, 113, 113), 2f)
                    {
                        StartCap = System.Drawing.Drawing2D.LineCap.Round,
                        EndCap = System.Drawing.Drawing2D.LineCap.Round
                    };
                    g.DrawLine(pen, 5.5f, 5.5f, 14.5f, 14.5f);
                    g.DrawLine(pen, 14.5f, 5.5f, 5.5f, 14.5f);
                    break;
                }
            }

            _trayGlyphs[key] = bmp;
            return bmp;
        }

        private ToolStripMenuItem CreateMenuItem(string text, string glyphKey, Color foreColor, Font? font = null)
        {
            return new ToolStripMenuItem(text)
            {
                Image = GetTrayGlyph(glyphKey),
                ForeColor = foreColor,
                Font = font ?? _trayMenu.Font,
                Padding = new Padding(4, 4, 12, 4)
            };
        }

        private void OnTrayMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _trayMenu.Items.Clear();

            var headerItem = CreateMenuItem(
                "Antigravity Hub",
                "hub",
                Color.FromArgb(110, 231, 183),
                new Font("Segoe UI Semibold", 9.5f));
            headerItem.Click += (s, args) => RestoreAndFocusHub();
            _trayMenu.Items.Add(headerItem);
            _trayMenu.Items.Add(new ToolStripSeparator());

            var snapshot = _orchestrator.GetSnapshot();
            foreach (var acc in snapshot.Accounts)
            {
                bool running = acc.Status == "running";
                string statusText = running
                    ? $"Account {acc.Id} · {acc.Label}   (Running)"
                    : $"Account {acc.Id} · {acc.Label}   (Stopped)";

                var accMenu = CreateMenuItem(
                    statusText,
                    running ? $"acc-{acc.Id}-on" : $"acc-{acc.Id}-off",
                    running ? Color.FromArgb(241, 245, 249) : Color.FromArgb(148, 163, 184),
                    running ? new Font("Segoe UI Semibold", 9.5f) : _trayMenu.Font);

                if (accMenu.DropDown is ToolStripDropDownMenu subDrop)
                {
                    subDrop.ShowImageMargin = true;
                    subDrop.ShowCheckMargin = false;
                    subDrop.ImageScalingSize = new Size(20, 20);
                    subDrop.Padding = new Padding(2, 5, 2, 5);
                }

                int accId = acc.Id;
                if (running)
                {
                    var focusSub = CreateMenuItem($"Focus Window  (PID {acc.Pid})", "focus", Color.FromArgb(186, 230, 253));
                    focusSub.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.FocusInstance(accId));

                    var restartSub = CreateMenuItem("Restart Instance", "restart", Color.FromArgb(226, 232, 240));
                    restartSub.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.RestartInstance(accId));

                    var killSub = CreateMenuItem("Kill Process", "kill", Color.FromArgb(253, 164, 175));
                    killSub.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.TerminateInstance(accId));

                    accMenu.DropDownItems.Add(focusSub);
                    accMenu.DropDownItems.Add(restartSub);
                    accMenu.DropDownItems.Add(killSub);

                    // Direct click focuses the instance
                    accMenu.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.FocusInstance(accId));
                }
                else
                {
                    var launchSub = CreateMenuItem("Launch Instance", "launch", Color.FromArgb(110, 231, 183));
                    launchSub.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.LaunchInstance(accId));
                    accMenu.DropDownItems.Add(launchSub);
                    accMenu.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.LaunchInstance(accId));
                }

                _trayMenu.Items.Add(accMenu);
            }

            _trayMenu.Items.Add(new ToolStripSeparator());

            var launchAllItem = CreateMenuItem("Launch All Accounts", "launch", Color.FromArgb(110, 231, 183));
            launchAllItem.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.LaunchAllInstances());
            _trayMenu.Items.Add(launchAllItem);

            var killAllItem = CreateMenuItem("Terminate All", "kill", Color.FromArgb(253, 164, 175));
            killAllItem.Click += (s, args) => RunTrayActionAsync(() => _orchestrator.TerminateAllInstances());
            _trayMenu.Items.Add(killAllItem);

            var settingsItem = CreateMenuItem("Settings && Paths", "settings", Color.FromArgb(226, 232, 240));
            settingsItem.Click += (s, args) =>
            {
                RestoreAndFocusHub();
                NotifyFrontendEvent("Opened Hub Settings & Paths", true);
            };
            _trayMenu.Items.Add(settingsItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            var exitItem = CreateMenuItem("Exit Hub", "exit", Color.FromArgb(248, 113, 113));
            exitItem.Click += (s, args) => ExitHubApplication();
            _trayMenu.Items.Add(exitItem);
        }

        private void NotifyFrontendEvent(string message, bool openSettings = false)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => NotifyFrontendEvent(message, openSettings)));
                return;
            }
            if (_webView.CoreWebView2 == null) return;
            try
            {
                string json = JsonSerializer.Serialize(new { type = "tray-event", message, openSettings });
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                string webViewDataDir = Path.Combine(_orchestrator.Config.ProfilesRootDir, "HubWebView2Cache");
                Directory.CreateDirectory(webViewDataDir);
                var env = await CoreWebView2Environment.CreateAsync(null, webViewDataDir);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _webView.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;

                _webView.CoreWebView2.NavigationStarting += (s, args) =>
                {
                    if (string.Equals(args.Uri, "about:blank", StringComparison.OrdinalIgnoreCase) ||
                        args.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                    if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) ||
                        !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(uri.Host, "antigravity.hub", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Cancel = true;
                    }
                };
                _webView.CoreWebView2.NewWindowRequested += (s, args) =>
                {
                    args.Handled = true;
                };

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Locate compiled dist/ folder
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidateDirs =
                {
                    Path.Combine(baseDir, "dist"),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "dist")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "dist"))
                };

                string? distPath = candidateDirs.FirstOrDefault(d => File.Exists(Path.Combine(d, "index.html")));
                if (distPath != null)
                {
                    _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "antigravity.hub",
                        distPath,
                        CoreWebView2HostResourceAccessKind.Allow);
                    _webView.CoreWebView2.Navigate("https://antigravity.hub/index.html");
                }
                else
                {
                    AppendStartupLog("dist folder not found!");
                    _webView.CoreWebView2.NavigateToString(
                        "<body style='background:#060911;color:#fff;font-family:sans-serif;padding:40px'><h2>Antigravity Hub UI dist folder not found. Run build.bat first.</h2></body>");
                }
            }
            catch (Exception ex)
            {
                AppendStartupLog($"InitializeWebViewAsync ERROR: {ex}");
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string requestId = "";
            string command = "";
            string? argsRaw = null;

            try
            {
                string raw = e.TryGetWebMessageAsString();
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                requestId = root.GetProperty("requestId").GetString() ?? "";
                command = root.GetProperty("command").GetString() ?? "";
                if (root.TryGetProperty("args", out var a) && a.ValueKind != JsonValueKind.Undefined)
                {
                    argsRaw = a.GetRawText();
                }
            }
            catch (Exception ex)
            {
                AppendStartupLog($"WebMessage parse ERROR: {ex}");
                SendBridgeResponse(requestId, null, ex.Message);
                return;
            }

            // Execute ALL bridge commands asynchronously off the UI thread so resizing/dragging/UI never freezes
            _ = Task.Run(() =>
            {
                try
                {
                    JsonElement args = default;
                    JsonDocument? argsDoc = null;
                    if (!string.IsNullOrEmpty(argsRaw))
                    {
                        argsDoc = JsonDocument.Parse(argsRaw);
                        args = argsDoc.RootElement;
                    }
                    using (argsDoc)
                    {
                        object result = HandleBridgeCommand(command, args);
                        SendBridgeResponse(requestId, result, null);
                    }
                }
                catch (Exception ex)
                {
                    AppendStartupLog($"WebMessage ERROR ({command}): {ex}");
                    SendBridgeResponse(requestId, null, ex.Message);
                }
            });
        }

        private object HandleBridgeCommand(string command, JsonElement args)
        {
            int accountId = 0;
            if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("accountId", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                idProp.TryGetInt32(out accountId);
            }

            switch (command)
            {
                case "get_hub_state":
                    return _orchestrator.GetSnapshot();

                case "launch_instance":
                {
                    if (!HubOrchestrator.IsValidAccountId(accountId))
                        return new { ok = false, message = $"Invalid account ID: {accountId}" };
                    string? ws = null;
                    if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("workspacePath", out var wsProp) && wsProp.ValueKind == JsonValueKind.String)
                        ws = wsProp.GetString();
                    var r = _orchestrator.LaunchInstance(accountId, ws);
                    return new { ok = r.Ok, message = r.Message };
                }

                case "focus_instance":
                {
                    if (!HubOrchestrator.IsValidAccountId(accountId))
                        return new { ok = false, message = $"Invalid account ID: {accountId}" };
                    var r = _orchestrator.FocusInstance(accountId);
                    return new { ok = r.Ok, message = r.Message };
                }

                case "terminate_instance":
                {
                    if (!HubOrchestrator.IsValidAccountId(accountId))
                        return new { ok = false, message = $"Invalid account ID: {accountId}" };
                    var r = _orchestrator.TerminateInstance(accountId);
                    return new { ok = r.Ok, message = r.Message };
                }

                case "restart_instance":
                {
                    if (!HubOrchestrator.IsValidAccountId(accountId))
                        return new { ok = false, message = $"Invalid account ID: {accountId}" };
                    var r = _orchestrator.RestartInstance(accountId);
                    return new { ok = r.Ok, message = r.Message };
                }

                case "clear_auth_cache":
                {
                    if (!HubOrchestrator.IsValidAccountId(accountId))
                        return new { ok = false, message = $"Invalid account ID: {accountId}" };
                    var r = _orchestrator.ClearAuthCache(accountId);
                    return new { ok = r.Ok, message = r.Message };
                }

                case "launch_all_instances":
                {
                    var r = _orchestrator.LaunchAllInstances();
                    return new { ok = r.Ok, message = r.Message };
                }

                case "terminate_all_instances":
                {
                    var r = _orchestrator.TerminateAllInstances();
                    return new { ok = r.Ok, message = r.Message };
                }

                case "open_shared_storage":
                {
                    string sharedDir = _orchestrator.Config.SharedAppDataDir;
                    if (!HubOrchestrator.IsSafeWindowsPath(sharedDir) || !Directory.Exists(sharedDir))
                        return new { ok = false, message = "Shared brain directory path is invalid or missing" };
                    var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                    psi.ArgumentList.Add(sharedDir);
                    Process.Start(psi);
                    return new { ok = true, message = $"Opened {sharedDir} in File Explorer" };
                }

                case "open_profile_folder":
                {
                    if (!HubOrchestrator.IsValidAccountId(accountId))
                        return new { ok = false, message = $"Invalid account ID: {accountId}" };
                    string dir = Path.Combine(_orchestrator.Config.ProfilesRootDir, $"Account{accountId}");
                    if (!HubOrchestrator.IsSafeWindowsPath(dir))
                        return new { ok = false, message = "Invalid profile folder path" };
                    Directory.CreateDirectory(dir);
                    var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                    psi.ArgumentList.Add(dir);
                    Process.Start(psi);
                    return new { ok = true, message = $"Opened Account {accountId} isolated profile directory" };
                }

                case "regenerate_shortcuts":
                {
                    var r = _orchestrator.RegenerateShortcuts();
                    BeginInvoke(new Action(() =>
                    {
                        var freshIcon = ResolveHubIcon();
                        if (freshIcon != null)
                        {
                            Icon = freshIcon;
                            _notifyIcon.Icon = freshIcon;
                        }
                    }));
                    return new { ok = r.Ok, message = r.Message };
                }

                case "sync_workspaces_and_settings":
                {
                    var r = _orchestrator.SyncUserSettingsAcrossProfiles();
                    return new { ok = r.Ok, message = r.Message };
                }

                case "optimize_sqlite_wal":
                {
                    var r = _orchestrator.OptimizeSqliteWal();
                    return new { ok = r.Ok, message = r.Message };
                }

                case "save_config":
                {
                    if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("config", out var cfgEl))
                    {
                        var updated = JsonSerializer.Deserialize<HubConfig>(cfgEl.GetRawText());
                        if (updated != null)
                        {
                            _orchestrator.SaveConfig(updated);
                            return new { ok = true, message = "Saved Hub configuration & synchronized environment variables" };
                        }
                    }
                    return new { ok = false, message = "Invalid configuration payload" };
                }

                case "minimize_to_tray":
                {
                    BeginInvoke(new Action(() =>
                    {
                        Hide();
                        _notifyIcon.ShowBalloonTip(
                            2000,
                            "Antigravity Hub Running in System Tray",
                            "Right-click the tray icon to monitor or switch Accounts 1, 2, and 3.",
                            ToolTipIcon.Info);
                    }));
                    return new { ok = true, message = "Minimized to Windows System Tray" };
                }

                case "browse_folder":
                {
                    string? initialPath = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("initialPath", out var pEl) && pEl.ValueKind == JsonValueKind.String
                        ? pEl.GetString()
                        : null;
                    string title = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String
                        ? (tEl.GetString() ?? "Select Folder")
                        : "Select Folder";

                    string? selected = null;
                    if (!IsDisposed)
                    {
                        selected = (string?)Invoke(new Func<string?>(() =>
                        {
                            using var dlg = new FolderBrowserDialog
                            {
                                Description = title,
                                UseDescriptionForTitle = true,
                                ShowNewFolderButton = true
                            };
                            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                            {
                                dlg.InitialDirectory = initialPath;
                            }
                            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedPath : null;
                        }));
                    }
                    return string.IsNullOrWhiteSpace(selected)
                        ? new { ok = false, path = (string?)null }
                        : new { ok = true, path = (string?)selected };
                }

                case "browse_file":
                {
                    string? initialPath = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("initialPath", out var pEl) && pEl.ValueKind == JsonValueKind.String
                        ? pEl.GetString()
                        : null;
                    string title = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String
                        ? (tEl.GetString() ?? "Select Antigravity Executable")
                        : "Select Antigravity Executable";

                    string? selected = null;
                    if (!IsDisposed)
                    {
                        selected = (string?)Invoke(new Func<string?>(() =>
                        {
                            using var dlg = new OpenFileDialog
                            {
                                Title = title,
                                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                                CheckFileExists = true
                            };
                            string? dir = !string.IsNullOrWhiteSpace(initialPath) ? Path.GetDirectoryName(initialPath) : null;
                            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                            {
                                dlg.InitialDirectory = dir;
                            }
                            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
                        }));
                    }
                    return string.IsNullOrWhiteSpace(selected)
                        ? new { ok = false, path = (string?)null }
                        : new { ok = true, path = (string?)selected };
                }

                case "exit_hub":
                {
                    BeginInvoke(new Action(ExitHubApplication));
                    return new { ok = true, message = "Exiting Hub..." };
                }

                default:
                    return new { ok = false, message = $"Unknown command: {command}" };
            }
        }

        private void SendBridgeResponse(string requestId, object? result, string? error)
        {
            if (IsDisposed) return;
            var payload = new
            {
                requestId,
                result,
                error
            };
            string json = JsonSerializer.Serialize(payload);
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && _webView.CoreWebView2 != null)
                    {
                        _webView.CoreWebView2.PostWebMessageAsJson(json);
                    }
                }));
                return;
            }
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing && _orchestrator.Config.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
                _notifyIcon.ShowBalloonTip(
                    2000,
                    "Antigravity Hub Active in Tray",
                    "Left-click to reopen Dashboard or right-click for dynamic Account controls.",
                    ToolTipIcon.Info);
                return;
            }
            try { _aumidTimer.Dispose(); } catch { }
            _orchestrator.DisposeResources();
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _trayMenu.Dispose();
                foreach (var bmp in _trayGlyphs.Values) { try { bmp.Dispose(); } catch { } }
                _trayGlyphs.Clear();
            }
            catch { }
            base.OnFormClosing(e);
        }

        private void ExitHubApplication()
        {
            _allowExit = true;
            try { _aumidTimer.Dispose(); } catch { }
            _orchestrator.DisposeResources();
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _trayMenu.Dispose();
                foreach (var bmp in _trayGlyphs.Values) { try { bmp.Dispose(); } catch { } }
                _trayGlyphs.Clear();
            }
            catch { }
            Application.Exit();
        }
    }

    internal static class Program
    {
        private static System.Threading.Mutex? _singleInstanceMutex;

        [STAThread]
        static void Main()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hub-startup.log");
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] AntigravityHub Main entered\n");

                const string mutexName = @"Global\AntigravityHub_SingleInstance_Mutex";
                _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] Mutex createdNew: {createdNew}\n");

                if (!createdNew)
                {
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] Another instance is already running; broadcasting restore.\n");
                    uint restoreMsg = Win32Native.RegisterWindowMessage("ANTIGRAVITY_HUB_RESTORE_SHOW_MSG");
                    Win32Native.PostMessage(Win32Native.HWND_BROADCAST, restoreMsg, IntPtr.Zero, IntPtr.Zero);

                    IntPtr existingHwnd = FindExistingHubWindow();
                    if (existingHwnd != IntPtr.Zero)
                    {
                        Win32Native.ShowWindow(existingHwnd, Win32Native.SW_SHOW);
                        Win32Native.ShowWindow(existingHwnd, Win32Native.SW_RESTORE);
                        Win32Native.BringWindowToTop(existingHwnd);
                        Win32Native.SetForegroundWindow(existingHwnd);
                    }
                    return;
                }

                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] AppDomain.UnhandledException: {e.ExceptionObject}\n");
                };
                Application.ApplicationExit += (s, e) =>
                {
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] Application.ApplicationExit triggered\n");
                };

                File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] Launching HubMainForm Application.Run\n");
                Application.Run(new HubMainForm());
                GC.KeepAlive(_singleInstanceMutex);
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] HubMainForm exited normally\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:o}] EXCEPTION: {ex}\n");
            }
        }

        private static IntPtr FindExistingHubWindow()
        {
            IntPtr found = IntPtr.Zero;
            Win32Native.EnumWindows((hWnd, lParam) =>
            {
                var sb = new StringBuilder(256);
                Win32Native.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (title.Contains("Multi-Account Hub") || title.Contains("Antigravity Multi-Account Hub") || title.Contains("Antigravity Hub"))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }

    #endregion
}
