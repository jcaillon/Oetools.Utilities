/* Returns a final OK if it ends well */

DEFINE VARIABLE ipc_code AS CHARACTER NO-UNDO.
DEFINE VARIABLE ipc_path AS CHARACTER NO-UNDO.
DEFINE VARIABLE gc_error AS CHARACTER NO-UNDO.
DEFINE VARIABLE gh_proc AS HANDLE NO-UNDO.

DEFINE STREAM str_file.

IF NUM-ENTRIES(SESSION:PARAMETER, "|") < 2 THEN DO:
    PUT UNFORMATTED "Invalid parameters.".
    QUIT.
END.

IF NUM-DBS < 1 THEN DO:
    /* the connected database should be DICTDB */
    PUT UNFORMATTED "Not connected to a database.".
    QUIT.
END.

ASSIGN
    ipc_code = ENTRY(1, SESSION:PARAMETER, "|")
    ipc_path = ENTRY(2, SESSION:PARAMETER, "|").

CASE ipc_code:
    WHEN "dump-df" THEN DO:
        RUN prodict/dump_df.p PERSISTENT SET gh_proc (INPUT ENTRY(3, SESSION:PARAMETER, "|") /* "table,table"/"ALL" */, INPUT ipc_path, INPUT ?).
        RUN setSilent IN gh_proc (TRUE).
        RUN doDump IN gh_proc NO-ERROR.
    END.
    WHEN "load-df" THEN DO:
        RUN prodict/load_df_silent.p (INPUT ipc_path, INPUT ?, OUTPUT gc_error) NO-ERROR.
        IF ERROR-STATUS:ERROR OR gc_error > "" THEN DO:
            PUT UNFORMATTED "Error while loading the schema definition:" SKIP gc_error SKIP ERROR-STATUS:GET-MESSAGE(1).
            QUIT.
        END.
        RUN ReadFileIntoStandardOutput (INPUT LDBNAME("DICTDB") + ".e").
    END.
    WHEN "dump-inc" THEN DO:
        CREATE ALIAS "DICTDB" FOR DATABASE "after".
        CREATE ALIAS "DICTDB2" FOR DATABASE "before".
        RUN prodict/dump_inc.p PERSISTENT SET gh_proc.
        RUN setFileName in gh_proc (ipc_path).
        RUN setCodePage in gh_proc (?).
        RUN setIndexMode in gh_proc ("active") NO-ERROR.
        RUN setRenameFilename in gh_proc (ENTRY(3, SESSION:PARAMETER, "|")).
        RUN setDebugMode in gh_proc (1).
        RUN setSilent in gh_proc (TRUE).
        RUN doDumpIncr IN gh_proc NO-ERROR.
        IF ERROR-STATUS:ERROR THEN DO:
            PUT UNFORMATTED "Error while dumping incremental schema definition:" SKIP ERROR-STATUS:GET-MESSAGE(1).
            QUIT.
        END.
        RUN ReadFileIntoStandardOutput (INPUT "incrdump.e").
    END.
    WHEN "dump-d" THEN DO:
        RUN prodict/dump_d.p (INPUT ENTRY(3, SESSION:PARAMETER, "|") /* "_User"/"ALL" */, INPUT ipc_path /* folder path */, INPUT ?) NO-ERROR.
    END.
    WHEN "load-d" THEN DO:
        RUN prodict/load_d.p (INPUT ENTRY(3, SESSION:PARAMETER, "|") /* "_User"/"ALL" */, INPUT ipc_path /* folder path */) NO-ERROR.
    END.
    WHEN "dump-seq" THEN DO:
        RUN DumpSequences (INPUT ipc_path) NO-ERROR.
    END.
    WHEN "load-seq" THEN DO:
        RUN LoadSequences (INPUT ipc_path) NO-ERROR.
    END.
    OTHERWISE DO:
        PUT UNFORMATTED "Invalid operation code.".
        QUIT.
    END.
END CASE.

IF ERROR-STATUS:ERROR THEN DO:
    PUT UNFORMATTED "Error while performing the database operation." SKIP ERROR-STATUS:GET-MESSAGE(1) SKIP RETURN-VALUE.
    QUIT.
END.

PUT UNFORMATTED SKIP "OK".
QUIT.

PROCEDURE DumpSequences :
    DEFINE INPUT PARAMETER ipc_path AS CHARACTER NO-UNDO.

    DEFINE VARIABLE iFilePointer AS INTEGER NO-UNDO.

    OUTPUT STREAM str_file TO VALUE (ipc_path).

    FOR EACH _Sequence NO-LOCK:
        EXPORT STREAM str_File _Sequence._Seq-Num _Sequence._Seq-Name DYNAMIC-CURRENT-VALUE(_Sequence._Seq-Name, "DICTDB").
    END.

    /* write end of file stuff */
    PUT STREAM str_File UNFORMATTED "." SKIP.
    ASSIGN iFilePointer = SEEK (str_File).
    PUT STREAM str_File UNFORMATTED "PSC" SKIP.
    PUT STREAM str_File UNFORMATTED "cpstream=" SESSION:CPSTREAM SKIP.
    PUT STREAM str_File UNFORMATTED "." SKIP.
    IF iFilePointer > 9999999999 THEN
        PUT STREAM str_File UNFORMATTED STRING(iFilePointer) SKIP.
    ELSE
        PUT STREAM str_File UNFORMATTED STRING(iFilePointer, "9999999999") SKIP.

    OUTPUT STREAM str_File CLOSE.

    RETURN "".
END PROCEDURE.

PROCEDURE LoadSequences :
    DEFINE INPUT PARAMETER ipc_path AS CHARACTER NO-UNDO.

    DEFINE VARIABLE lc_line AS CHARACTER NO-UNDO.
    DEFINE VARIABLE lc_seqName AS CHARACTER NO-UNDO.
    DEFINE VARIABLE li_seqValue AS INTEGER NO-UNDO.

    INPUT STREAM str_file FROM VALUE (ipc_path).
    REPEAT:
        IMPORT STREAM str_file UNFORMATTED lc_line.
        IF NUM-ENTRIES(lc_line, " ") = 3 THEN DO:
            ASSIGN
                lc_seqName = TRIM(ENTRY(2, lc_line, " "), '"')
                li_seqValue  = INTEGER(ENTRY(3, lc_line, " "))
                .
            ASSIGN DYNAMIC-CURRENT-VALUE(lc_seqName, "DICTDB") = li_seqValue.
        END.
    END.

    INPUT STREAM str_file CLOSE.

    RETURN "".
END PROCEDURE.

PROCEDURE ReadFileIntoStandardOutput:
    DEFINE INPUT PARAMETER ipc_file AS CHARACTER NO-UNDO.
    DEFINE VARIABLE lc_line AS CHARACTER NO-UNDO.

    IF (SEARCH(ipc_file) = ?) THEN
        RETURN "".

    INPUT STREAM str_file FROM VALUE(ipc_file).
    REPEAT:
        IMPORT STREAM str_file UNFORMATTED lc_line.
        PUT UNFORMATTED SKIP "e:" + lc_line.
    END.
    INPUT STREAM str_file CLOSE.

    OS-DELETE VALUE(SEARCH(ipc_file)).

    RETURN "".
END PROCEDURE.
