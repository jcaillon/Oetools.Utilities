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
                &IF NOT {&UseXmlXref} OR NOT {&ProVerHigherOrEqualTo10.2} OR {&IsAnalysisMode} &THEN
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

/* ************************  Function Implementations ***************** */
