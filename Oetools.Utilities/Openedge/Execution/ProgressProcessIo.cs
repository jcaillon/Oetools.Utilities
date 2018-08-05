#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProgressProcessIo.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge;

namespace Oetools.Packager.Core2.Execution {
    
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    ///     - progress returns an exit different of 0 only if it actually failed to start,
    ///     if your procedure return error or quit, it is still an exit code of 0
    /// </remarks>
    public class ProgressProcessIo : ProcessIoAsync {
        
        public string DlcPath { get;  }
        
        public bool UseCharacterMode { get; }

        public bool? CanUseNoSplash { get; set; }
        
        public string StartParameters { get; private set; }

        /// <summary>
        ///     Constructor
        /// </summary>
        public ProgressProcessIo(string dlcPath, bool useCharacterModeOfProgress, bool? canUseNoSplash = null) : base (null) {
            DlcPath = dlcPath;
            UseCharacterMode = useCharacterModeOfProgress;
            CanUseNoSplash = canUseNoSplash;
            Executable = ProUtilities.GetProExecutableFromDlc(DlcPath, UseCharacterMode);
        }

        protected override void ExecuteAsyncProcess(string arguments = null, bool silent = true) {
            base.ExecuteAsyncProcess(arguments, silent);
#if WINDOWSONLYBUILD
            if (silent) {
                // on windows, we try to hide the window
                while (!_process.HasExited && _process.TotalProcessorTime.TotalMilliseconds < 10000 && !HideProwinProcess(_process.Id)) {
                }
            }
#endif
        }

        protected override void WaitUntilProcessExits(int timeoutMs) {
            base.WaitUntilProcessExits(timeoutMs);
            RestoreSplashScreen();
        }

        protected override void PrepareStart(string arguments, bool silent) {
            
            if (silent) {
                arguments = $"{arguments ?? ""} -b";
            }
            
            if (!UseCharacterMode) {
                if (CanUseNoSplash != null && CanUseNoSplash.Value || ProUtilities.CanProVersionUseNoSplashParameter(ProUtilities.GetProVersionFromDlc(DlcPath))) {
                    arguments = $"{arguments ?? ""} -nosplash";
                } else {
                    DisableSplashScreen();
                }
            }

            StartParameters = arguments;

            // we can only redirect output in -b batch mode 
            RedirectOutput = silent;
            
            base.PrepareStart(arguments, silent);

            // in character mode, we need to execute _progress in a console!
            if (UseCharacterMode && !silent) {
                _startInfo.UseShellExecute = true;
            }
        }

        protected override void ProcessOnExited(object sender, EventArgs e) {
            base.ProcessOnExited(sender, e);
            RestoreSplashScreen();
        }

        private void DisableSplashScreen() {
            try {
                File.Move(Path.Combine(DlcPath, "bin", "splashscreen.bmp"), Path.Combine(DlcPath, "bin", "splashscreen-disabled.bmp"));
            } catch (Exception) {
                // if it fails it is not really a problem
            }
        }

        private void RestoreSplashScreen() {
            try {
                File.Move(Path.Combine(DlcPath, "bin", "splashscreen-disabled.bmp"), Path.Combine(DlcPath, "bin", "splashscreen.bmp"));
            } catch (Exception) {
                // if it fails it is not really a problem
            }
        }
        
#if WINDOWSONLYBUILD
    
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className,  string windowTitle);

        [DllImport("user32.dll", SetLastError=true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        // ReSharper disable once InconsistentNaming
        private const int GWL_EX_STYLE = -20;

        // ReSharper disable once InconsistentNaming
        private const int WS_EX_APPWINDOW = 0x00040000;

        // ReSharper disable once InconsistentNaming
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private bool HideProwinProcess(int procId) {
            var hWnd = IntPtr.Zero;
            do {
                hWnd = FindWindowEx(IntPtr.Zero, hWnd, OeConstants.ProwinWindowClass, null);
                GetWindowThreadProcessId(hWnd, out var hWndProcessId);
                if (hWndProcessId == procId) {
                    SetWindowLong(hWnd, GWL_EX_STYLE, (GetWindowLong(hWnd, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
                    ShowWindow(hWnd, 0);
                    return true;
                }
            } while(hWnd != IntPtr.Zero);	
            return false;
        }

#endif
    }
}