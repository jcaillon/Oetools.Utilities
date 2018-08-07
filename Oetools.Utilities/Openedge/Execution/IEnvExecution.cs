using System.Collections.Generic;

namespace Oetools.Utilities.Openedge.Execution {
    
    public interface IEnvExecution {
        
        /// <summary>
        /// Path to the dlc folder (openedge installation folder)
        /// </summary>
        string DlcDirectoryPath { get; }

        /// <summary>
        /// True if using _progres
        /// </summary>
        bool UseProgressCharacterMode { get; }

        /// <summary>
        /// Connection string to use for the database connection in a CONNECT statement (there can be several databases)
        /// </summary>
        string DatabaseConnectionString { get; }

        /// <summary>
        /// List of aliases to use for the connected databases
        /// </summary>
        List<IEnvExecutionDatabaseAlias> DatabaseAliases { get; }

        /// <summary>
        /// Path to the .ini file (to define FONTS/COLORS mostly, the PROPATH value should be emptied as it *weirdly* slows down the execution if it is not)
        /// </summary>
        string IniFilePath { get; }

        /// <summary>
        /// Propath, list of directories/.pl
        /// </summary>
        List<string> ProPathList { get; }

        /// <summary>
        /// Command line parameters to append to the execution of progress
        /// </summary>
        string ProExeCommandLineParameters { get; }

        /// <summary>
        /// Path of the .p program that should be executed at the start of an openedge session
        /// </summary>
        string PreExecutionProgramPath { get; }

        /// <summary>
        /// Path of the .p program that should be executed at the end of an openedge session
        /// </summary>
        string PostExecutionProgramPath { get; }

        /// <summary>
        /// Indicates whether or not the -nosplash parameter is available for this version of openedge
        /// </summary>
        bool CanProVersionUseNoSplash { get; }

        /// <summary>
        /// Temporary folder used when executing openedge
        /// </summary>
        string TempDirectory { get; }
    }
}