#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseAdministrator.cs) is part of Oetools.Utilities.
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
using System.Threading;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Database.Exceptions;
using Oetools.Utilities.Openedge.Execution.Exceptions;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// Administrate an openedge database.
    /// </summary>
    public class UoePreprocessedExpressionEvaluator : IDisposable {

        /// <summary>
        /// Path to the openedge installation folder
        /// </summary>
        protected string DlcPath { get; }

        /// <summary>
        /// The encoding to use for I/O.
        /// </summary>
        public Encoding Encoding { get; }

        /// <summary>
        /// A logger.
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// Cancellation token. Used to cancel execution.
        /// </summary>
        public CancellationToken? CancelToken { get; set; }

        /// <summary>
        /// The temp folder to use when we need to write the openedge procedure for data administration
        /// </summary>
        public string TempFolder {
            get => _tempFolder ?? (_tempFolder = Utils.CreateTempDirectory());
            set => _tempFolder = value;
        }

        private UoeProcessIo _progres;
        private string _tempFolder;

        private UoeProcessIo Progres {
            get {
                if (_progres == null) {
                    _progres = new UoeProcessIo(DlcPath, true, null, Encoding) {
                        CancelToken = CancelToken,
                        Log = Log
                    };
                }
                return _progres;
            }
        }

        /// <summary>
        /// Initialize a new instance.
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="encoding"></param>
        public UoePreprocessedExpressionEvaluator(string dlcPath, Encoding encoding = null) {
            Encoding = encoding ?? UoeUtilities.GetProcessIoCodePageFromDlc(dlcPath);
            DlcPath = dlcPath;
            if (string.IsNullOrEmpty(dlcPath) || !Directory.Exists(dlcPath)) {
                throw new ArgumentException($"Invalid dlc path {dlcPath.PrettyQuote()}.");
            }
        }

        /// <inheritdoc />
        public void Dispose() {
            _progres?.Dispose();
            _progres = null;
        }

        /// <summary>
        /// Returns true if the given pre-processed expression evaluates to true.
        /// </summary>
        /// <param name="preProcExpression"></param>
        /// <returns></returns>
        /// <exception cref="UoePreprocessedExpressionEvaluationException"></exception>
        public bool IsTrue(string preProcExpression) {

            if (CanEvaluateFromString(preProcExpression, out bool result)) {
                return result;
            }

            var content = $"&IF {preProcExpression} &THEN\nPUT UNFORMATTED \"true\".\n&ELSE\nPUT UNFORMATTED \"false\".\n&ENDIF";
            var procedurePath = Path.Combine(TempFolder, $"preproc_eval_{Path.GetRandomFileName()}.p");
            File.WriteAllText(procedurePath, content, Encoding);

            try {
                var args = new ProcessArgs().Append("-p").Append(procedurePath);
                Progres.WorkingDirectory = TempFolder;
                var executionOk = Progres.TryExecute(args);
                if (!executionOk || !bool.TryParse(Progres.StandardOutput.ToString(), out result)) {
                    throw new UoePreprocessedExpressionEvaluationException(Progres.BatchOutputString);
                }
                return result;
            } finally {
                File.Delete(procedurePath);
            }
        }

        private bool CanEvaluateFromString(string preProcExpression, out bool isExpressionTrue) {
            preProcExpression = preProcExpression.Trim();

            if (preProcExpression.Equals("true", StringComparison.CurrentCultureIgnoreCase)) {
                isExpressionTrue = true;
                return true;
            }

            if (preProcExpression.Equals("false", StringComparison.CurrentCultureIgnoreCase)) {
                isExpressionTrue = false;
                return true;
            }

            if (int.TryParse(preProcExpression, out int result)) {
                isExpressionTrue = result > 0;
                return true;
            }

            var splitEqual = preProcExpression.Split('=');
            if (splitEqual.Length == 2 && splitEqual[0].TrimEnd().Equals(splitEqual[1].TrimStart(), StringComparison.CurrentCultureIgnoreCase)) {
                isExpressionTrue = true;
                return true;
            }

            isExpressionTrue = false;
            return false;
        }
    }
}
