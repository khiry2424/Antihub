using System;
using AntigravityHub;

int passed = 0;
int failed = 0;

void Assert(bool condition, string name)
{
    if (condition)
    {
        passed++;
        Console.WriteLine($"[PASS] {name}");
    }
    else
    {
        failed++;
        Console.Error.WriteLine($"[FAIL] {name}");
    }
}

// 1. Account ID bounds validation
Assert(HubOrchestrator.IsValidAccountId(1), "IsValidAccountId(1) == true");
Assert(HubOrchestrator.IsValidAccountId(2), "IsValidAccountId(2) == true");
Assert(HubOrchestrator.IsValidAccountId(3), "IsValidAccountId(3) == true");
Assert(!HubOrchestrator.IsValidAccountId(0), "IsValidAccountId(0) == false");
Assert(!HubOrchestrator.IsValidAccountId(-1), "IsValidAccountId(-1) == false");
Assert(!HubOrchestrator.IsValidAccountId(4), "IsValidAccountId(4) == false");

// 2. Windows path safety validation
Assert(HubOrchestrator.IsSafeWindowsPath(@"C:\Users\Demo\AppData\Roaming\Antigravity-Profiles"), "Accepts valid absolute path");
Assert(HubOrchestrator.IsSafeWindowsPath(@"E:\Projects\My App"), "Accepts path with spaces");
Assert(!HubOrchestrator.IsSafeWindowsPath(null), "Rejects null path");
Assert(!HubOrchestrator.IsSafeWindowsPath("relative\\path"), "Rejects relative path");
Assert(!HubOrchestrator.IsSafeWindowsPath(@"C:\foo & calc.exe"), "Rejects & shell metacharacter");
Assert(!HubOrchestrator.IsSafeWindowsPath(@"C:\foo|bar"), "Rejects | shell metacharacter");
Assert(!HubOrchestrator.IsSafeWindowsPath("C:\\foo\"bar"), "Rejects quote character");
Assert(!HubOrchestrator.IsSafeWindowsPath("C:\\foo\nbar"), "Rejects newline character");

// 3. Process profile matching avoids workspace name collision
string acc1Profile = @"C:\Users\Demo\AppData\Roaming\Antigravity-Profiles\Account1";
string workspaceCollisionCmd = "\"C:\\Programs\\Antigravity.exe\" --user-data-dir=\"C:\\Users\\Demo\\AppData\\Roaming\\Antigravity-Profiles\\Account2\" \"E:\\Work\\Account1Demo\"".ToLowerInvariant();
string validAcc1Cmd = "\"C:\\Programs\\Antigravity.exe\" --user-data-dir=\"C:\\Users\\Demo\\AppData\\Roaming\\Antigravity-Profiles\\Account1\" \"E:\\Work\\Repo\"".ToLowerInvariant();
string validAcc1ForwardSlash = "--user-data-dir=c:/users/demo/appdata/roaming/antigravity-profiles/account1".ToLowerInvariant();

Assert(!HubOrchestrator.ContainsProfileArg(workspaceCollisionCmd, acc1Profile), "Workspace named Account1Demo does not match Account1 profile");
Assert(HubOrchestrator.ContainsProfileArg(validAcc1Cmd, acc1Profile), "Matches exact backslash profile dir");
Assert(HubOrchestrator.ContainsProfileArg(validAcc1ForwardSlash, acc1Profile), "Matches forward-slash profile dir");

// 4. Config normalization clamps pollIntervalMs and rejects unsafe paths
var orch = new HubOrchestrator();
var badLow = new HubConfig { PollIntervalMs = 0, AntigravityExePath = @"C:\bad & cmd.exe" };
var normLow = orch.NormalizeConfig(badLow);
Assert(normLow.PollIntervalMs == 500, "Clamps PollIntervalMs < 500 up to 500");
Assert(HubOrchestrator.IsSafeWindowsPath(normLow.AntigravityExePath), "Replaces unsafe exe path with safe default");

var badHigh = new HubConfig { PollIntervalMs = 999999 };
Assert(orch.NormalizeConfig(badHigh).PollIntervalMs == 60000, "Clamps PollIntervalMs > 60000 down to 60000");

var validPoll = new HubConfig { PollIntervalMs = 2000 };
Assert(orch.NormalizeConfig(validPoll).PollIntervalMs == 2000, "Preserves valid PollIntervalMs = 2000");

// 5. EnsureDirectoryJunction no-ops safely on invalid inputs
HubOrchestrator.EnsureDirectoryJunction(@"C:\bad & path", @"C:\nonexistent");
Assert(true, "EnsureDirectoryJunction handles unsafe paths without throwing");

Console.WriteLine($"\nSummary: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
