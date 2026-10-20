// Copyright (c) CreationArt. All Rights Reserved.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameBench.Editor.Native.Interop;

// Owns the Engine.exe child process: spawn with redirected streams, keep a
// tail of stdout/stderr for startup error reports, and put the process in a
// kill-on-close job object so it can never outlive the editor.
internal sealed class EngineProcess : IDisposable
{
    private const int TailLineCount = 40;

    private readonly ConcurrentQueue<string> _stdoutTail = new();
    private readonly ConcurrentQueue<string> _stderrTail = new();

    private Process? _process;
    private IntPtr _job = IntPtr.Zero;
    private bool _started;
    private bool _disposed;

    public event Action<int>? Exited;

    public bool IsRunning => _started && _process is { HasExited: false };

    public int ExitCode => _started && _process is not null ? _process.ExitCode : -1;

    public static string ExecutablePath => Path.Combine(AppContext.BaseDirectory, "Engine.exe");

    public string StdErrTail => string.Join(Environment.NewLine, _stderrTail);

    public string StdOutTail => string.Join(Environment.NewLine, _stdoutTail);

    public void Start(string arguments)
    {
        if (!File.Exists(ExecutablePath))
        {
            throw new EngineStartupException(
                $"Engine.exe was not found next to the editor (expected '{ExecutablePath}'). Build the Engine project first.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = arguments,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => AppendTail(_stdoutTail, e.Data);
        _process.ErrorDataReceived += (_, e) => AppendTail(_stderrTail, e.Data);
        _process.Exited += (_, _) => Exited?.Invoke(ExitCode);

        if (!_process.Start())
        {
            throw new EngineStartupException("Failed to start Engine.exe.");
        }

        _started = true;

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        AttachKillOnCloseJob();
    }

    // Closing the last handle to a job with KILL_ON_JOB_CLOSE terminates every
    // process inside it. Best effort: some debuggers already put the child in a
    // job, in which case explicit kill-on-shutdown still covers us.
    private void AttachKillOnCloseJob()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            _job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (_job == IntPtr.Zero)
            {
                return;
            }

            var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            var size = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            if (!NativeMethods.SetInformationJobObject(_job, NativeMethods.JobObjectExtendedLimitInformation, ref info, size))
            {
                CloseJob();
                return;
            }

            if (!NativeMethods.AssignProcessToJobObject(_job, _process.Handle))
            {
                CloseJob();
            }
        }
        catch
        {
            CloseJob();
        }
    }

    private static void AppendTail(ConcurrentQueue<string> tail, string? line)
    {
        if (line is null)
        {
            return;
        }

        tail.Enqueue(line);
        while (tail.Count > TailLineCount)
        {
            tail.TryDequeue(out _);
        }
    }

    public async Task KillAsync()
    {
        if (!_started || _process is null || _process.HasExited)
        {
            return;
        }

        try
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // The job object reaps it on editor exit even if this fails.
        }
    }

    private void CloseJob()
    {
        if (_job == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.CloseHandle(_job);
        _job = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process?.Dispose();
        _process = null;
        CloseJob();
    }

    private static class NativeMethods
    {
        public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
        public const int JobObjectExtendedLimitInformation = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
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

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(
            IntPtr hJob,
            int jobObjectInformationClass,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInformation,
            int cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
