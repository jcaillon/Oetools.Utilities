using System;
using System.Collections.Generic;

namespace Oetools.Builder.Core2.Execution {
    
    public interface IEnvExecution {
                
        /// <summary>
        /// Path to the dlc folder (openedge installation folder)
        /// </summary>
        string DlcPath { get; set; }
        
        /// <summary>
        /// True if using _progres
        /// </summary>
        bool UseProgressCharacterMode { get; set; }

        /// <summary>
        /// Connection string to use for the database connection in a CONNECT statement
        /// </summary>
        string ConnectionString { get; set; }

        List<IEnvExecutionDatabaseAlias> DatabaseAliases { get; set; }

        /// <summary>
        /// Path to the .ini file (to define FONTS/COLORS mostly, the PROPATH is emptied and replace by <see cref="ProPathList"/>
        /// </summary>
        string IniPath { get; set; }

        /// <summary>
        /// Propath, list of directories/.pl
        /// </summary>
        List<string> ProPathList { get; set; }

        /// <summary>
        /// Command line parameters to append to the execution of progress
        /// </summary>
        string ProExeCommandLineParameters { get; set; }
        
        /// <summary>
        /// Path of the .p program that should be executed at the start of an openedge session
        /// </summary>
        string PreExecutionProgramPath { get; set; }
        
        /// <summary>
        /// Path of the .p program that should be executed at the end of an openedge session
        /// </summary>
        string PostExecutionProgramPath { get; set; }
        
        /// <summary>
        /// Indicates whether or not the -nosplash parameter is available for this version of openedge
        /// </summary>
        bool CanProVersionUseNoSplash { get; }
        
        /// <summary>
        /// Temporary folder used when executing openedge
        /// </summary>
        string TempDirectory { get; set; }
        
    }
}