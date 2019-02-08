/*
Author(s) : Julien Caillon (julien.caillon@gmail.com)
This file was created with the 3P :  https://jcaillon.github.io/3P/
*/

/* When executed, the preprocessed variables below are set to real values */
&IF DEFINED(ErrorLogPath) = 0 &THEN
    &SCOPED-DEFINE ErrorLogPath ".log"
    &SCOPED-DEFINE DbErrorLogPath ".db.log"
    &SCOPED-DEFINE PropathFilePath ""
    &SCOPED-DEFINE DbConnectString ""
    &SCOPED-DEFINE DatabaseAliasList ""
    &SCOPED-DEFINE DbConnectionRequired FALSE
    &SCOPED-DEFINE PreExecutionProgramPath ""
    &SCOPED-DEFINE PostExecutionProgramPath ""
    &SCOPED-DEFINE TryToHideProcessFromTaskBarOnWindows FALSE
&ENDIF

&SCOPED-DEFINE GWL_EX_STYLE -20
&SCOPED-DEFINE WS_EX_APPWINDOW 262144
&SCOPED-DEFINE WS_EX_TOOLWINDOW 128

/* ***************************  Definitions  ************************** */

DEFINE STREAM str_rw.


/* ***************************  Prototypes  *************************** */

FUNCTION fi_output_last_error RETURNS LOGICAL PRIVATE ( INPUT ipc_path AS CHARACTER ) FORWARD.
FUNCTION fi_write RETURNS LOGICAL PRIVATE ( INPUT ipc_path AS CHARACTER, INPUT ipc_content AS CHARACTER ) FORWARD.

FUNCTION fi_escape_special_char RETURNS CHARACTER PRIVATE ( INPUT ipc_text AS CHARACTER ) FORWARD.

/* ***************************  Main Block  *************************** */

RUN main NO-ERROR.
fi_output_last_error(INPUT {&ErrorLogPath}).

/* we might not go till the end of this proc correctly (and yet the process would exit with code 0)
We write a final char in the standard output to let the C# side that it went ok */
fi_write(INPUT {&ErrorLogPath}, INPUT "").

/* Must be QUIT in non batch mode or progress opens an empty editor! */
QUIT.

/* **********************  Internal Procedures  *********************** */

PROCEDURE main PRIVATE:
    /*------------------------------------------------------------------------------
    Summary    :
    Parameters : <none>
    Returns    :
    Remarks    :
    ------------------------------------------------------------------------------*/

    DEFINE VARIABLE li_db AS INTEGER NO-UNDO.
    DEFINE VARIABLE ll_dbKo AS LOGICAL NO-UNDO.

    /* Session options */
    IF NOT SESSION:BATCH-MODE THEN DO:
        SESSION:SYSTEM-ALERT-BOXES = TRUE.
        SESSION:APPL-ALERT-BOXES = TRUE.
        SESSION:DEBUG-ALERT = TRUE.
        SESSION:SUPPRESS-WARNINGS = FALSE.
    END.

    &IF {&TryToHideProcessFromTaskBarOnWindows} &THEN
        IF OPSYS = "WIN32" THEN DO:
            DEFINE VARIABLE li_pid AS INTEGER NO-UNDO.
            DEFINE VARIABLE li_hwnd AS INTEGER NO-UNDO.
            DEFINE VARIABLE li_foundPid AS INTEGER NO-UNDO.
            DEFINE VARIABLE li_styles AS INTEGER NO-UNDO.

            RUN GetCurrentProcessId (OUTPUT li_pid).

            ASSIGN li_hwnd = 0.

            findHandleLoop:
            DO WHILE TRUE:
                RUN FindWindowExA (INPUT 0, INPUT li_hwnd, INPUT "ProMainWin", INPUT 0, OUTPUT li_hwnd).
                IF li_hwnd = 0 THEN
                    LEAVE findHandleLoop.
                RUN GetWindowThreadProcessId (INPUT li_hwnd, OUTPUT li_foundPid).
                IF li_foundPid = li_pid THEN DO:
                    RUN GetWindowLongA (INPUT li_hwnd, INPUT {&GWL_EX_STYLE}, OUTPUT li_styles).
                    RUN SetWindowLongA (INPUT li_hwnd, INPUT {&GWL_EX_STYLE}, INPUT li_styles - {&WS_EX_APPWINDOW} + {&WS_EX_TOOLWINDOW}, OUTPUT li_styles).
                    /* I did not find another way to actually not show this windows in the taskbar other than that... */
                    RUN Sleep (INPUT 100).
                    LEAVE findHandleLoop.
                END.
            END.
        END.
    &ENDIF

    /* Assign the PROPATH here (maximum length is 31990 and the file shouldn't contain more!) */
    &IF {&PropathFilePath} > "" &THEN
        DEFINE VARIABLE lc_propath AS CHARACTER NO-UNDO.
        INPUT STREAM str_rw FROM VALUE({&PropathFilePath}) NO-ECHO.
        REPEAT:
            IMPORT STREAM str_rw UNFORMATTED lc_propath.
        END.
        INPUT STREAM str_rw CLOSE.
        ASSIGN PROPATH = lc_propath.
    &ENDIF

    /* Connect the database(s) */
    &IF {&DbConnectString} > "" &THEN
        CONNECT VALUE({&DbConnectString}) NO-ERROR.
        IF fi_output_last_error(INPUT {&DbErrorLogPath}) THEN
            ASSIGN ll_dbKo = TRUE.
    &ENDIF

    /* Create aliases */
    &IF {&DatabaseAliasList} > "" &THEN
        REPEAT li_db = 1 TO NUM-ENTRIES({&DatabaseAliasList}, ";"):
            IF NUM-ENTRIES(ENTRY(li_db, {&DatabaseAliasList}, ";")) = 2 THEN DO:
                CREATE ALIAS VALUE(ENTRY(1, ENTRY(li_db, {&DatabaseAliasList}, ";"))) FOR DATABASE VALUE(ENTRY(2, ENTRY(li_db, {&DatabaseAliasList}, ";"))) NO-ERROR.
                IF fi_output_last_error(INPUT {&DbErrorLogPath}) THEN
                    ASSIGN ll_dbKo = TRUE.
            END.
            ELSE
                RETURN ERROR "Invalid ALIAS format, please correct it : " + QUOTER(ENTRY(li_db, {&DatabaseAliasList}, ";")) + "~n".
        END.
    &ENDIF

    /* Pre-execution program */
    &IF {&PreExecutionProgramPath} > "" &THEN
        IF SEARCH({&PreExecutionProgramPath}) = ? THEN
            RETURN ERROR "Couldn't find the post-execution program : " + {&PreExecutionProgramPath}.
        ELSE DO:
            DO ON STOP UNDO, LEAVE
                ON ERROR UNDO, LEAVE
                    ON ENDKEY UNDO, LEAVE
                    ON QUIT UNDO, LEAVE:
                RUN VALUE({&PreExecutionProgramPath}) NO-ERROR.
            END.
            fi_output_last_error(INPUT {&ErrorLogPath}).
        END.
    &ENDIF

    /* main program */
    IF NOT {&DbConnectionRequired} OR NOT ll_dbKo THEN
        DO  ON STOP   UNDO, LEAVE
        ON ERROR  UNDO, LEAVE
            ON ENDKEY UNDO, LEAVE
            ON QUIT   UNDO, LEAVE:
        /* the program to execute will be appended to this procedure so the internal procedure "program_to_run" exists at this point */
        RUN program_to_run NO-ERROR.
    END.
    fi_output_last_error(INPUT {&ErrorLogPath}).

    /* Post-execution program */
    &IF {&PostExecutionProgramPath} > "" &THEN
        IF SEARCH({&PostExecutionProgramPath}) = ? THEN
            RETURN ERROR "Couldn't find the post-execution program : " + {&PostExecutionProgramPath}.
        ELSE DO:
            DO  ON STOP   UNDO, LEAVE
                ON ERROR  UNDO, LEAVE
                    ON ENDKEY UNDO, LEAVE
                    ON QUIT   UNDO, LEAVE:
                RUN VALUE({&PostExecutionProgramPath}) NO-ERROR.
            END.
            fi_output_last_error(INPUT {&ErrorLogPath}).
        END.
    &ENDIF

    /* Delete all aliases */
    REPEAT li_db = 1 TO NUM-ALIASES:
        DELETE ALIAS VALUE(ALIAS(li_db)).
    END.

    /* Disconnect all db */
    REPEAT li_db = 1 TO NUM-DBS:
        DISCONNECT VALUE(LDBNAME(li_db)) NO-ERROR.
    END.

    RETURN "".

END PROCEDURE.

PROCEDURE GetCurrentProcessId EXTERNAL "kernel32.dll":
    DEFINE RETURN PARAMETER ppid AS LONG NO-UNDO.
END PROCEDURE.

PROCEDURE GetWindowThreadProcessId EXTERNAL "user32.dll":
    DEFINE INPUT PARAMETER phwnd AS LONG NO-UNDO.
    DEFINE OUTPUT PARAMETER pid AS LONG NO-UNDO.
END PROCEDURE.

PROCEDURE FindWindowExA EXTERNAL "user32.dll":
    DEFINE INPUT  PARAMETER hWndParent AS LONG.
    DEFINE INPUT  PARAMETER hWndChildAfter AS LONG.
    DEFINE INPUT  PARAMETER lpszClass AS CHARACTER.
    DEFINE INPUT  PARAMETER lpszWindow AS LONG.
    DEFINE RETURN PARAMETER intHandle AS LONG.
END PROCEDURE.

PROCEDURE GetWindowLongA EXTERNAL "user32.dll":
    DEFINE INPUT  PARAMETER phwnd AS LONG.
    DEFINE INPUT  PARAMETER cindex AS LONG.
    DEFINE RETURN PARAMETER currentlong AS LONG.
END PROCEDURE.

PROCEDURE SetWindowLongA EXTERNAL "user32.dll":
    DEFINE INPUT  PARAMETER phwnd AS LONG.
    DEFINE INPUT  PARAMETER cindex AS LONG.
    DEFINE INPUT  PARAMETER newlong AS LONG.
    DEFINE RETURN PARAMETER oldlong AS LONG.
END PROCEDURE.

PROCEDURE Sleep EXTERNAL "kernel32.dll" :
    DEFINE INPUT PARAMETER dwMilliseconds AS LONG.
END PROCEDURE.


/* ************************  Function Implementations ***************** */

FUNCTION fi_output_last_error RETURNS LOGICAL PRIVATE ( INPUT ipc_path AS CHARACTER ) :
    /*------------------------------------------------------------------------------
    Purpose: output the last error encountered
    Notes:
    ------------------------------------------------------------------------------*/

    DEFINE VARIABLE li_ AS INTEGER NO-UNDO.
    DEFINE VARIABLE lc_out AS CHARACTER NO-UNDO.

    IF ERROR-STATUS:ERROR THEN DO:
        IF RETURN-VALUE > "" THEN
            ASSIGN lc_out = "0~t" + fi_escape_special_char(INPUT RETURN-VALUE) + "~n".
        IF ERROR-STATUS:NUM-MESSAGES > 0 THEN DO:
            DO li_ = 1 TO ERROR-STATUS:NUM-MESSAGES:
                ASSIGN lc_out = STRING(ERROR-STATUS:GET-NUMBER(li_)) + "~t" + fi_escape_special_char(INPUT ERROR-STATUS:GET-MESSAGE(li_)) + "~n".
            END.
        END.
        fi_write(INPUT ipc_path, INPUT lc_out).
        RETURN TRUE.
    END.

    ERROR-STATUS:ERROR = FALSE.

    RETURN FALSE.

END FUNCTION.

FUNCTION fi_write RETURNS LOGICAL PRIVATE ( INPUT ipc_path AS CHARACTER, INPUT ipc_content AS CHARACTER ) :
    /*------------------------------------------------------------------------------
    Purpose: write in file
    Notes:
    ------------------------------------------------------------------------------*/

    OUTPUT STREAM str_rw TO VALUE(ipc_path) APPEND BINARY.
    PUT STREAM str_rw UNFORMATTED ipc_content.
    OUTPUT STREAM str_rw CLOSE.

    RETURN TRUE.

END FUNCTION.

FUNCTION fi_escape_special_char RETURNS CHARACTER PRIVATE ( INPUT ipc_text AS CHARACTER ) :
    /*------------------------------------------------------------------------------
    Purpose: Replace new line
    Notes:
    ------------------------------------------------------------------------------*/

    RETURN (IF ipc_text <> ? THEN REPLACE(REPLACE(REPLACE(ipc_text, "~t", "~~t"), "~r", ""), "~n", "~~n") ELSE "?").

END FUNCTION.

