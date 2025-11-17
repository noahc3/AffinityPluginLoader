/*
 * AffinityBootstrap - Native Wine-compatible bootstrap DLL
 * 
 * This native DLL is loaded by Wine and initializes the managed AffinityPluginLoader.dll
 * 
 * Build with MinGW:
 *   x86_64-w64-mingw32-gcc -shared -o AffinityBootstrap.dll bootstrap.c -lole32 -loleaut32 -luuid
 * 
 * Build with Visual Studio:
 *   cl.exe /LD bootstrap.c ole32.lib oleaut32.lib mscoree.lib /Fe:AffinityBootstrap.dll
 */

#define _WIN32_WINNT 0x0600

// Define INITGUID before including headers to create GUID definitions
#define INITGUID

#include <windows.h>
#include <stdio.h>
#include <initguid.h>

// COM interfaces for .NET hosting
#include <metahost.h>  // This has ICLRMetaHost, ICLRRuntimeInfo, etc.

// Need to link these libraries (for Visual Studio)
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "oleaut32.lib")
#pragma comment(lib, "mscoree.lib")

// Function to initialize the managed DLL
static DWORD WINAPI InitializeManagedLoader(LPVOID lpParam)
{
    HRESULT hr;
    ICLRMetaHost *pMetaHost = NULL;
    ICLRRuntimeInfo *pRuntimeInfo = NULL;
    ICLRRuntimeHost *pClrHost = NULL;
    DWORD dwRet = 0;
    
    // Small delay to let DllMain complete
    Sleep(100);
    
    OutputDebugStringW(L"AffinityBootstrap: Starting initialization...\n");
    
    // Get the directory where this DLL is located
    WCHAR dllPath[MAX_PATH];
    HMODULE hModule = NULL;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | 
                       GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       (LPCWSTR)&InitializeManagedLoader,
                       &hModule);
    GetModuleFileNameW(hModule, dllPath, MAX_PATH);
    
    // Get directory
    WCHAR *lastSlash = wcsrchr(dllPath, L'\\');
    if (lastSlash) *lastSlash = L'\0';
    
    // Build path to AffinityPluginLoader.dll
    WCHAR loaderPath[MAX_PATH];
    _snwprintf_s(loaderPath, MAX_PATH, _TRUNCATE, L"%s\\AffinityPluginLoader.dll", dllPath);
    
    // Debug output
    WCHAR debugMsg[MAX_PATH + 50];
    _snwprintf_s(debugMsg, sizeof(debugMsg)/sizeof(WCHAR), _TRUNCATE, 
                 L"AffinityBootstrap: Looking for: %s\n", loaderPath);
    OutputDebugStringW(debugMsg);
    
    // Initialize CLR
    hr = CLRCreateInstance(&CLSID_CLRMetaHost, &IID_ICLRMetaHost, (LPVOID*)&pMetaHost);
    if (FAILED(hr)) {
        _snwprintf_s(debugMsg, sizeof(debugMsg)/sizeof(WCHAR), _TRUNCATE,
                     L"AffinityBootstrap: Failed to create CLR instance (0x%08X)\n", hr);
        OutputDebugStringW(debugMsg);
        return 1;
    }
    
    OutputDebugStringW(L"AffinityBootstrap: CLR instance created\n");
    
    // Get .NET Framework 4.0 runtime
    hr = pMetaHost->lpVtbl->GetRuntime(pMetaHost, L"v4.0.30319", &IID_ICLRRuntimeInfo, (LPVOID*)&pRuntimeInfo);
    if (FAILED(hr)) {
        _snwprintf_s(debugMsg, sizeof(debugMsg)/sizeof(WCHAR), _TRUNCATE,
                     L"AffinityBootstrap: Failed to get runtime info (0x%08X)\n", hr);
        OutputDebugStringW(debugMsg);
        pMetaHost->lpVtbl->Release(pMetaHost);
        return 1;
    }
    
    OutputDebugStringW(L"AffinityBootstrap: Runtime info obtained\n");
    
    // Get runtime host
    hr = pRuntimeInfo->lpVtbl->GetInterface(pRuntimeInfo, &CLSID_CLRRuntimeHost, &IID_ICLRRuntimeHost, (LPVOID*)&pClrHost);
    if (FAILED(hr)) {
        _snwprintf_s(debugMsg, sizeof(debugMsg)/sizeof(WCHAR), _TRUNCATE,
                     L"AffinityBootstrap: Failed to get runtime host (0x%08X)\n", hr);
        OutputDebugStringW(debugMsg);
        pRuntimeInfo->lpVtbl->Release(pRuntimeInfo);
        pMetaHost->lpVtbl->Release(pMetaHost);
        return 1;
    }
    
    OutputDebugStringW(L"AffinityBootstrap: Runtime host obtained\n");
    
    // Start CLR
    hr = pClrHost->lpVtbl->Start(pClrHost);
    if (FAILED(hr) && hr != S_FALSE) { // S_FALSE means already started
        _snwprintf_s(debugMsg, sizeof(debugMsg)/sizeof(WCHAR), _TRUNCATE,
                     L"AffinityBootstrap: Failed to start CLR (0x%08X)\n", hr);
        OutputDebugStringW(debugMsg);
        pClrHost->lpVtbl->Release(pClrHost);
        pRuntimeInfo->lpVtbl->Release(pRuntimeInfo);
        pMetaHost->lpVtbl->Release(pMetaHost);
        return 1;
    }
    
    OutputDebugStringW(L"AffinityBootstrap: CLR started\n");
    
    // Execute managed code
    // Call AffinityPluginLoader.EntryPoint.Initialize() 
    hr = pClrHost->lpVtbl->ExecuteInDefaultAppDomain(
        pClrHost,
        loaderPath,
        L"AffinityPluginLoader.EntryPoint",
        L"Initialize",
        L"",
        &dwRet
    );
    
    if (FAILED(hr)) {
        _snwprintf_s(debugMsg, sizeof(debugMsg)/sizeof(WCHAR), _TRUNCATE,
                     L"AffinityBootstrap: Failed to execute managed code (0x%08X)\n", hr);
        OutputDebugStringW(debugMsg);
        
        // Common error codes
        if (hr == 0x80070002) {
            OutputDebugStringW(L"AffinityBootstrap: ERROR - AffinityPluginLoader.dll not found!\n");
        } else if (hr == 0x80131513) {
            OutputDebugStringW(L"AffinityBootstrap: ERROR - Method not found in assembly!\n");
        }
    } else {
        OutputDebugStringW(L"AffinityBootstrap: Successfully initialized managed loader!\n");
    }
    
    // Don't release - keep CLR running
    // pClrHost->lpVtbl->Release(pClrHost);
    // pRuntimeInfo->lpVtbl->Release(pRuntimeInfo);
    // pMetaHost->lpVtbl->Release(pMetaHost);
    
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    if (fdwReason == DLL_PROCESS_ATTACH)
    {
        OutputDebugStringW(L"AffinityBootstrap: DLL loaded\n");
        
        // Disable thread notifications for better performance
        DisableThreadLibraryCalls(hinstDLL);
        
        // Initialize in a new thread to avoid blocking DllMain
        HANDLE hThread = CreateThread(NULL, 0, InitializeManagedLoader, NULL, 0, NULL);
        
        if (hThread) {
            CloseHandle(hThread);
        } else {
            OutputDebugStringW(L"AffinityBootstrap: Failed to create initialization thread!\n");
        }
    }
    
    return TRUE;
}
