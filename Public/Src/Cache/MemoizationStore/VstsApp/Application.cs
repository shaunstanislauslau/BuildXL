// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.ContractsLight;
using System.Threading;
using BuildXL.Cache.ContentStore.FileSystem;
using BuildXL.Cache.ContentStore.Interfaces.FileSystem;
using BuildXL.Cache.ContentStore.Interfaces.Logging;
using BuildXL.Cache.ContentStore.Logging;
using BuildXL.Cache.ContentStore.Tracing;
using CLAP;

// ReSharper disable UnusedMember.Global
namespace BuildXL.Cache.MemoizationStore.VstsApp
{
    /// <summary>
    ///     Core application implementation with CLAP verbs.
    /// </summary>
    internal sealed partial class Application : IDisposable
    {
        private readonly IAbsFileSystem _fileSystem;
        private readonly ConsoleLog _consoleLog;
        private readonly Logger _logger;
        private readonly Tracer _tracer;
        private bool _waitForDebugger;
        private FileLog _fileLog;
        private bool _logAutoFlush;
        private string _logDirectoryPath;
        private long _logMaxFileSize;
        private int _logMaxFileCount;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Application"/> class.
        /// </summary>
        public Application()
        {
            _consoleLog = new ConsoleLog(Severity.Warning);
            _logger = new Logger(true, _consoleLog);
            _fileSystem = new PassThroughFileSystem(_logger);
            _tracer = new Tracer(nameof(Application));
        }

        /// <inheritdoc />
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_fileLog")]
        public void Dispose()
        {
            _logger.Dispose();
            _fileLog?.Dispose();
            _consoleLog.Dispose();
            _fileSystem.Dispose();
        }

        /// <summary>
        ///     Show user help.
        /// </summary>
        /// <param name="help">Help string generated by CLAP.</param>
        /// <remarks>
        ///     This is intended to only be called by CLAP.
        /// </remarks>
        [Help(Aliases = "help,h,?")]
        public void ShowHelp(string help)
        {
            Contract.Requires(help != null);
            _logger.Always("MemoizationStoreVsts Tool");
            _logger.Always(help);
        }

        /// <summary>
        ///     Handle verb exception.
        /// </summary>
        /// <remarks>
        ///     This is intended to only be called by CLAP.
        /// </remarks>
        [Error]
        public void HandleError(ExceptionContext exceptionContext)
        {
            Contract.Requires(exceptionContext != null);
            _logger.Error(exceptionContext.Exception.InnerException != null
                ? $"{exceptionContext.Exception.Message}: {exceptionContext.Exception.InnerException.Message}"
                : exceptionContext.Exception.Message);
            exceptionContext.ReThrow = false;
        }

        /// <summary>
        ///     Set option to wait for debugger to attach.
        /// </summary>
        [Global("WaitForDebugger", Description = "Wait for debugger to attach")]
        public void SetWaitForDebugger(bool waitForDebugger)
        {
            _waitForDebugger = waitForDebugger;
        }

        /// <summary>
        ///     Set the console log line format to short or long form.
        /// </summary>
        [Global("LogLongForm", Description = "Use long logging form on console")]
        public void SetLogLongLayout(bool value)
        {
            foreach (var consoleLog in _logger.GetLog<ConsoleLog>())
            {
                consoleLog.UseShortLayout = !value;
            }
        }

        /// <summary>
        ///     Set the console log severity filter.
        /// </summary>
        [Global("LogSeverity", Description = "Set console severity filter")]
        public void SetLogSeverity(Severity logSeverity)
        {
            foreach (var consoleLog in _logger.GetLog<ConsoleLog>())
            {
                consoleLog.CurrentSeverity = logSeverity;
            }
        }

        /// <summary>
        ///     Enable automatic log file flushing.
        /// </summary>
        [Global("LogAutoFlush", Description = "Enable automatic log file flushing")]
        public void SetLogAutoFlush(bool logAutoFlush)
        {
            _logAutoFlush = logAutoFlush;
        }

        /// <summary>
        ///     Self explanatory.
        /// </summary>
        [Global("LogDirectoryPath", Description = "Set log directory path")]
        public void SetLogDirectoryPath(string path)
        {
            _logDirectoryPath = path;
        }

        /// <summary>
        ///     Set log rolling max file size.
        /// </summary>
        [Global("LogMaxFileSizeMB", Description = "Set log rolling max file size in MB")]
        public void SetLogMaxFileSizeMB(long value)
        {
            _logMaxFileSize = value * 1024 * 1024;
        }

        /// <summary>
        ///     Set log rolling max file count.
        /// </summary>
        [Global("LogMaxFileCount", Description = "Set log rolling max file count")]
        public void SetLogMaxFileCount(int value)
        {
            _logMaxFileCount = value;
        }

        private void Initialize()
        {
            if (_waitForDebugger)
            {
                _logger.Warning("Waiting for debugger to attach. Hit any key to bypass.");

                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                Debugger.Break();
            }

            SetThreadPoolSizes();

            _fileLog = new FileLog(_logDirectoryPath, null, Severity.Diagnostic, _logAutoFlush, _logMaxFileSize, _logMaxFileCount);
            _logger.AddLog(_fileLog);
        }

        private void SetThreadPoolSizes()
        {
            int workerThreads;
            int completionPortThreads;

            ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
            workerThreads = Math.Max(workerThreads, Environment.ProcessorCount * 16);
            ThreadPool.SetMaxThreads(workerThreads, completionPortThreads);

            ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);
            workerThreads = Math.Max(workerThreads, Environment.ProcessorCount * 16);
            ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
        }
    }
}
