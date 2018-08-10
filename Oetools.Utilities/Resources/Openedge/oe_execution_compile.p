/*
Author(s) : Julien Caillon (julien.caillon@gmail.com)
This file was created with the 3P :  https://jcaillon.github.io/3P/
*/

/* When executed, the preprocessed variables below are set to real values */
/* if ExecutionType not already defined */

&IF DEFINED(ErrorLogPath) = 0 &THEN
    &SCOPED-DEFINE ErrorLogPath ".log"
    DEFINE STREAM str_rw.
    FUNCTION fi_output_last_error RETURNS LOGICAL ( INPUT ipc_path AS CHARACTER ) IN SUPER.
    FUNCTION fi_write RETURNS LOGICAL ( INPUT ipc_path AS CHARACTER, INPUT ipc_content AS CHARACTER ) IN SUPER.
    FUNCTION fi_escape_special_char RETURNS CHARACTER ( INPUT ipc_text AS CHARACTER ) IN SUPER.
&ENDIF

&IF DEFINED(CompileListFilePath) = 0 &THEN
    &SCOPED-DEFINE CompileListFilePath ""
    &SCOPED-DEFINE CompileProgressionFilePath ""
    /* LOG-MANAGER doesn't exist prior 10.2 so we can't analyse in early versions */
    &SCOPED-DEFINE IsAnalysisMode FALSE
    &SCOPED-DEFINE GetRcodeTableList FALSE
    /* for version >= 10.2 */
    &SCOPED-DEFINE ProVerHigherOrEqualTo10.2 true
    &SCOPED-DEFINE UseXmlXref false
    &SCOPED-DEFINE CompileStatementExtraOptions 
    &SCOPED-DEFINE CompilerMultiCompile false 
    &SCOPED-DEFINE ProVerHigherOrEqualTo11.7 true
    /* the OPTIONS option was introduced in 11.7 */
    &SCOPED-DEFINE CompileOptions "" 
&ENDIF

&IF DEFINED(RunProgramMode) = 0 &THEN
    &SCOPED-DEFINE RunProgramMode false
    &SCOPED-DEFINE RunFullClientLogPath ""
&ENDIF

/* ***************************  Definitions  ************************** */

DEFINE TEMP-TABLE tt_list NO-UNDO
    FIELD order AS INTEGER
    FIELD sourcePath AS CHARACTER
    FIELD outDirectory AS CHARACTER
    FIELD errorLogPath AS CHARACTER
    FIELD listingPath AS CHARACTER
    FIELD xrfPath AS CHARACTER
    FIELD xmlxrfPath AS CHARACTER
    FIELD dbgPath AS CHARACTER
    FIELD preprocessedPath AS CHARACTER
    FIELD fileIdLogPath AS CHARACTER
    FIELD rcodeTableList AS CHARACTER
    INDEX idxfld order ASCENDING
    .

/* ***************************  Prototypes  *************************** */


/* **********************  Internal Procedures  *********************** */

PROCEDURE program_to_run PRIVATE:
    /*------------------------------------------------------------------------------
    Purpose: allows to compile all the files listed in the {&CompileListFilePath}
    Parameters:  <none>
    ------------------------------------------------------------------------------*/

    DEFINE VARIABLE li_order AS INTEGER NO-UNDO INITIAL 0.
    
    &IF {&ProVerHigherOrEqualTo10.2} &THEN
        COMPILER:MULTI-COMPILE = {&CompilerMultiCompile}.
    &ENDIF

    /* loop through all the files to compile and store them in a tt */
    INPUT STREAM str_rw FROM VALUE({&CompileListFilePath}) NO-ECHO.
    REPEAT:
        CREATE tt_list.
        ASSIGN
            tt_list.order = li_order
            li_order = li_order + 1.
        IMPORT STREAM str_rw tt_list EXCEPT tt_list.order.
        RELEASE tt_list.
    END.
    INPUT STREAM str_rw CLOSE.
    IF AVAILABLE(tt_list) THEN
        DELETE tt_list.

    /* for each file to compile */
    FOR EACH tt_list:
        &IF {&RunProgramMode} &THEN
           
            &IF {&ProVerHigherOrEqualTo10.2} &THEN
                &IF {&RunFullClientLogPath} > "" &THEN
                    LOG-MANAGER:CLOSE-LOG() NO-ERROR.
                    ASSIGN
                        LOG-MANAGER:LOGFILE-NAME = {&RunFullClientLogPath}
                        LOG-MANAGER:LOGGING-LEVEL = 3
                        LOG-MANAGER:LOG-ENTRY-TYPES = LOG-MANAGER:ENTRY-TYPES-LIST.
                &ELSE
                    IF LOG-MANAGER:LOGFILE-NAME > "" THEN DO:
                        LOG-MANAGER:CLEAR-LOG().
                    END.
                &ENDIF
            &ENDIF
        
            DO  ON STOP   UNDO, LEAVE
                ON ERROR  UNDO, LEAVE
                ON ENDKEY UNDO, LEAVE
                ON QUIT   UNDO, LEAVE:
                RUN VALUE(tt_list.sourcePath) NO-ERROR.
            END.
            
            &IF {&ProVerHigherOrEqualTo10.2} &THEN
                IF LOG-MANAGER:LOGFILE-NAME > "" THEN DO:
                    LOG-MANAGER:CLOSE-LOG().
                END.
            &ENDIF
        &ELSE
            &IF {&IsAnalysisMode} AND {&ProVerHigherOrEqualTo10.2} &THEN
                IF tt_list.fileIdLogPath > "" THEN DO:
                    /* we don't bother saving/restoring the log-manager state since we are only compiling, there
                    should be no *useful* log activated at this moment */
                    ASSIGN
                        LOG-MANAGER:LOGFILE-NAME = tt_list.fileIdLogPath
                        LOG-MANAGER:LOGGING-LEVEL = 3
                        LOG-MANAGER:LOG-ENTRY-TYPES = "FileID"
                        .
                END.
            &ENDIF
            DEFINE VARIABLE ll_save AS LOGICAL NO-UNDO INITIAL TRUE.
            ASSIGN ll_save = tt_list.outDirectory > "".
            COMPILE VALUE(tt_list.sourcePath)
                SAVE = ll_save INTO VALUE(tt_list.outDirectory)
                &IF {&ProVerHigherOrEqualTo11.7} AND {&CompileOptions} > "" &THEN
                    OPTIONS {&CompileOptions}
                &ENDIF                
                LISTING VALUE(tt_list.listingPath)
                &IF NOT {&UseXmlXref} OR NOT {&ProVerHigherOrEqualTo10.2} &THEN
                    XREF VALUE(tt_list.xrfPath)
                &ELSE
                    XREF-XML VALUE(tt_list.xmlxrfPath)
                &ENDIF
                DEBUG-LIST VALUE(tt_list.dbgPath)
                PREPROCESS VALUE(tt_list.preprocessedPath)
                &IF "{&CompileStatementExtraOptions}" > "" &THEN
                    {&CompileStatementExtraOptions}
                &ENDIF
                NO-ERROR.
            &IF {&IsAnalysisMode} AND {&ProVerHigherOrEqualTo10.2} &THEN
                IF tt_list.fileIdLogPath > "" THEN DO:
                    LOG-MANAGER:CLOSE-LOG().
                END.
            &ENDIF
        &ENDIF
        
        /* handles the errors on the compile statement itself */
        fi_output_last_error(INPUT {&ErrorLogPath}).
        
        RUN pi_handleCompilationErrors (INPUT tt_list.sourcePath, INPUT tt_list.errorLogPath) NO-ERROR.
        fi_output_last_error(INPUT {&ErrorLogPath}).
        
        &IF {&IsAnalysisMode} AND {&GetRcodeTableList} AND {&ProVerHigherOrEqualTo10.2} &THEN
            /* Here we generate a file that lists all db.tables + CRC referenced in the .r code produced */
            RUN pi_getRcodeTableList (INPUT tt_list.sourcePath, INPUT tt_list.outDirectory, INPUT tt_list.rcodeTableList) NO-ERROR.
            fi_output_last_error(INPUT {&ErrorLogPath}).
        &ENDIF

        /* the following stream / file is used to inform the C# side of the progression */
        fi_write(INPUT {&CompileProgressionFilePath}, INPUT "x").
    END.

    RETURN "".

END PROCEDURE.

PROCEDURE pi_handleCompilationErrors PRIVATE:
    /*------------------------------------------------------------------------------
    Purpose: save any compilation error into a log file
    Parameters:  <none>
    ------------------------------------------------------------------------------*/

    DEFINE INPUT PARAMETER ipc_from AS CHARACTER NO-UNDO.
    DEFINE INPUT PARAMETER ipc_logpath AS CHARACTER NO-UNDO.
    DEFINE VARIABLE lc_msg AS CHARACTER NO-UNDO INITIAL "".

    /* if PROVERSION >= 10.2 */
    &IF {&ProVerHigherOrEqualTo10.2} &THEN
        IF COMPILER:NUM-MESSAGES > 0 THEN DO:
            DEFINE VARIABLE li_i AS INTEGER NO-UNDO.
            DO li_i = 1 TO COMPILER:NUM-MESSAGES:
                ASSIGN lc_msg = lc_msg + SUBSTITUTE("&1~t&2~t&3~t&4~t&5~t&6~t&7&8",
                    ipc_from,
                    COMPILER:GET-FILE-NAME(li_i),
                    IF COMPILER:GET-MESSAGE-TYPE(li_i) = 2 THEN "Warning" ELSE "Error",
                    COMPILER:GET-ERROR-ROW(li_i),
                    COMPILER:GET-ERROR-COLUMN(li_i),
                    COMPILER:GET-NUMBER(li_i),
                    fi_escape_special_char(INPUT TRIM(REPLACE(REPLACE(COMPILER:GET-MESSAGE(li_i), "** ", ""), " (" + STRING(COMPILER:GET-NUMBER(li_i)) + ")", ""))),
                    "~r~n"
                    ).
            END.
        END.
    &ELSE
        IF COMPILER:ERROR OR COMPILER:WARNING THEN DO:
            DEFINE VARIABLE li_j AS INTEGER NO-UNDO.
            DEFINE VARIABLE lc_m AS CHARACTER NO-UNDO.
            DO li_j = 1 TO ERROR-STATUS:NUM-MESSAGES:
                ASSIGN lc_m = fi_escape_special_char(INPUT REPLACE(REPLACE(ERROR-STATUS:GET-MESSAGE(li_j), "** ", ""), " (" + STRING(ERROR-STATUS:GET-NUMBER(li_j)) + ")", "")).
                ASSIGN lc_msg = lc_msg + SUBSTITUTE("&1~t&2~t&3~t&4~t&5~t&6~t&7&8",
                    ipc_from,
                    IF COMPILER:FILE-NAME > "" THEN COMPILER:FILE-NAME ELSE ipc_from,
                    IF COMPILER:WARNING AND lc_m BEGINS "WARNING" THEN "Warning" ELSE "Error",
                    IF COMPILER:ERROR-ROW > 0 THEN COMPILER:ERROR-ROW ELSE 1,
                    IF COMPILER:ERROR-COLUMN > 0 THEN COMPILER:ERROR-COLUMN ELSE 1,
                    ERROR-STATUS:GET-NUMBER(li_j),
                    lc_m,
                    "~r~n"
                    ).
            END.
        END.
    &ENDIF

    IF lc_msg > "" THEN
        fi_write(INPUT ipc_logpath, INPUT lc_msg).

    ASSIGN ERROR-STATUS:ERROR = FALSE.

    RETURN "".

END PROCEDURE.


&IF {&IsAnalysisMode} AND {&GetRcodeTableList} AND {&ProVerHigherOrEqualTo10.2} &THEN
    PROCEDURE pi_getRcodeTableList PRIVATE:
        /*------------------------------------------------------------------------------
        Summary    : generate a file that lists all db.tables + CRC referenced in the .r code produced
        Parameters : <none>
        ------------------------------------------------------------------------------*/

        DEFINE INPUT PARAMETER ipc_compiledSource AS CHARACTER NO-UNDO.
        DEFINE INPUT PARAMETER ipc_compilationDir AS CHARACTER NO-UNDO.
        DEFINE INPUT PARAMETER ipc_outTableRefPath AS CHARACTER NO-UNDO.

        DEFINE VARIABLE li_i AS INTEGER NO-UNDO.
        DEFINE VARIABLE lc_tableList AS CHARACTER NO-UNDO INITIAL "".
        DEFINE VARIABLE lc_crcList AS CHARACTER NO-UNDO INITIAL "".
        DEFINE VARIABLE lc_rcode AS CHARACTER NO-UNDO.
        DEFINE VARIABLE lc_rcodePath AS CHARACTER NO-UNDO.

        ASSIGN
            lc_rcode = REPLACE(ipc_compiledSource, "/", "~\")
            lc_rcode = SUBSTRING(lc_rcode, R-INDEX(lc_rcode, "~\") + 1)
            lc_rcode = SUBSTRING(lc_rcode, 1, R-INDEX(lc_rcode, ".") - 1)
            lc_rcode = lc_rcode + ".r"
            lc_rcodePath = RIGHT-TRIM(ipc_compilationDir, "~\") + "~\" + lc_rcode
            .

        /* The only difficulty is to find the .r code for classes */
        ASSIGN FILE-INFORMATION:FILE-NAME = lc_rcodePath.
        IF FILE-INFORMATION:FILE-TYPE = ? THEN DO:

            /* need to find the right .r code in the directories created during compilation */
            RUN pi_findInFolders (INPUT lc_rcode, INPUT ipc_compilationDir) NO-ERROR.
            ASSIGN
                lc_rcodePath = RETURN-VALUE
                FILE-INFORMATION:FILE-NAME = lc_rcodePath.
        END.
        
        /* Retrieve table list as well as their CRC values */
        IF FILE-INFORMATION:FILE-TYPE <> ? THEN
            ASSIGN
                RCODE-INFORMATION:FILE-NAME = lc_rcodePath
                lc_tableList = TRIM(RCODE-INFORMATION:TABLE-LIST)
                lc_crcList = TRIM(RCODE-INFORMATION:TABLE-CRC-LIST)
                .

        /* Store tables referenced in the file */
        OUTPUT STREAM str_rw TO VALUE(ipc_outTableRefPath) APPEND BINARY.
        PUT STREAM str_rw UNFORMATTED "".
        REPEAT li_i = 1 TO NUM-ENTRIES(lc_tableList):
            PUT STREAM str_rw UNFORMATTED ENTRY(li_i, lc_tableList) + " " + ENTRY(li_i, lc_crcList) SKIP.
        END.
        OUTPUT STREAM str_rw CLOSE.

        RETURN "".

    END PROCEDURE.

    PROCEDURE pi_findInFolders PRIVATE:
        /*------------------------------------------------------------------------------
        Summary    : Allows to find the fullpath of the given file in a given folder (recursively)
        Parameters : <none>
        ------------------------------------------------------------------------------*/

        DEFINE INPUT PARAMETER ipc_fileToFind AS CHARACTER NO-UNDO.
        DEFINE INPUT PARAMETER ipc_dir AS CHARACTER NO-UNDO.

        DEFINE VARIABLE lc_listSubdir AS CHARACTER NO-UNDO INITIAL "".
        DEFINE VARIABLE li_subDir AS INTEGER NO-UNDO.
        DEFINE VARIABLE lc_listFilesSubDir AS CHARACTER NO-UNDO INITIAL "".
        DEFINE VARIABLE lc_filename AS CHARACTER NO-UNDO.
        DEFINE VARIABLE lc_fullPath AS CHARACTER NO-UNDO.
        DEFINE VARIABLE lc_fileType AS CHARACTER NO-UNDO.
        DEFINE VARIABLE lc_outputFullPath AS CHARACTER NO-UNDO INITIAL "".

        INPUT STREAM str_rw FROM OS-DIR(ipc_dir).
        dirRepeat:
        REPEAT:
            IMPORT STREAM str_rw lc_filename lc_fullPath lc_fileType.
            IF lc_filename = "." OR lc_filename = ".." THEN
                NEXT dirRepeat.
            IF lc_filename = ipc_fileToFind THEN DO:
                ASSIGN lc_outputFullPath = lc_fullPath.
                LEAVE dirRepeat.
            END.
            ELSE IF lc_fileType MATCHES "*D*" THEN
                ASSIGN lc_listSubdir = lc_listSubdir + lc_fullPath + ",".
        END.
        INPUT STREAM str_rw CLOSE.

        IF lc_outputFullPath > "" THEN
            RETURN lc_outputFullPath.

        ASSIGN lc_listSubdir = TRIM(lc_listSubdir, ",").
        DO li_subDir = 1 TO NUM-ENTRIES(lc_listSubdir):
            RUN pi_findInFolders (INPUT ipc_fileToFind, INPUT ENTRY(li_subDir, lc_listSubdir)) NO-ERROR.
            ASSIGN lc_outputFullPath = RETURN-VALUE.
            IF NOT fi_output_last_error(INPUT {&ErrorLogPath}) AND lc_outputFullPath > "" THEN
                RETURN lc_outputFullPath.
        END.

        RETURN "".

    END PROCEDURE.

&ENDIF

/* ************************  Function Implementations ***************** */
