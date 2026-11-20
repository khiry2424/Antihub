param(
    [string]$AntigravityExe = "$env:LOCALAPPDATA\Programs\Antigravity\Antigravity.exe",
    [string]$SharedAppDataDir = "",
    [string]$ProfilesRoot = "$env:APPDATA\Antigravity-Profiles",
    [switch]$CopyToDesktop = $true
)

$ErrorActionPreference = "Stop"

# Resolve true Windows user profile (never an isolated Account*/Home override)
$RealUserProfile = $env:USERPROFILE
if ([string]::IsNullOrWhiteSpace($RealUserProfile) -or ($RealUserProfile -like "*Antigravity-Profiles*")) {
    $roamingParent = Split-Path -Parent $env:APPDATA
    $RealUserProfile = Split-Path -Parent $roamingParent
}
if ([string]::IsNullOrWhiteSpace($SharedAppDataDir)) {
    $SharedAppDataDir = Join-Path $RealUserProfile ".gemini\antigravity"
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " Antigravity Hub - AUMID Shortcut & Isolated Profile Generator  " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# 1. Ensure Directories Exist
$IconsDir = Join-Path $ProfilesRoot "Icons"
$ShortcutsDir = Join-Path $ProfilesRoot "Shortcuts"
$LaunchersDir = Join-Path $ProfilesRoot "Launchers"
$RepoIconsDir = Join-Path (Split-Path -Parent $PSScriptRoot) "assets\icons"

foreach ($dir in @($ProfilesRoot, $IconsDir, $ShortcutsDir, $LaunchersDir, $RepoIconsDir, $SharedAppDataDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Ensure-DirectoryJunction([string]$link, [string]$target) {
    if (-not (Test-Path $target)) { return }
    if (Test-Path $link) {
        $item = Get-Item $link -Force
        if ($item.Attributes -match "ReparsePoint") { return }
        Remove-Item -Path $link -Recurse -Force
    }
    $parent = Split-Path -Parent $link
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    cmd /c mklink /J "$link" "$target" | Out-Null
}

$RealConfig = "$RealUserProfile\.gemini\config"
$BaseAntigravity = "$env:APPDATA\Antigravity"
$BaseUser = "$BaseAntigravity\User"
$BaseWorkspace = "$BaseUser\workspaceStorage"
$BaseSnippets = "$BaseUser\snippets"
$BaseGlobal = "$BaseUser\globalStorage"
$BaseAppStorage = "$BaseAntigravity\app_storage.json"

for ($i = 1; $i -le 3; $i++) {
    $accDir = Join-Path $ProfilesRoot "Account$i"
    $userDir = Join-Path $accDir "User"
    $globalDir = Join-Path $userDir "globalStorage"
    $homeDir = Join-Path $accDir "Home"
    $geminiDir = Join-Path $homeDir ".gemini"
    $cacheDir = Join-Path $geminiDir "cache"

    foreach ($d in @($accDir, $userDir, $globalDir, $homeDir, $geminiDir, $cacheDir)) {
        if (-not (Test-Path $d)) {
            New-Item -ItemType Directory -Path $d -Force | Out-Null
        }
    }

    # Force language_server to bypass global Windows Credential Manager
    $markerPath = Join-Path $cacheDir "antigravity-keyring-unavailable"
    if (-not (Test-Path $markerPath)) {
        [System.IO.File]::WriteAllText($markerPath, [DateTime]::UtcNow.ToString("o"), (New-Object System.Text.UTF8Encoding($false)))
    }

    # 1. NTFS Junction for Shared Brain (.gemini/antigravity)
    Ensure-DirectoryJunction (Join-Path $geminiDir "antigravity") $SharedAppDataDir

    # 2. NTFS Junction for Shared Config & Projects (.gemini/config)
    if (Test-Path $RealConfig) {
        Ensure-DirectoryJunction (Join-Path $geminiDir "config") $RealConfig
    }

    # 3. NTFS Junction for Shared WorkspaceStorage (User/workspaceStorage)
    if (Test-Path $BaseWorkspace) {
        Ensure-DirectoryJunction (Join-Path $userDir "workspaceStorage") $BaseWorkspace
    }

    # 4. NTFS Junction for Shared Snippets (User/snippets)
    if (Test-Path $BaseSnippets) {
        Ensure-DirectoryJunction (Join-Path $userDir "snippets") $BaseSnippets
    }

    # 5. Sync Settings & Keybindings
    if (Test-Path "$BaseUser\settings.json") {
        Copy-Item "$BaseUser\settings.json" (Join-Path $userDir "settings.json") -Force
    }
    if (Test-Path "$BaseUser\keybindings.json") {
        Copy-Item "$BaseUser\keybindings.json" (Join-Path $userDir "keybindings.json") -Force
    }

    # 6. Sync globalStorage (storage.json & state.vscdb)
    if (Test-Path $BaseGlobal) {
        Copy-Item "$BaseGlobal\storage.json" (Join-Path $globalDir "storage.json") -Force -ErrorAction SilentlyContinue
        $targetDb = Join-Path $globalDir "state.vscdb"
        if (-not (Test-Path $targetDb)) {
            Copy-Item "$BaseGlobal\state.vscdb" $targetDb -Force -ErrorAction SilentlyContinue
        }
    }

    # 7. Sync app_storage.json (Projects order & sidebar UI) preserving per-account auth (Strict UTF-8 without BOM)
    if (Test-Path $BaseAppStorage) {
        $targetAppStorage = Join-Path $accDir "app_storage.json"
        $tokenFile = Join-Path $geminiDir "jetski-standalone-oauth-token"
        $hasToken = (Test-Path $tokenFile) -and ((Get-Item $tokenFile).Length -gt 10)

        $existingUser = $null
        $existingTos = $null
        $existingAgyUser = $null
        $existingAgyTos = $null
        $targetObj = $null
        if (Test-Path $targetAppStorage) {
            try {
                $rawExisting = [System.IO.File]::ReadAllText($targetAppStorage).TrimStart([char]0xFEFF)
                $targetObj = $rawExisting | ConvertFrom-Json
                if ($hasToken) {
                    if ($targetObj."jetski.onboarding.lastLoginUsername") {
                        $existingUser = $targetObj."jetski.onboarding.lastLoginUsername"
                    }
                    if ($targetObj."jetski.onboarding.lastLoginIsGcpTos") {
                        $existingTos = $targetObj."jetski.onboarding.lastLoginIsGcpTos"
                    }
                    if ($targetObj."agy.onboarding.lastLoginUsername") {
                        $existingAgyUser = $targetObj."agy.onboarding.lastLoginUsername"
                    }
                    if ($targetObj."agy.onboarding.lastLoginIsGcpTos") {
                        $existingAgyTos = $targetObj."agy.onboarding.lastLoginIsGcpTos"
                    }
                }
            } catch {}
        }
        $mainRaw = [System.IO.File]::ReadAllText($BaseAppStorage).TrimStart([char]0xFEFF)
        $mainJson = $mainRaw | ConvertFrom-Json
        $baseToUse = if ($targetObj) { $targetObj } else { $mainJson }
        $merged = [ordered]@{}
        foreach ($prop in $baseToUse.PSObject.Properties) {
            $merged[$prop.Name] = $prop.Value
        }
        if ($mainJson."projectsOrder") {
            $merged["projectsOrder"] = $mainJson."projectsOrder"
        }
        if ($mainJson."sidebar_collapsed_sections") {
            $merged["sidebar_collapsed_sections"] = $mainJson."sidebar_collapsed_sections"
        }

        if ($hasToken -and ($existingUser -or $existingAgyUser)) {
            if ($existingUser) {
                $merged["jetski.onboarding.lastLoginUsername"] = $existingUser
            }
            if ($existingTos) {
                $merged["jetski.onboarding.lastLoginIsGcpTos"] = $existingTos
            }
            if ($existingAgyUser) {
                $merged["agy.onboarding.lastLoginUsername"] = $existingAgyUser
            }
            if ($existingAgyTos) {
                $merged["agy.onboarding.lastLoginIsGcpTos"] = $existingAgyTos
            }
        } else {
            $merged.Remove("jetski.onboarding.lastLoginUsername")
            $merged.Remove("jetski.onboarding.lastLoginIsGcpTos")
            $merged.Remove("agy.onboarding.lastLoginUsername")
            $merged.Remove("agy.onboarding.lastLoginIsGcpTos")
        }
        $merged["ide-install-wizard-shown"] = "true"
        $jsonOutput = $merged | ConvertTo-Json -Depth 10
        [System.IO.File]::WriteAllText($targetAppStorage, $jsonOutput, (New-Object System.Text.UTF8Encoding($false)))
    }
}

# 2. Compile C# Icon Generator + Win32 COM IShellLinkW / IPropertyStore Shortcut Builder
$CSharpSource = @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace AntigravityShortcutBuilder
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    internal interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PropVariant pv);
        void SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        void Commit();
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

    public static class Builder
    {
        // System.AppUserModel.ID = {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5
        private static readonly Guid AppUserModelGuid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, string dwItem1, IntPtr dwItem2);

        public static void RefreshShellFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    SHChangeNotify(0x00001000, 0x0005, folderPath, IntPtr.Zero); // SHCNE_UPDATEDIR | SHCNF_PATHW
                }
                SHChangeNotify(0x08000000, 0x0000, null, IntPtr.Zero); // SHCNE_ASSOCCHANGED
            }
            catch { }
        }

        private static void SetPropertyString(IPropertyStore store, Guid guid, uint pid, string val)
        {
            PROPERTYKEY pkey = new PROPERTYKEY(guid, pid);
            PropVariant pv = PropVariant.FromString(val);
            try
            {
                store.SetValue(ref pkey, ref pv);
            }
            finally
            {
                pv.Dispose();
            }
        }

        public static void CreateAumidShortcut(
            string shortcutPath,
            string targetPath,
            string arguments,
            string workingDirectory,
            string description,
            string iconPath,
            string aumid,
            string relaunchDisplayName)
        {
            IShellLinkW link = (IShellLinkW)new ShellLink();
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetDescription(description);
            if (!string.IsNullOrEmpty(iconPath))
            {
                link.SetIconLocation(iconPath, 0);
            }

            IPropertyStore propStore = (IPropertyStore)link;

            // 1. PKEY_AppUserModel_ID (pid 5)
            SetPropertyString(propStore, AppUserModelGuid, 5, aumid);

            // 2. PKEY_AppUserModel_RelaunchCommand (pid 2)
            string fullCmd = "\"" + targetPath + "\" " + arguments;
            SetPropertyString(propStore, AppUserModelGuid, 2, fullCmd);

            // 3. PKEY_AppUserModel_RelaunchDisplayNameResource (pid 4)
            SetPropertyString(propStore, AppUserModelGuid, 4, relaunchDisplayName);

            // 4. PKEY_AppUserModel_RelaunchIconResource (pid 3)
            if (!string.IsNullOrEmpty(iconPath))
            {
                SetPropertyString(propStore, AppUserModelGuid, 3, iconPath + ",0");
            }

            propStore.Commit();
            ((IPersistFile)link).Save(shortcutPath, true);
        }

        public static void GenerateHubMasterIcon(string outputPath)
        {
            int size = 256;
            using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                gfx.Clear(Color.Transparent);

                Color emerald = Color.FromArgb(255, 16, 185, 129);
                Color emeraldBright = Color.FromArgb(255, 52, 211, 153);
                Color darkBg = Color.FromArgb(255, 8, 16, 24);

                // Distinct dark emerald-obsidian rounded frame
                using (GraphicsPath path = CreateRoundedRect(12, 12, 232, 232, 64))
                {
                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                        new Rectangle(0, 0, size, size),
                        Color.FromArgb(255, 14, 34, 38),
                        darkBg,
                        45f))
                    {
                        gfx.FillPath(bgBrush, path);
                    }
                    using (Pen borderPen = new Pen(emerald, 11f))
                    {
                        gfx.DrawPath(borderPen, path);
                    }
                }

                // Draw 3 stacked isometric "Hub Layers" (bottom -> middle -> top)
                Action<float, Color, Color, float> drawLayer = (centerY, cTop, cBot, alphaFill) =>
                {
                    PointF[] diamond = new PointF[]
                    {
                        new PointF(128, centerY - 30),
                        new PointF(200, centerY),
                        new PointF(128, centerY + 30),
                        new PointF(56, centerY)
                    };
                    using (LinearGradientBrush fill = new LinearGradientBrush(
                        new RectangleF(56, centerY - 30, 144, 60),
                        cTop,
                        cBot,
                        90f))
                    {
                        gfx.FillPolygon(fill, diamond);
                    }
                    using (Pen edge = new Pen(Color.FromArgb((int)(alphaFill * 255), 236, 253, 245), 4.5f))
                    {
                        edge.LineJoin = LineJoin.Round;
                        gfx.DrawPolygon(edge, diamond);
                    }
                };

                // Bottom layer (Account 3 tint / deep teal)
                drawLayer(168f, Color.FromArgb(220, 6, 95, 70), Color.FromArgb(240, 4, 60, 46), 0.45f);
                // Middle layer (Emerald)
                drawLayer(130f, Color.FromArgb(240, 16, 185, 129), Color.FromArgb(245, 5, 150, 105), 0.75f);
                // Top layer (Bright Emerald-White)
                drawLayer(92f, Color.FromArgb(255, 236, 253, 245), emeraldBright, 0.95f);

                // Small 3-node status dots along bottom arc representing 3 concurrent accounts
                int dotSize = 18;
                int dotY = 204;
                Color[] dotColors = new Color[]
                {
                    Color.FromArgb(255, 56, 189, 248),  // Account 1 Cyan
                    Color.FromArgb(255, 167, 139, 250), // Account 2 Violet
                    Color.FromArgb(255, 251, 191, 36)   // Account 3 Amber
                };
                int[] dotX = new int[] { 86, 119, 152 };
                for (int i = 0; i < 3; i++)
                {
                    using (SolidBrush db = new SolidBrush(dotColors[i]))
                    {
                        gfx.FillEllipse(db, dotX[i], dotY, dotSize, dotSize);
                    }
                }

                SaveBitmapAsMultiSizeIcon(bmp, outputPath);
            }
        }

        public static void GenerateBadgeIcon(string outputPath, int accountId, int r, int g, int b)
        {
            int size = 256;
            using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                gfx.Clear(Color.Transparent);

                Color accent = Color.FromArgb(255, r, g, b);
                Color darkBg = Color.FromArgb(255, 10, 15, 28);

                // Outer rounded shield
                using (GraphicsPath path = CreateRoundedRect(12, 12, 232, 232, 54))
                {
                    using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                        new Rectangle(0, 0, size, size),
                        Color.FromArgb(255, 18, 26, 46),
                        darkBg,
                        45f))
                    {
                        gfx.FillPath(bgBrush, path);
                    }
                    using (Pen borderPen = new Pen(accent, 10f))
                    {
                        gfx.DrawPath(borderPen, path);
                    }
                }

                // Unified geometric "A" symbol with seamless integrated crossbar
                using (GraphicsPath aPath = new GraphicsPath(FillMode.Alternate))
                {
                    PointF[] outerA = new PointF[]
                    {
                        new PointF(128, 46),
                        new PointF(196, 180),
                        new PointF(164, 180),
                        new PointF(153, 154),
                        new PointF(103, 154),
                        new PointF(92, 180),
                        new PointF(60, 180)
                    };
                    PointF[] innerCutout = new PointF[]
                    {
                        new PointF(128, 94),
                        new PointF(144, 132),
                        new PointF(112, 132)
                    };
                    aPath.AddPolygon(outerA);
                    aPath.AddPolygon(innerCutout);

                    using (LinearGradientBrush deltaBrush = new LinearGradientBrush(
                        new Rectangle(46, 46, 160, 140),
                        Color.White,
                        accent,
                        90f))
                    {
                        gfx.FillPath(deltaBrush, aPath);
                    }
                }

                // Account Number Badge Pill in bottom-right corner
                if (accountId > 0)
                {
                    Rectangle badgeRect = new Rectangle(150, 150, 90, 90);
                    using (SolidBrush badgeBg = new SolidBrush(accent))
                    using (Pen badgeBorder = new Pen(darkBg, 8f))
                    {
                        gfx.FillEllipse(badgeBg, badgeRect);
                        gfx.DrawEllipse(badgeBorder, badgeRect);
                    }

                    using (Font font = new Font("Segoe UI", 42, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(255, 8, 12, 22)))
                    {
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        gfx.DrawString(accountId.ToString(), font, textBrush, new RectangleF(150, 152, 90, 90), sf);
                    }
                }

                SaveBitmapAsMultiSizeIcon(bmp, outputPath);
            }
        }

        private static GraphicsPath CreateRoundedRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void SaveBitmapAsMultiSizeIcon(Bitmap sourceBmp, string icoPath)
        {
            int[] sizes = new int[] { 256, 64, 48, 32, 16 };
            byte[][] pngFrames = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int targetSize = sizes[i];
                if (targetSize == sourceBmp.Width && targetSize == sourceBmp.Height)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        sourceBmp.Save(ms, ImageFormat.Png);
                        pngFrames[i] = ms.ToArray();
                    }
                }
                else
                {
                    using (Bitmap resized = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(sourceBmp, 0, 0, targetSize, targetSize);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            resized.Save(ms, ImageFormat.Png);
                            pngFrames[i] = ms.ToArray();
                        }
                    }
                }
            }

            using (FileStream fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);               // reserved
                bw.Write((ushort)1);               // type: 1 = ICO
                bw.Write((ushort)sizes.Length);    // image count

                uint offset = (uint)(6 + 16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                    bw.Write(dim);                         // width
                    bw.Write(dim);                         // height
                    bw.Write((byte)0);                     // colors
                    bw.Write((byte)0);                     // reserved
                    bw.Write((ushort)1);                   // color planes
                    bw.Write((ushort)32);                  // bits per pixel
                    bw.Write((uint)pngFrames[i].Length);   // byte size
                    bw.Write(offset);                      // image data offset
                    offset += (uint)pngFrames[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngFrames[i]);
                }
            }
        }
    }
}
"@

Add-Type -TypeDefinition $CSharpSource -ReferencedAssemblies "System.Drawing"

# 3. Generate Master Hub Icon & 3 Account Icons
$MasterIcon = Join-Path $IconsDir "AntigravityHub.ico"
[AntigravityShortcutBuilder.Builder]::GenerateHubMasterIcon($MasterIcon)
Copy-Item $MasterIcon (Join-Path $RepoIconsDir "AntigravityHub.ico") -Force

$AccountsMeta = @(
    @{ Id = 1; Label = "Personal"; R = 56;  G = 189; B = 248; Aumid = "Antigravity.Account.1" },
    @{ Id = 2; Label = "Work";     R = 167; G = 139; B = 250; Aumid = "Antigravity.Account.2" },
    @{ Id = 3; Label = "Secondary";R = 251; G = 191; B = 36;  Aumid = "Antigravity.Account.3" }
)

foreach ($acc in $AccountsMeta) {
    $id = $acc.Id
    $icoPath = Join-Path $IconsDir "Account$id.ico"
    [AntigravityShortcutBuilder.Builder]::GenerateBadgeIcon($icoPath, $id, $acc.R, $acc.G, $acc.B)
    Copy-Item $icoPath (Join-Path $RepoIconsDir "Account$id.ico") -Force
    Write-Host "[+] Generated custom icon: $icoPath" -ForegroundColor Green

    $profilePath = Join-Path $ProfilesRoot "Account$id"
    $shortcutName = "Antigravity - Account $id ($($acc.Label)).lnk"
    $shortcutPath = Join-Path $ShortcutsDir $shortcutName

    # Compile isolated standalone GUI launcher executable
    $launcherExe = Join-Path $LaunchersDir "Antigravity-Account$id.exe"
    $launcherSrc = @"
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AccountLauncher$id
{
    internal static class Program
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        private static readonly char[] ForbiddenShellChars = new char[] { '&', '|', '<', '>', '^', '%', '"', '\r', '\n', '\0' };

        private static bool IsSafePath(string p)
        {
            return !string.IsNullOrWhiteSpace(p) && p.IndexOfAny(ForbiddenShellChars) < 0 && Path.IsPathRooted(p);
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            if (arg.IndexOfAny(new char[] { ' ', '\t', '"', '\n', '\v' }) < 0 && !arg.EndsWith("\\")) return arg;
            var sb = new System.Text.StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0) sb.Append('\\', backslashes);
                    backslashes = 0;
                    sb.Append(c);
                }
            }
            if (backslashes > 0) sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        private static void EnsureDirectoryJunction(string linkPath, string targetPath)
        {
            if (!IsSafePath(linkPath) || !IsSafePath(targetPath) || !Directory.Exists(targetPath)) return;
            try
            {
                string fullLink = Path.GetFullPath(linkPath);
                string fullTarget = Path.GetFullPath(targetPath);
                if (!IsSafePath(fullLink) || !IsSafePath(fullTarget)) return;
                if (Directory.Exists(fullLink))
                {
                    var di = new DirectoryInfo(fullLink);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return;
                    Directory.Delete(fullLink, true);
                }
                string parent = Path.GetDirectoryName(fullLink);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                var psi = new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + fullLink + "\" \"" + fullTarget + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(3000);
                }
            }
            catch { }
        }

        private static string StripStaleOnboarding(string content)
        {
            content = Regex.Replace(content, "\"(?:jetski|agy)\\.onboarding\\.lastLoginUsername\"\\s*:\\s*\"[^\"]*\",?\\r?\\n?", "");
            content = Regex.Replace(content, "\"(?:jetski|agy)\\.onboarding\\.lastLoginIsGcpTos\"\\s*:\\s*\"[^\"]*\",?\\r?\\n?", "");
            return content;
        }

        private static void SyncState(string profileDir, string homeDir, string sharedAppData)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (userProfile.IndexOf("Antigravity-Profiles", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var parent1 = Directory.GetParent(appData);
                    var parent2 = parent1 != null ? parent1.Parent : null;
                    if (parent2 != null) userProfile = parent2.FullName;
                }
                string realConfig = Path.Combine(userProfile, ".gemini", "config");
                string geminiDir = Path.Combine(homeDir, ".gemini");
                string mainAntigravity = Path.Combine(appData, "Antigravity");
                string mainWs = Path.Combine(mainAntigravity, "User", "workspaceStorage");
                string mainSnippets = Path.Combine(mainAntigravity, "User", "snippets");

                EnsureDirectoryJunction(Path.Combine(geminiDir, "antigravity"), sharedAppData);
                if (Directory.Exists(realConfig)) EnsureDirectoryJunction(Path.Combine(geminiDir, "config"), realConfig);
                if (Directory.Exists(mainWs)) EnsureDirectoryJunction(Path.Combine(profileDir, "User", "workspaceStorage"), mainWs);
                if (Directory.Exists(mainSnippets)) EnsureDirectoryJunction(Path.Combine(profileDir, "User", "snippets"), mainSnippets);

                string tokenFile = Path.Combine(geminiDir, "jetski-standalone-oauth-token");
                bool hasToken = File.Exists(tokenFile) && new FileInfo(tokenFile).Length > 10;

                string targetStorage = Path.Combine(profileDir, "app_storage.json");
                string mainStorage = Path.Combine(mainAntigravity, "app_storage.json");

                if (!File.Exists(targetStorage) && File.Exists(mainStorage))
                {
                    string content = StripStaleOnboarding(File.ReadAllText(mainStorage));
                    File.WriteAllText(targetStorage, content, new System.Text.UTF8Encoding(false));
                }
                else if (File.Exists(targetStorage) && !hasToken)
                {
                    string content = File.ReadAllText(targetStorage);
                    if (content.Contains("jetski.onboarding.lastLoginUsername") || content.Contains("agy.onboarding.lastLoginUsername"))
                    {
                        content = StripStaleOnboarding(content);
                        File.WriteAllText(targetStorage, content, new System.Text.UTF8Encoding(false));
                    }
                }
            }
            catch { }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            SetCurrentProcessExplicitAppUserModelID("$($acc.Aumid)");
            string profileDir = @"$profilePath";
            string homeDir = Path.Combine(profileDir, "Home");
            string cacheDir = Path.Combine(homeDir, ".gemini", "cache");
            Directory.CreateDirectory(cacheDir);
            try { File.WriteAllText(Path.Combine(cacheDir, "antigravity-keyring-unavailable"), DateTime.UtcNow.ToString("o"), new System.Text.UTF8Encoding(false)); } catch { }

            SyncState(profileDir, homeDir, @"$SharedAppDataDir");

            string antigravityExe = @"$AntigravityExe";
            var argList = new System.Collections.Generic.List<string>();
            argList.Add(QuoteArg("--user-data-dir=" + profileDir));
            foreach (string a in args) argList.Add(QuoteArg(a));
            string fullArgs = string.Join(" ", argList);

            var psi = new ProcessStartInfo(antigravityExe, fullArgs);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetDirectoryName(antigravityExe);
            psi.EnvironmentVariables["SSH_CONNECTION"] = "127.0.0.1 22 127.0.0.1 22";
            psi.EnvironmentVariables["SSH_CLIENT"] = "127.0.0.1 22 22";
            psi.EnvironmentVariables["SSH_TTY"] = "/dev/pts/0";
            psi.EnvironmentVariables["USERPROFILE"] = homeDir;
            psi.EnvironmentVariables["HOME"] = homeDir;
            psi.EnvironmentVariables["ANTIGRAVITY_APP_DATA_DIR"] = @"$SharedAppDataDir";
            psi.EnvironmentVariables["ANTIGRAVITY_ACCOUNT_ID"] = "$id";
            psi.EnvironmentVariables["ANTIGRAVITY_AUMID"] = "$($acc.Aumid)";

            Process.Start(psi);
        }
    }
}
"@
    Add-Type -TypeDefinition $launcherSrc -OutputAssembly $launcherExe -OutputType WindowsApplication

    [AntigravityShortcutBuilder.Builder]::CreateAumidShortcut(
        $shortcutPath,
        $launcherExe,
        "",
        $profilePath,
        "Google Antigravity - Account $id ($($acc.Label)) [Isolated Auth + Shared Brain]",
        $icoPath,
        $acc.Aumid,
        "Antigravity Account $id ($($acc.Label))"
    )
    Write-Host "[+] Created AUMID Shortcut ($($acc.Aumid)): $shortcutPath" -ForegroundColor Cyan

    $startMenuRoot = if ($env:APPDATA) { Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs" } else { [Environment]::GetFolderPath("Programs") }
    if (-not [string]::IsNullOrWhiteSpace($startMenuRoot)) {
        $startMenuDir = Join-Path $startMenuRoot "Antigravity Hub"
        New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
        Copy-Item $shortcutPath (Join-Path $startMenuDir $shortcutName) -Force
    }

    if ($CopyToDesktop) {
        $desktopDir = [Environment]::GetFolderPath("Desktop")
        if (Test-Path $desktopDir) {
            Copy-Item $shortcutPath (Join-Path $desktopDir $shortcutName) -Force
        }
    }
}

# 3b. Create Start Menu (& optional Desktop) shortcut for Antigravity Hub itself (enables Pin to Start)
$repoRoot = Split-Path -Parent $PSScriptRoot
$hubExe = Join-Path $repoRoot "release\AntigravityHub.exe"
if (-not (Test-Path $hubExe)) {
    $hubExe = Join-Path (Split-Path -Parent $repoRoot) "release\AntigravityHub.exe"
}
$hubIcon = Join-Path $repoRoot "assets\icons\AntigravityHub.ico"
if (-not (Test-Path $hubIcon)) {
    $hubIcon = Join-Path $IconsDir "AntigravityHub.ico"
}
if (Test-Path $hubExe) {
    $hubShortcutName = "Antigravity Hub.lnk"
    $hubShortcutPath = Join-Path $ShortcutsDir $hubShortcutName
    [AntigravityShortcutBuilder.Builder]::CreateAumidShortcut(
        $hubShortcutPath,
        $hubExe,
        "",
        (Split-Path -Parent $hubExe),
        "Antigravity Multi-Account Hub Dashboard",
        $(if (Test-Path $hubIcon) { $hubIcon } else { $hubExe }),
        "Antigravity.MultiAccount.Hub",
        "Antigravity Hub"
    )
    Write-Host "[+] Created Hub Shortcut: $hubShortcutPath" -ForegroundColor Green

    $startMenuRoot = if ($env:APPDATA) { Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs" } else { [Environment]::GetFolderPath("Programs") }
    if (-not [string]::IsNullOrWhiteSpace($startMenuRoot)) {
        $startMenuDir = Join-Path $startMenuRoot "Antigravity Hub"
        New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
        Copy-Item $hubShortcutPath (Join-Path $startMenuDir $hubShortcutName) -Force
        Write-Host "[+] Added to Start Menu (Pin to Start ready): $startMenuDir\$hubShortcutName" -ForegroundColor Green
    }

    if ($CopyToDesktop) {
        $desktopDir = [Environment]::GetFolderPath("Desktop")
        if (Test-Path $desktopDir) {
            Copy-Item $hubShortcutPath (Join-Path $desktopDir $hubShortcutName) -Force
        }
    }
}

# 4. Persist User-Level Environment Variable ANTIGRAVITY_APP_DATA_DIR so even direct shortcut launches share the brain
[Environment]::SetEnvironmentVariable("ANTIGRAVITY_APP_DATA_DIR", $SharedAppDataDir, "User")
$env:ANTIGRAVITY_APP_DATA_DIR = $SharedAppDataDir
Write-Host "[+] Enforced ANTIGRAVITY_APP_DATA_DIR = $SharedAppDataDir" -ForegroundColor Green

$smFolder = if ($env:APPDATA) { Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Antigravity Hub" } else { $null }
if ($smFolder) {
    [AntigravityShortcutBuilder.Builder]::RefreshShellFolder($smFolder)
}

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " All 3 AUMID Shortcuts & Isolated Profiles Ready!              " -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
