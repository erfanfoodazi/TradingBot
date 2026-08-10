using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Trading.Infrastructure.Options;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Launches the Python FastAPI backend (uvicorn) alongside the WPF app and
/// shuts it down when the app exits.
/// </summary>
public sealed class PythonApiHost : IDisposable
{
    // KILL_ON_JOB_CLOSE: when the last handle to the job object is closed
    // (which happens automatically when this app process terminates), every
    // process still in the job is terminated by the OS. This guarantees the
    // Python backend dies together with the WPF app, even on crash/kill.
    private const uint KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectBasicLimitInformationClass = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private readonly PythonApiOptions _options;
    private Process? _process;
    private IntPtr _job;
    private bool _started;

    public PythonApiHost(IOptions<PythonApiOptions> options)
    {
        _options = options.Value;
    }

    public bool IsRunning => _process is not null && !_process.HasExited;

    public void Start()
    {
        if (_started || !_options.AutoStartPython)
            return;
        _started = true;

        var workingDir = ResolveWorkingDirectory();
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
        {
            Debug.WriteLine("PythonApiHost: 'Trading.Python' folder not found; skipping auto-start.");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(workingDir),
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("uvicorn");
            psi.ArgumentList.Add("main:app");
            psi.ArgumentList.Add("--host");
            psi.ArgumentList.Add(_options.Host);
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(_options.Port.ToString());
            psi.ArgumentList.Add("--log-level");
            psi.ArgumentList.Add("info");

            _process = new Process { StartInfo = psi };
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) Debug.WriteLine($"[python] {e.Data}");
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) Debug.WriteLine($"[python:err] {e.Data}");
            };

            if (_process.Start())
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                AttachToKillOnCloseJob(_process);
                Debug.WriteLine($"PythonApiHost started (pid {_process.Id}) in {workingDir}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PythonApiHost failed to start: {ex.Message}");
        }
    }

    /// <summary>
    /// Polls the backend /health endpoint until it responds or the timeout
    /// elapses, so the UI can connect on the first attempt.
    /// </summary>
    public async Task<bool> WaitUntilReadyAsync(TimeSpan timeout)
    {
        if (!IsRunning)
            return false;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var uri = $"{_options.BaseUrl.TrimEnd('/')}/health";
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (!IsRunning)
                return false;

            try
            {
                var response = await client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
                // Backend still booting.
            }

            await Task.Delay(250);
        }

        return false;
    }

    public void Stop()
    {
        // Close the job first: KILL_ON_JOB_CLOSE terminates every process
        // still inside it (the whole uvicorn tree), so Python cannot outlive
        // the window even if Process.Kill below misses a child.
        CloseJob();

        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // Best effort shutdown.
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    /// <summary>
    /// Puts the backend process into a job object configured to kill the whole
    /// tree when this app exits. Assigning to a job is best-effort; on failure
    /// <see cref="Stop"/> still falls back to <see cref="Process.Kill"/>.
    /// </summary>
    private void AttachToKillOnCloseJob(Process process)
    {
        try
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
                return;

            var limits = new JobObjectBasicLimitInformation { LimitFlags = KILL_ON_JOB_CLOSE };
            var size = (uint)Marshal.SizeOf<JobObjectBasicLimitInformation>();
            var ptr = Marshal.AllocHGlobal((int)size);

            try
            {
                Marshal.StructureToPtr(limits, ptr, false);
                var ok = SetInformationJobObject(job, JobObjectBasicLimitInformationClass, ptr, size) &&
                         AssignProcessToJobObject(job, process.Handle);

                if (ok)
                    _job = job;
                else
                    CloseHandle(job);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PythonApiHost: could not attach job, using Kill fallback: {ex.Message}");
        }
    }

    private void CloseJob()
    {
        if (_job == IntPtr.Zero)
            return;

        var handle = _job;
        _job = IntPtr.Zero;
        CloseHandle(handle);
    }

    private string ResolveWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.PythonWorkingDirectory))
            return Path.GetFullPath(_options.PythonWorkingDirectory);

        // Walk up from the app output directory looking for the backend folder.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Trading.Python");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return string.Empty;
    }

    private string ResolvePythonExecutable(string workingDir)
    {
        // An explicitly configured interpreter always wins.
        var configured = _options.PythonExecutable?.Trim();
        if (!string.IsNullOrWhiteSpace(configured) &&
            !string.Equals(configured, "python", StringComparison.OrdinalIgnoreCase))
        {
            return configured;
        }

        // Prefer a virtualenv living inside the backend folder.
        var candidates = new[]
        {
            Path.Combine(workingDir, ".venv", "Scripts", "python.exe"),
            Path.Combine(workingDir, "venv", "Scripts", "python.exe"),
        };

        return candidates.FirstOrDefault(File.Exists) ?? "python";
    }

    public void Dispose() => Stop();
}
