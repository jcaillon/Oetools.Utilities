/*
	Author(s) : Julien Caillon (julien.caillon@gmail.com)
	This file was created with the 3P :  https://jcaillon.github.io/3P/
*/

DEFINE INPUT PARAMETER gc_FileName AS CHARACTER NO-UNDO.
DEFINE INPUT PARAMETER ipc_baseName AS CHARACTER NO-UNDO. /* */
DEFINE INPUT PARAMETER ipc_physicalName AS CHARACTER NO-UNDO. /* */
DEFINE INPUT PARAMETER ipc_candoTableType AS CHARACTER NO-UNDO. /* */
DEFINE INPUT PARAMETER ipc_candoFileName AS CHARACTER NO-UNDO. /* */

DEFINE VARIABLE gc_sep AS CHARACTER NO-UNDO INITIAL "~t".

DEFINE STREAM str_out.
OUTPUT STREAM str_out TO VALUE(gc_FileName) APPEND BINARY.

/* _Sequence, */

/* Report meta-information */
PUT STREAM str_out UNFORMATTED "#S~t<Sequence name>~t<Sequence num>" SKIP.
PUT STREAM str_out UNFORMATTED "#T~t<Table name>~t<Table CRC>" SKIP.

/* write sequences info */
FOR EACH TPALDB._Sequence NO-LOCK:
    PUT STREAM str_out UNFORMATTED
        "S" + gc_sep +
        ipc_baseName + "." + TPALDB._Sequence._Seq-Name
        SKIP.
END.

/* Write table information */
/* Format is: <Table name> <Table CRC> */
FOR EACH TPALDB._FILE NO-LOCK WHERE CAN-DO(ipc_candoTableType, TPALDB._FILE._Tbl-Type) AND CAN-DO(ipc_candoFileName, TPALDB._FILE._FILE-NAME):
    PUT STREAM str_out UNFORMATTED
        "T" + gc_sep + 
        ipc_baseName + "." + TPALDB._FILE._FILE-NAME + gc_sep +
        STRING(TPALDB._FILE._CRC)
        SKIP.
END.

OUTPUT STREAM str_out CLOSE.

RETURN "".
