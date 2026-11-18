#[cfg(windows)]
pub mod win32 {
    use windows::core::{GUID, PCWSTR};
    use windows::Win32::Foundation::{BOOL, HWND, LPARAM};
    use windows::Win32::System::Com::StructuredStorage::PROPVARIANT;
    use windows::Win32::UI::Shell::PropertiesSystem::{
        IPropertyStore, SHGetPropertyStoreForWindow, PROPERTYKEY,
    };
    use windows::Win32::UI::WindowsAndMessaging::{
        BringWindowToTop, EnumWindows, GetWindowThreadProcessId, IsWindowVisible,
        SetForegroundWindow, ShowWindow, SW_RESTORE,
    };

    const PKEY_APPUSERMODEL_ID: PROPERTYKEY = PROPERTYKEY {
        fmtid: GUID::from_u128(0x9F4C2855_9F79_4B39_A8D0_E1D42DE1D5F3),
        pid: 5,
    };

    struct FindWindowCtx {
        target_pid: u32,
        found_hwnd: HWND,
    }

    unsafe extern "system" fn enum_window_proc(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let ctx = &mut *(lparam.0 as *mut FindWindowCtx);
        let mut win_pid: u32 = 0;
        GetWindowThreadProcessId(hwnd, Some(&mut win_pid));
        if win_pid == ctx.target_pid && IsWindowVisible(hwnd).as_bool() {
            ctx.found_hwnd = hwnd;
            return BOOL(0);
        }
        BOOL(1)
    }

    pub fn find_main_hwnd_for_pid(pid: u32) -> Option<isize> {
        unsafe {
            let mut ctx = FindWindowCtx {
                target_pid: pid,
                found_hwnd: HWND::default(),
            };
            let _ = EnumWindows(
                Some(enum_window_proc),
                LPARAM(&mut ctx as *mut FindWindowCtx as isize),
            );
            if ctx.found_hwnd.0 as isize != 0 {
                Some(ctx.found_hwnd.0 as isize)
            } else {
                None
            }
        }
    }

    pub fn focus_window_by_pid(pid: u32) -> bool {
        if let Some(raw_hwnd) = find_main_hwnd_for_pid(pid) {
            unsafe {
                let hwnd = HWND(raw_hwnd as *mut core::ffi::c_void);
                let _ = ShowWindow(hwnd, SW_RESTORE);
                let _ = BringWindowToTop(hwnd);
                return SetForegroundWindow(hwnd).as_bool();
            }
        }
        false
    }

    pub fn stamp_window_aumid(pid: u32, aumid: &str) -> bool {
        if let Some(raw_hwnd) = find_main_hwnd_for_pid(pid) {
            unsafe {
                let hwnd = HWND(raw_hwnd as *mut core::ffi::c_void);
                if let Ok(store) = SHGetPropertyStoreForWindow::<_, IPropertyStore>(hwnd) {
                    let wide: Vec<u16> = aumid.encode_utf16().chain(std::iter::once(0)).collect();
                    let pv = PROPVARIANT::from(PCWSTR(wide.as_ptr()).to_string().unwrap_or_default().as_str());
                    if store.SetValue(&PKEY_APPUSERMODEL_ID, &pv).is_ok() {
                        let _ = store.Commit();
                        return true;
                    }
                }
            }
        }
        false
    }
}
