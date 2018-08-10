/*
Author(s) : Julien Caillon (julien.caillon@gmail.com)
This file was created with the 3P :  https://jcaillon.github.io/3P/
*/

/* When executed, the preprocessed variables below are set to real values */
/* if ExecutionType not already defined */

&IF DEFINED(ErrorLogPath) = 0 &THEN
    &SCOPED-DEFINE ErrorLogPath "out.log"
    DEFINE STREAM str_rw.
    FUNCTION fi_output_last_error RETURNS LOGICAL ( INPUT ipc_path AS CHARACTER ) IN SUPER.
    FUNCTION fi_write RETURNS LOGICAL ( INPUT ipc_path AS CHARACTER, INPUT ipc_content AS CHARACTER ) IN SUPER.
    FUNCTION fi_escape_special_char RETURNS CHARACTER ( INPUT ipc_text AS CHARACTER ) IN SUPER.
&ENDIF

&IF DEFINED(DatabaseExtractCandoTblType) = 0 &THEN
    &SCOPED-DEFINE DatabaseExtractCandoTblType "T"
    &SCOPED-DEFINE DatabaseExtractCandoTblName "*"
    &SCOPED-DEFINE DatabaseExtractFilePath "out.dbdump"
&ENDIF

/* ***************************  Definitions  ************************** */

/* ***************************  Prototypes  *************************** */

/* **********************  Internal Procedures  *********************** */

PROCEDURE program_to_run PRIVATE:

    DEFINE VARIABLE li_db AS INTEGER NO-UNDO.

    OUTPUT STREAM str_rw TO VALUE({&DatabaseExtractFilePath}) APPEND BINARY.

    CREATE WIDGET-POOL.
    REPEAT li_db = 1 TO NUM-DBS:
        PUT STREAM str_rw UNFORMATTED "D " + QUOTER(LDBNAME(li_db)) SKIP.    
        RUN pi_DumpTableListAndSequences (INPUT LDBNAME(li_db), INPUT "S", INPUT "_Sequence,_Seq-Name", INPUT "") NO-ERROR.
        fi_output_last_error(INPUT {&ErrorLogPath}).
        RUN pi_DumpTableListAndSequences (INPUT LDBNAME(li_db), INPUT "T", INPUT "_FILE,_FILE-NAME,_CRC", INPUT " WHERE CAN-DO(" + QUOTER({&DatabaseExtractCandoTblType}) + ", " + LDBNAME(li_db) + "._FILE._Tbl-Type) AND CAN-DO(" + QUOTER({&DatabaseExtractCandoTblName}) + ", " + LDBNAME(li_db) + "._FILE._FILE-NAME)") NO-ERROR.
        fi_output_last_error(INPUT {&ErrorLogPath}).
    END.
    DELETE WIDGET-POOL.

    OUTPUT STREAM str_rw CLOSE.

    RETURN "".

END PROCEDURE.

PROCEDURE pi_DumpTableListAndSequences PRIVATE:

    DEFINE INPUT PARAMETER ipc_database AS CHARACTER NO-UNDO.
    DEFINE INPUT PARAMETER ipc_prefix AS CHARACTER NO-UNDO.
    DEFINE INPUT PARAMETER ipc_listOfTablesAndFields AS CHARACTER NO-UNDO.
    DEFINE INPUT PARAMETER ipc_query AS CHARACTER NO-UNDO.

    DEFINE VARIABLE lh_table AS HANDLE NO-UNDO.
    DEFINE VARIABLE lh_field AS HANDLE NO-UNDO.
    DEFINE VARIABLE lh_query AS HANDLE NO-UNDO.
    DEFINE VARIABLE li_table_loop AS INTEGER NO-UNDO.
    DEFINE VARIABLE li_field_loop AS INTEGER NO-UNDO.
    DEFINE VARIABLE li_num_fields AS INTEGER NO-UNDO.
    DEFINE VARIABLE lc_table_info AS CHARACTER NO-UNDO.
    DEFINE VARIABLE lc_table_name AS CHARACTER NO-UNDO.
    DEFINE VARIABLE lc_field_name AS CHARACTER NO-UNDO.

    DO li_table_loop = 1 TO NUM-ENTRIES(ipc_listOfTablesAndFields, ";"):

        ASSIGN
            lc_table_info = ENTRY(li_table_loop, ipc_listOfTablesAndFields, ";")
            lc_table_name = ipc_database + "." + ENTRY(1, lc_table_info, ",")
            li_num_fields = NUM-ENTRIES(lc_table_info, ",") - 1
            .

        CREATE QUERY lh_query.
        CREATE BUFFER lh_table FOR TABLE lc_table_name.

        lh_query:SET-BUFFERS(lh_table).
        lh_query:QUERY-PREPARE("FOR EACH " + lc_table_name + " NO-LOCK" + ipc_query).
        lh_query:QUERY-OPEN.

        myquery:
        REPEAT:
            lh_query:GET-NEXT().
            IF lh_query:QUERY-OFF-END THEN
                LEAVE myquery.

            PUT STREAM str_rw UNFORMATTED ipc_prefix.

            DO li_field_loop = 2 TO li_num_fields + 1:

                PUT STREAM str_rw UNFORMATTED " ".

                ASSIGN
                    lc_field_name = ENTRY(li_field_loop, lc_table_info, ",")
                    lh_field = lh_table:BUFFER-FIELD(lc_field_name)
                    .

                /* only for non extent field */
                IF (lh_field:DATA-TYPE = "CHARACTER") THEN
                    PUT STREAM str_rw UNFORMATTED QUOTER(lh_field:BUFFER-VALUE).
                ELSE
                    PUT STREAM str_rw UNFORMATTED lh_field:BUFFER-VALUE.
            END.

            PUT STREAM str_rw UNFORMATTED SKIP.

        END.
        lh_query:QUERY-CLOSE().

        IF VALID-HANDLE(lh_query) THEN
            DELETE OBJECT lh_query.

    END.

    RETURN "".

END PROCEDURE.

/* ************************  Function Implementations ***************** */
