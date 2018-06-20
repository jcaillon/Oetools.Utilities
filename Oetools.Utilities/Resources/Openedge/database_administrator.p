DEFINE VARIABLE ipc_code AS CHARACTER NO-UNDO.
DEFINE VARIABLE ipc_path AS CHARACTER NO-UNDO.
DEFINE VARIABLE gc_error AS CHARACTER NO-UNDO.
DEFINE VARIABLE gh_proc AS HANDLE NO-UNDO.
        
DEFINE STREAM str_file.

IF NUM-ENTRIES(SESSION:PARAMETER, "|") < 2 THEN DO:
    PUT UNFORMATTED "** Invalid parameters." SKIP ">> ERROR".
    QUIT.
END.

IF NUM-DBS < 1 THEN DO:
    PUT UNFORMATTED "** Not connected to a database." SKIP ">> ERROR".
    QUIT.
END.

ASSIGN  
    ipc_code = ENTRY(1, SESSION:PARAMETER, "|")
    ipc_path = ENTRY(2, SESSION:PARAMETER, "|").

CASE ipc_code:
    WHEN "dump-_user" THEN DO:
        RUN prodict/dump_d.p (INPUT "_User", INPUT ipc_path /* folder path */, INPUT ?) NO-ERROR.
    END.
    WHEN "load-_user" THEN DO:
        RUN prodict/load_d.p (INPUT "_User", INPUT ipc_path /* folder path */) NO-ERROR.
    END.
    WHEN "dump-df" THEN DO:
        RUN prodict/dump_df.p PERSISTENT SET gh_proc (INPUT "ALL", INPUT ipc_path, INPUT ?).
        RUN setSilent IN gh_proc (TRUE).
        RUN doDump IN gh_proc NO-ERROR.
    END.
    WHEN "load-df" THEN DO:
        /* RUN prodict/load_df.p (INPUT ipc_path) NO-ERROR. */
        RUN prodict/load_df_silent.p (INPUT ipc_path, INPUT ?, OUTPUT gc_error) NO-ERROR.
    END.
    WHEN "dump-inc" THEN DO:
        /* incremental df */
        /*CREATE ALIAS "DICTDB" FOR DATABASE "apres".*/
        /*CREATE ALIAS "DICTDB2" FOR DATABASE "avant".*/
        run prodict/dump_inc.p PERSISTENT SET gh_proc .
        run setFileName in gh_proc (ipc_path).
        run setCodePage in gh_proc ("").
        run setIndexMode in gh_proc ("active").
        run setRenameFilename in gh_proc (ipc_path + ".rf").
        run setDebugMode in gh_proc (0).
        run setSilent in gh_proc (TRUE).
        RUN doDumpIncr IN gh_proc NO-ERROR.
        DELETE PROCEDURE gh_proc.
    END.
    WHEN "dump-seq" THEN DO:
        RUN DumpSequences (INPUT ipc_path) NO-ERROR.
    END.
    WHEN "load-seq" THEN DO:
        RUN LoadSequences (INPUT ipc_path) NO-ERROR.
    END.
    OTHERWISE DO:
        PUT UNFORMATTED "** Invalid operation code." SKIP ">> ERROR".
        QUIT.
    END.
END CASE.

IF ERROR-STATUS:ERROR OR gc_error > "" THEN DO:
    PUT UNFORMATTED "** Error while performing operation." SKIP gc_error SKIP ERROR-STATUS:GET-MESSAGE(1) SKIP RETURN-VALUE SKIP ">> ERROR".
    QUIT.
END.

PUT UNFORMATTED SKIP ">> OK".
QUIT.

PROCEDURE DumpSequences :
    /*------------------------------------------------------------------------------
    Purpose:
    Notes:
    ------------------------------------------------------------------------------*/
    DEFINE INPUT PARAMETER ipipc_path  AS CHARACTER    NO-UNDO.
    
    &SCOPED-DEFINE QUERY-WHERE      "FOR EACH _Sequence NO-LOCK":U
    &SCOPED-DEFINE SEQUENCE-NUM     hBuffer:BUFFER-FIELD ( "_Seq-Num":U ):BUFFER-VALUE
    &SCOPED-DEFINE SEQUENCE-NAME    hBuffer:BUFFER-FIELD ( "_Seq-Name":U ):BUFFER-VALUE
    
    DEFINE VARIABLE hBuffer        AS HANDLE      NO-UNDO.
    DEFINE VARIABLE hQuery         AS HANDLE      NO-UNDO.
    DEFINE VARIABLE cErrorMessage  AS CHARACTER   NO-UNDO.
    DEFINE VARIABLE l_CurValue     AS INT64       NO-UNDO.
    DEFINE VARIABLE iFilePointer   AS INT64       NO-UNDO.
    DEFINE VARIABLE iCount         AS INTEGER     NO-UNDO.
    
    
    DUMP-SEQVAL-BLK:
    DO ON ERROR UNDO DUMP-SEQVAL-BLK, LEAVE DUMP-SEQVAL-BLK:
        CREATE BUFFER hBuffer FOR TABLE "_Sequence":U.
        CREATE QUERY hQuery.
        hQuery:SET-BUFFERS ( hBuffer ).
        hQuery:QUERY-PREPARE ( {&QUERY-WHERE} ).
        hQuery:QUERY-OPEN ( ).
        hQuery:GET-FIRST ( NO-LOCK ).
        
        OUTPUT STREAM str_file TO VALUE ( ipipc_path ).
        
        REPEAT WHILE NOT hQuery:QUERY-OFF-END:
            ASSIGN l_CurValue = DYNAMIC-CURRENT-VALUE ({&SEQUENCE-NAME}, "DICTDB" )
                iCount     = iCount + 1.
            EXPORT STREAM str_File {&SEQUENCE-NUM} {&SEQUENCE-NAME} l_CurValue.
            hQuery:GET-NEXT ( NO-LOCK ).
        END.
        
        PUT STREAM str_File UNFORMATTED ".":U SKIP.
        ASSIGN iFilePointer = SEEK ( str_File ).
        
        PUT STREAM str_File UNFORMATTED "PSC" SKIP.
        PUT STREAM str_File UNFORMATTED "cpstream=" SESSION:CPSTREAM SKIP.
        PUT STREAM str_File UNFORMATTED ".":U SKIP.
        
        IF iFilePointer > 9999999999 THEN
            PUT STREAM str_File UNFORMATTED STRING( iFilePointer ) SKIP.
        ELSE
            PUT STREAM str_File UNFORMATTED STRING( iFilePointer, "9999999999" ) SKIP.
        
        PUT UNFORMATTED ( SUBSTITUTE ( "&1 sequence values dumped to &2.", iCount, ipipc_path ) ) SKIP.
        
        /* Error handling */
        CATCH oAnyError AS Progress.Lang.Error:
            ASSIGN cErrorMessage = "** Unhandled exception dumping sequence current values [" + oAnyError:GetMessage ( 1 ) + "].".
            DELETE OBJECT oAnyError.
            RETURN ERROR cErrorMessage.
        END CATCH.
        
        FINALLY:
            IF VALID-HANDLE ( hQuery ) THEN DO:
                IF hQuery:IS-OPEN THEN hQuery:QUERY-CLOSE ( ).
                DELETE OBJECT hQuery.
                ASSIGN hQuery = ?.
            END.
            IF VALID-HANDLE ( hBuffer ) THEN DO:
                DELETE OBJECT hBuffer.
                ASSIGN hBuffer = ?.
            END.
            OUTPUT STREAM str_File CLOSE.
        END FINALLY.
    END.
    
    RETURN "".
END PROCEDURE.

PROCEDURE LoadSequences :
    /*------------------------------------------------------------------------------
    Purpose:
    Notes:
    ------------------------------------------------------------------------------*/
    DEFINE INPUT PARAMETER ipipc_path  AS CHARACTER    NO-UNDO.
    
    DEFINE VARIABLE hBuffer        AS HANDLE      NO-UNDO.
    DEFINE VARIABLE cDummy         AS CHARACTER   NO-UNDO.
    DEFINE VARIABLE cErrorMessage  AS CHARACTER   NO-UNDO.
    DEFINE VARIABLE cSeqName       AS CHARACTER   NO-UNDO.
    DEFINE VARIABLE iSeqNum        AS INTEGER     NO-UNDO.
    DEFINE VARIABLE iCount         AS INTEGER     NO-UNDO.
    DEFINE VARIABLE iSeqVal        AS INT64       NO-UNDO.
    
    LOAD-SEQVAL-BLK:
    DO ON ERROR UNDO LOAD-SEQVAL-BLK, LEAVE LOAD-SEQVAL-BLK:
        IF ipipc_path = "":U or ipipc_path = ? THEN DO:
            PUT UNFORMATTED "** Input file for import not specified." SKIP.
            UNDO LOAD-SEQVAL-BLK, LEAVE LOAD-SEQVAL-BLK.
        END.
        INPUT STREAM str_file FROM VALUE ( ipipc_path ).
        REPEAT:
            IMPORT STREAM str_file UNFORMATTED cDummy.
            IF NUM-ENTRIES ( cDummy, " ":U ) = 3 THEN DO:
                ASSIGN iSeqNum  = INTEGER ( ENTRY ( 1, cDummy, " ":U ) )
                    cSeqName = TRIM ( ENTRY ( 2, cDummy, " ":U ), '"' )
                    iSeqVal  = INT64 ( ENTRY ( 3, cDummy, " ":U ) ).
                DO TRANSACTION:
                    ASSIGN DYNAMIC-CURRENT-VALUE ( cSeqName, "DICTDB" ) = iSeqVal
                        iCount                                       = iCount + 1.
                END.
            END.
        END.
        PUT UNFORMATTED SUBSTITUTE ( "&1 sequence values loaded from &2.", iCount, ipipc_path ) SKIP.
        /* Error handling */
        CATCH oAnyError AS Progress.Lang.Error:
            ASSIGN cErrorMessage = "** Unhandled exception loading sequence current values [" + oAnyError:GetMessage ( 1 ) + "].".
            DELETE OBJECT oAnyError.
            RETURN ERROR cErrorMessage.
        END CATCH.
        FINALLY:
            INPUT STREAM str_file CLOSE.
        END FINALLY.
    END.
    
    RETURN "".
END PROCEDURE.

