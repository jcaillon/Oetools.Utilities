#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (IUoeExecutionEnv.cs) is part of Oetools.Utilities.
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
using System.Collections.Generic;
using System.Text;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    /// An openedge execution environment. Contains the parameters needed for any openedge execution.
    /// </summary>
    public abstract class AUoeExecutionEnv {
        
        /// <summary>
        /// Path to the dlc folder (openedge installation folder)
        /// </summary>
        public abstract string DlcDirectoryPath { get; set; }

        /// <summary>
        /// True if using _progres
        /// </summary>
        public abstract bool UseProgressCharacterMode { get; set; }

        /// <summary>
        /// Connection string to use for the database connection in a CONNECT statement (there can be several databases)
        /// </summary>
        public abstract string DatabaseConnectionString { get; set; }

        /// <summary>
        /// List of aliases to use for the connected databases
        /// </summary>
        public abstract IEnumerable<IUoeExecutionDatabaseAlias> DatabaseAliases { get; set; }

        /// <summary>
        /// Path to the .ini file (to define FONTS/COLORS mostly, the PROPATH value should be emptied as it *weirdly* slows down the execution if it is not)
        /// </summary>
        public abstract string IniFilePath { get; set; }

        /// <summary>
        /// Propath, list of directories/.pl
        /// </summary>
        public abstract List<string> ProPathList { get; set; }

        /// <summary>
        /// Command line parameters to append to the execution of progress
        /// </summary>
        public abstract string ProExeCommandLineParameters { get; set; }

        /// <summary>
        /// Path of the .p program that should be executed at the start of an openedge session
        /// </summary>
        public abstract string PreExecutionProgramPath { get; set; }

        /// <summary>
        /// Path of the .p program that should be executed at the end of an openedge session
        /// </summary>
        public abstract string PostExecutionProgramPath { get; set; }

        /// <summary>
        /// Indicates whether or not the -nosplash parameter is available for this version of openedge
        /// </summary>
        public abstract bool CanProVersionUseNoSplash { get; }

        /// <summary>
        /// Temporary folder used when executing openedge
        /// </summary>
        public abstract string TempDirectory { get; set; }

        /// <summary>
        /// Returns true if the given version is higher or equal to the pro version found in the dlc/version file
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public abstract bool IsProVersionHigherOrEqualTo(Version version);

        /// <summary>
        /// The encoding to use for i/o with openedge processes.
        /// </summary>
        public abstract Encoding GetIoEncoding();
    }
}