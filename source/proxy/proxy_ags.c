// Forward AMD AGS APIs and load existing local DLLs on a background thread.
// Loading a DLL does not prove that its plugin initialization has completed.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdarg.h>
#include <wchar.h>

#include "ags_exports.h"

static const wchar_t *kDlls[] = {
    L"sysdll_Win64_retail.dll",
    L"CardsDLL_Win64_retail.dll",
    L"FootballCompEng_Win64_retail.dll",
    L"powdll_Win64_retail.dll",
    L"CryptBase.dll",
    L"dbdata.dll",
};

static wchar_t g_dir[MAX_PATH];

static void log_line(const wchar_t *fmt, ...) {
    wchar_t path[MAX_PATH];
    _snwprintf_s(path, MAX_PATH, _TRUNCATE, L"%s\\fifa19_proxy.log", g_dir);
    FILE *f = NULL;
    if (_wfopen_s(&f, path, L"a, ccs=UTF-8") != 0 || !f) return;
    va_list ap; va_start(ap, fmt);
    vfwprintf(f, fmt, ap);
    va_end(ap);
    fwprintf(f, L"\n");
    fclose(f);
}

static DWORD WINAPI loader_thread(LPVOID param) {
    (void)param;
    log_line(L"[proxy] loader thread start, dir=%s", g_dir);
    for (size_t i = 0; i < sizeof(kDlls) / sizeof(kDlls[0]); ++i) {
        wchar_t full[MAX_PATH];
        _snwprintf_s(full, MAX_PATH, _TRUNCATE, L"%s\\%s", g_dir, kDlls[i]);
        HMODULE h = LoadLibraryExW(full, NULL, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (h) {
            log_line(L"[proxy] loaded OK   %s (0x%p)", kDlls[i], (void *)h);
        } else {
            log_line(L"[proxy] load FAILED %s err=%lu", kDlls[i], GetLastError());
        }
    }
    log_line(L"[proxy] loader thread done");
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE hinst, DWORD reason, LPVOID reserved) {
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hinst);
        GetModuleFileNameW(hinst, g_dir, MAX_PATH);
        wchar_t *slash = wcsrchr(g_dir, L'\\');
        if (slash) *slash = 0;
        HANDLE t = CreateThread(NULL, 0, loader_thread, NULL, 0, NULL);
        if (t) CloseHandle(t);
    }
    return TRUE;
}
