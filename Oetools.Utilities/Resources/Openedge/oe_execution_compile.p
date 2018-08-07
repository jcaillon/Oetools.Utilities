/*
Author(s) : Julien Caillon (julien.caillon@gmail.com)
This file was created with the 3P :  https://jcaillon.github.io/3P/
*/

/* When executed, the preprocessed variables below are set to real values */
/* if ExecutionType not already defined */

&IF DEFINED(ErrorLogPath) = 0 &THEN
    &SCOPED-DEFINE ErrorLogPath ".log"
    &SCOPED-DEFINE DbErrorLogPath ".db.log"
    &SCOPED-DEFINE PropathFilePath ""
    &SCOPED-DEFINE DbConnectString ""
    &SCOPED-DEFINE DatabaseAliasList ""
    &SCOPED-DEFINE DbConnectionRequired FALSE
    &SCOPED-DEFINE PreExecutionProgramPath ""
    &SCOPED-DEFINE PostExecutionProgramPath ""
&ENDIF

/* ***************************  Definitions  ************************** */

DEFINE STREAM str_rw.

/* ***************************  Prototypes  *************************** */

FUNCTION fi_output_last_error RETURNS LOGICAL PRIVATE ( INPUT ipc_path AS CHARACTER ) FORWARD.
FUNCTION fi_write RETURNS LOGICAL PRIVATE ( INPUT ipc_path AS CHARACTER, INPUT ipc_content AS CHARACTER ) FORWARD.

FUNCTION fi_escape_special_char RETURNS CHARACTER PRIVATE ( INPUT ipc_text AS CHARACTER ) FORWARD.

/* **********************  Internal Procedures  *********************** */

PROCEDURE program_to_run PRIVATE:
/*------------------------------------------------------------------------------
  Summary    :     
  Parameters : <none>
  Returns    : 
  Remarks    :     
------------------------------------------------------------------------------*/

    

    RETURN "".

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

