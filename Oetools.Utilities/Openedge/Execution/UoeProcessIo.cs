#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeProcessIo.cs) is part of Oetools.Utilities.
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
using System.IO;
using System.Text;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// Represents a progress process
    /// </summary>
    /// <remarks>
    /// - progress returns an exit different of 0 only if it actually failed to start,
    /// if your procedure return error or quit, it is still an exit code of 0
    /// - in batch mode (-b) and GUI mode, even if we set CreateNoWindow and WindowStyle to Hidden,
    /// the window still appears in the taskbar. All the code between #if WINDOWSONLYBUILD in this class
    /// is made to hide this window from the taskbar in that case
    /// </remarks>
    public class UoeProcessIo : ProcessIoAsync {

        /// <summary>
        /// DLC path to use
        /// </summary>
        public string DlcPath { get;  }

        /// <summary>
        /// Whether or not to use character mode (_progres) instead of GUI (prowin)
        /// </summary>
        public bool UseCharacterMode { get; }

        /// <summary>
        /// Whether or not the executable can use the -nosplash parameter
        /// </summary>
        public bool CanUseNoSplash { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public UoeProcessIo(string dlcPath, bool useCharacterModeOfProgress, bool? canUseNoSplash = null, Encoding redirectedOutputEncoding = null) : base (null) {
            DlcPath = dlcPath;
            UseCharacterMode = useCharacterModeOfProgress;
            CanUseNoSplash = canUseNoSplash ?? UoeUtilities.CanProVersionUseNoSplashParameter(UoeUtilities.GetProVersionFromDlc(DlcPath));
            ExecutablePath = UoeUtilities.GetProExecutableFromDlc(DlcPath, UseCharacterMode);
            RedirectedOutputEncoding = redirectedOutputEncoding ?? UoeUtilities.GetProcessIoCodePageFromDlc(dlcPath);
        }

        /// <inheritdoc />
        protected override bool WaitUntilProcessExits(int timeoutMs) {
            RestoreSplashScreen();
            return base.WaitUntilProcessExits(timeoutMs);
        }

        /// <inheritdoc />
        protected override void PrepareStart(ProcessArgs arguments, bool silent) {

            if (silent) {
                arguments.Append("-b");
            }

            if (!UseCharacterMode) {
                if (CanUseNoSplash) {
                    arguments.Append("-nosplash");
                } else {
                    DisableSplashScreen();
                }
            }

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

    }
}
