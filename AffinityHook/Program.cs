using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AffinityPluginLoader
{
    internal class Program
    {
        private static Process _affinityProcess;
        
        static int Main(string[] args)
        {
            try
            {
                // Check for --detach flag
                bool detachMode = false;
                var affinityArgs = new System.Collections.Generic.List<string>();
                
                foreach (string arg in args)
                {
                    if (arg.Equals("--detach", StringComparison.OrdinalIgnoreCase))
                    {
                        detachMode = true;
                    }
                    else
                    {
                        affinityArgs.Add(arg);
                    }
                }
                
                // Get the directory where AffinityHook.exe is located
                string hookExeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string hookExeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                
                // Determine Affinity executable and directory
                string affinityDir = null;
                string affinityExe = null;
                
                // Check if we're renamed as Affinity.exe (meaning real Affinity was renamed)
                if (hookExeName.Equals("Affinity.exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Look for Affinity.real.exe in current directory
                    string realExe = Path.Combine(hookExeDir, "Affinity.real.exe");
                    if (File.Exists(realExe))
                    {
                        affinityDir = hookExeDir;
                        affinityExe = realExe;
                        Console.WriteLine("Running as Affinity.exe, found Affinity.real.exe");
                    }
                }
                
                // If not found yet, look for Affinity.exe
                if (affinityExe == null)
                {
                    // First check current directory
                    string localExe = Path.Combine(hookExeDir, "Affinity.exe");
                    if (File.Exists(localExe) && !localExe.Equals(Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase))
                    {
                        affinityDir = hookExeDir;
                        affinityExe = localExe;
                        Console.WriteLine($"Found Affinity.exe in current directory");
                    }
                    else
                    {
                        // Check standard install location
                        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        string standardPath = Path.Combine(programFiles, "Affinity", "Affinity", "Affinity.exe");
                        
                        if (File.Exists(standardPath))
                        {
                            affinityDir = Path.GetDirectoryName(standardPath);
                            affinityExe = standardPath;
                            Console.WriteLine($"Found Affinity.exe in standard install location");
                        }
                    }
                }
                
                // Check if we found Affinity
                if (affinityExe == null)
                {
                    Console.Error.WriteLine("ERROR: Could not find Affinity.exe or Affinity.real.exe");
                    Console.Error.WriteLine($"Searched in:");
                    Console.Error.WriteLine($"  - {hookExeDir}");
                    Console.Error.WriteLine($"  - {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Affinity", "Affinity")}");
                    Console.WriteLine("\nPress Enter to exit...");
                    Console.ReadLine();
                    return 1;
                }
                
                // Check for AffinityPluginLoader.dll
                string pluginLoaderPath = Path.Combine(affinityDir, "AffinityPluginLoader.dll");
                bool hasPluginLoader = File.Exists(pluginLoaderPath);
                
                if (!hasPluginLoader)
                {
                    Console.WriteLine("WARNING: AffinityPluginLoader.dll not found");
                    Console.WriteLine("Launching Affinity without plugin loader...");
                    Console.WriteLine($"Expected location: {pluginLoaderPath}");
                    Console.WriteLine();
                }
                
                Console.WriteLine($"Starting Affinity from: {affinityExe}");
                Console.WriteLine($"Mode: {(detachMode ? "Detached" : "Attached")}");
                
                // Start the process with inherited console
                var startInfo = new ProcessStartInfo
                {
                    FileName = affinityExe,
                    Arguments = string.Join(" ", affinityArgs),
                    WorkingDirectory = affinityDir,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                
                // In attached mode, inherit standard streams so output appears in same terminal
                if (!detachMode)
                {
                    startInfo.RedirectStandardOutput = false;
                    startInfo.RedirectStandardError = false;
                    startInfo.RedirectStandardInput = false;
                }
                
                var process = Process.Start(startInfo);
                _affinityProcess = process;
                
                Console.WriteLine($"Process started (PID {process.Id})");
                
                // Only inject if plugin loader exists
                if (hasPluginLoader)
                {
                    bool isWine = IsRunningOnWine();
                    Console.WriteLine($"Platform: {(isWine ? "Wine" : "Windows")}");
                    Console.WriteLine("Using native bootstrap injection...");
                    
                    InjectBootstrap(process.Id, pluginLoaderPath);
                }
                
                // Wait for Affinity to exit unless in detach mode
                if (!detachMode)
                {
                    // Set up Ctrl+C handler to forward signal to Affinity
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true; // Prevent immediate termination of hook
                        Console.WriteLine("\nReceived Ctrl+C, terminating Affinity...");
                        
                        try
                        {
                            if (_affinityProcess != null && !_affinityProcess.HasExited)
                            {
                                // Try graceful shutdown first
                                if (!_affinityProcess.CloseMainWindow())
                                {
                                    // If that fails, kill it
                                    _affinityProcess.Kill();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Error terminating Affinity: {ex.Message}");
                        }
                    };
                    
                    Console.WriteLine("Waiting for Affinity to exit... (use --detach to skip, Ctrl+C to terminate)");
                    process.WaitForExit();
                    Console.WriteLine($"Affinity exited with code {process.ExitCode}");
                    return process.ExitCode;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("\nPress Enter to exit...");
                Console.ReadLine();
                return 1;
            }
        }
        
        /// <summary>
        /// Detect if running under Wine
        /// </summary>
        private static bool IsRunningOnWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                if (ntdll == IntPtr.Zero)
                    return false;
                
                // Wine has a "wine_get_version" export that Windows doesn't
                IntPtr wineVersion = GetProcAddress(ntdll, "wine_get_version");
                return wineVersion != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Inject using simple CreateRemoteThread + LoadLibrary
        /// This works on both Windows and Wine - we always use the native bootstrap
        /// </summary>
        private static void InjectBootstrap(int processId, string pluginLoaderPath)
        {
            try
            {
                // Use native bootstrap DLL for both Windows and Wine
                string dllDir = Path.GetDirectoryName(pluginLoaderPath);
                string bootstrapPath = Path.Combine(dllDir, "AffinityBootstrap.dll");
                
                // Check if native bootstrap exists
                if (!File.Exists(bootstrapPath))
                {
                    Console.WriteLine($"ERROR: AffinityBootstrap.dll not found at: {bootstrapPath}");
                    Console.WriteLine("The native bootstrap DLL is required for plugin loading.");
                    Console.WriteLine("");
                    Console.WriteLine("To build it:");
                    Console.WriteLine("  Windows: cd AffinityBootstrap && build.bat");
                    Console.WriteLine("  Linux:   cd AffinityBootstrap && ./build.sh");
                    Console.WriteLine("");
                    throw new Exception("Native bootstrap DLL not found");
                }
                
                Console.WriteLine($"Using native bootstrap: {bootstrapPath}");
                
                // Wait a moment for process to initialize
                Thread.Sleep(500);
                
                // Open process with full access
                IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    throw new Exception($"Failed to open process. Error: {Marshal.GetLastWin32Error()}");
                }
                
                try
                {
                    // Get LoadLibraryW address from kernel32
                    IntPtr kernel32 = GetModuleHandle("kernel32.dll");
                    IntPtr loadLibraryAddr = GetProcAddress(kernel32, "LoadLibraryW");
                    
                    if (loadLibraryAddr == IntPtr.Zero)
                    {
                        throw new Exception("Failed to get LoadLibraryW address");
                    }
                    
                    // Allocate memory in target process for DLL path
                    byte[] dllPathBytes = Encoding.Unicode.GetBytes(bootstrapPath + "\0");
                    IntPtr allocMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllPathBytes.Length, 
                        MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                    
                    if (allocMem == IntPtr.Zero)
                    {
                        throw new Exception("Failed to allocate memory in target process");
                    }
                    
                    // Write DLL path to target process
                    if (!WriteProcessMemory(hProcess, allocMem, dllPathBytes, (uint)dllPathBytes.Length, out _))
                    {
                        throw new Exception("Failed to write DLL path to target process");
                    }
                    
                    // Create remote thread that calls LoadLibraryW with our DLL path
                    IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMem, 0, IntPtr.Zero);
                    
                    if (hThread == IntPtr.Zero)
                    {
                        throw new Exception($"Failed to create remote thread. Error: {Marshal.GetLastWin32Error()}");
                    }
                    
                    // Wait for the thread to complete
                    WaitForSingleObject(hThread, 5000); // 5 second timeout
                    
                    // Get thread exit code (should be the HMODULE of loaded DLL)
                    GetExitCodeThread(hThread, out uint exitCode);
                    
                    CloseHandle(hThread);
                    
                    if (exitCode == 0)
                    {
                        throw new Exception("LoadLibrary returned NULL - DLL failed to load");
                    }
                    
                    Console.WriteLine("Native bootstrap injected successfully!");
                    Console.WriteLine("The bootstrap will load AffinityPluginLoader.dll and apply patches.");
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Failed to inject plugin loader: {ex.Message}");
                Console.WriteLine("Affinity is running without plugins.");
                Console.WriteLine("");
                Console.WriteLine("Troubleshooting:");
                Console.WriteLine("  1. Make sure AffinityBootstrap.dll is in the Affinity directory");
                Console.WriteLine("  2. Check that AffinityPluginLoader.dll and 0Harmony.dll are present");
                Console.WriteLine("  3. Run as Administrator if on Windows");
            }
        }
        
        #region P/Invoke Declarations
        
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 0x04;
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, 
            uint flAllocationType, uint flProtect);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, 
            uint nSize, out int lpNumberOfBytesWritten);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, 
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        
        #endregion
    }
}
