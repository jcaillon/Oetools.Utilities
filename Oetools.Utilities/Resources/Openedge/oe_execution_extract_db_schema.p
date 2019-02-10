/*
	Author(s) : Julien Caillon (julien.caillon@gmail.com)
	This file was created with the 3P :  https://jcaillon.github.io/3P/
*/

DEFINE INPUT PARAMETER ipc_filePath AS CHARACTER NO-UNDO.
DEFINE INPUT PARAMETER ipc_logicalName AS CHARACTER NO-UNDO.
DEFINE INPUT PARAMETER ipc_physicalName AS CHARACTER NO-UNDO.
DEFINE INPUT PARAMETER ipc_candoTableType AS CHARACTER NO-UNDO.
DEFINE INPUT PARAMETER ipc_candoFileName AS CHARACTER NO-UNDO.

&SCOPED-DEFINE sep "~t"
&SCOPED-DEFINE format_database "#D~t<YYYYMMDD>~t<HH:MM:SS>~t<logical_name>~t<physical_name>~t<proversion>~t<version.minor_version>~t<charset>~t<collation>"
&SCOPED-DEFINE format_sequence "#S~t<name>~t<cycle?>~t<increment>~t<initial>~t<min>~t<max>"
&SCOPED-DEFINE format_table    "#T~t<name>~t<dump_name>~t<crc>~t<label>~t<sa>~t<desc>~t<hidden?>~t<frozen?>~t<area>~t<type>~t<valid_expr>~t<valid_mess>~t<sa>~t<replication>~t<foreign>"
&SCOPED-DEFINE format_trigger  "#X~t<event>~t<field_name>~t<proc_name>~t<overridable?>~t<crc>"
&SCOPED-DEFINE format_index    "#I~t<name>~t<active?>~t<primary?>~t<unique?>~t<word?>~t<crc>~t<area>~t<fields>~t<desc>"
&SCOPED-DEFINE format_field    "#F~t<name>~t<data_type>~t<format>~t<sa>~t<order>~t<pos>~t<mandatory?>~t<case_sensitive?>~t<extent>~t<in_index?>~t<in_pk?>~t<initial>~t<sa>~t<width>~t<label>~t<sa>~t<column_label>~t<sa>~t<desc>~t<help>~t<sa>~t<decimals>~t<charset>~t<collation>~t<size>~t<bytes>"

DEFINE VARIABLE gc_fieldList AS CHARACTER NO-UNDO.
DEFINE VARIABLE gc_fieldListWithSort AS CHARACTER NO-UNDO.
DEFINE VARIABLE gc_champIndex AS CHARACTER NO-UNDO.
DEFINE VARIABLE gc_champPK AS CHARACTER NO-UNDO.

DEFINE BUFFER gb_storageObject FOR ALIAS4DB._StorageObject.
DEFINE BUFFER gb_area FOR ALIAS4DB._Area.

DEFINE STREAM str_out.
OUTPUT STREAM str_out TO VALUE(ipc_filePath) APPEND BINARY.

/* Write database info */
PUT STREAM str_out UNFORMATTED "#______ " + ipc_logicalName + " ______" SKIP.
PUT STREAM str_out UNFORMATTED {&format_database} SKIP.
FIND FIRST ALIAS4DB._DbStatus NO-LOCK.
FIND FIRST ALIAS4DB._Db NO-LOCK.
PUT STREAM str_out UNFORMATTED "D" + {&sep} +
    STRING(YEAR(TODAY), "9999") + STRING(MONTH(TODAY), "99") + STRING(DAY(TODAY), "99") + {&sep} +
    STRING(TIME, "HH:MM:SS") + {&sep} +
    ipc_logicalName + {&sep} +
    QUOTER(ipc_physicalName) + {&sep} +
    PROVERSION + {&sep} +
    STRING(ALIAS4DB._DbStatus._DbStatus-DbVers) + "." + STRING(ALIAS4DB._DbStatus._DbStatus-DbVersMinor) + {&sep} +
    QUOTER(ALIAS4DB._Db._Db-xl-name) + {&sep} +
    QUOTER(ALIAS4DB._Db._Db-coll-name)
    SKIP.
PUT STREAM str_out UNFORMATTED "#" SKIP.

/* write sequences info */
PUT STREAM str_out UNFORMATTED {&format_sequence} SKIP.
FOR EACH ALIAS4DB._Sequence NO-LOCK BY _Seq-Num:
    PUT STREAM str_out UNFORMATTED
        "S" + {&sep} +
        ALIAS4DB._Sequence._Seq-Name + {&sep} +
        STRING(ALIAS4DB._Sequence._Cycle-Ok, "1/0") + {&sep} +
        STRING(ALIAS4DB._Sequence._Seq-Incr) + {&sep} +
        STRING(ALIAS4DB._Sequence._Seq-Init) + {&sep} +
        QUOTER(ALIAS4DB._Sequence._Seq-Min) + {&sep} +
        QUOTER(ALIAS4DB._Sequence._Seq-Max)
        SKIP.
END.
PUT STREAM str_out UNFORMATTED "#" SKIP.

/* Write table information */
FOR EACH ALIAS4DB._File NO-LOCK WHERE CAN-DO(ipc_candoTableType, ALIAS4DB._File._Tbl-Type) AND CAN-DO(ipc_candoFileName, ALIAS4DB._File._File-Name),
    FIRST ALIAS4DB._Storageobject NO-LOCK WHERE ALIAS4DB._Storageobject._Object-Number = ALIAS4DB._File._File-Number,
    FIRST ALIAS4DB._Area NO-LOCK WHERE ALIAS4DB._Area._Area-Number = ALIAS4DB._Storageobject._Area-Number:

    PUT STREAM str_out UNFORMATTED "#______ " + ALIAS4DB._File._File-Name + " ______" SKIP.
    PUT STREAM str_out UNFORMATTED {&format_table} SKIP.
    PUT STREAM str_out UNFORMATTED
        "T" + {&sep} +
        ALIAS4DB._File._File-Name + {&sep} +
        QUOTER(ALIAS4DB._File._Dump-name) + {&sep} +
        STRING(ALIAS4DB._File._CRC) + {&sep} +
        QUOTER(ALIAS4DB._File._File-Label) + {&sep} +
        QUOTER(ALIAS4DB._File._File-Label-SA) + {&sep} +
        QUOTER(ALIAS4DB._File._Desc) + {&sep} +
        STRING(ALIAS4DB._File._Hidden, "1/0") + {&sep} +
        STRING(ALIAS4DB._File._Frozen, "1/0") + {&sep} +
        QUOTER(ALIAS4DB._Area._Area-name) + {&sep} +
        ALIAS4DB._File._Tbl-Type + {&sep} +
        QUOTER(ALIAS4DB._File._Valexp) + {&sep} +
        QUOTER(ALIAS4DB._File._Valmsg) + {&sep} +
        QUOTER(ALIAS4DB._File._Valmsg-SA) + {&sep} +
        QUOTER(ALIAS4DB._File._Fil-misc2[6]) + {&sep} +
        QUOTER(ALIAS4DB._File._For-Name)
        SKIP.
    PUT STREAM str_out UNFORMATTED "#" SKIP.

    /* Write triggers information */
    PUT STREAM str_out UNFORMATTED {&format_trigger} SKIP.
    FOR EACH ALIAS4DB._File-Trig NO-LOCK OF ALIAS4DB._File:
        PUT STREAM str_out UNFORMATTED
            "X" + {&sep} +
            ALIAS4DB._File-Trig._Event + {&sep} +
            QUOTER(?) + {&sep} +
            QUOTER(ALIAS4DB._File-Trig._Proc-Name) + {&sep} +
            STRING(ALIAS4DB._File-Trig._Override, "1/0") + {&sep} +
            QUOTER(ALIAS4DB._File-Trig._Trig-Crc)
            SKIP.
    END.
    FOR EACH ALIAS4DB._Field-Trig NO-LOCK OF ALIAS4DB._File,
        FIRST ALIAS4DB._Field NO-LOCK WHERE RECID(ALIAS4DB._Field) = ALIAS4DB._Field-Trig._Field-Recid:
        PUT STREAM str_out UNFORMATTED
            "X" + {&sep} +
            ALIAS4DB._Field-Trig._Event + {&sep} +
            ALIAS4DB._Field._Field-Name + {&sep} +
            QUOTER(ALIAS4DB._Field-Trig._Proc-Name) + {&sep} +
            STRING(ALIAS4DB._Field-Trig._Override, "1/0") + {&sep} +
            QUOTER(ALIAS4DB._Field-Trig._Trig-Crc)
            SKIP.
    END.
    PUT STREAM str_out UNFORMATTED "#" SKIP.

    ASSIGN
        gc_champIndex = ""
        gc_champPK = ""
        .

    /* Write index information */
    PUT STREAM str_out UNFORMATTED {&format_index} SKIP.
    FOR EACH ALIAS4DB._Index NO-LOCK OF ALIAS4DB._File,
        FIRST gb_storageObject NO-LOCK WHERE gb_storageObject._Object-Number = ALIAS4DB._Index._idx-num,
        FIRST gb_area NO-LOCK WHERE gb_area._Area-Number = gb_storageObject._Area-Number:
        ASSIGN
            gc_fieldList = ""
            gc_fieldListWithSort = ""
            .

        FOR EACH ALIAS4DB._Index-Field NO-LOCK OF ALIAS4DB._Index,
            FIRST ALIAS4DB._Field NO-LOCK OF ALIAS4DB._Index-Field:
            ASSIGN gc_fieldList = gc_fieldList + ALIAS4DB._Field._Field-Name + ",".
            ASSIGN gc_fieldListWithSort = gc_fieldListWithSort + ALIAS4DB._Field._Field-Name + STRING(ALIAS4DB._Index-Field._Ascending, "+/-") + STRING(ALIAS4DB._Index-Field._Abbreviate, "1/0") + ",".
        END.

        IF RECID(ALIAS4DB._Index) = ALIAS4DB._File._Prime-Index THEN
            ASSIGN gc_champPK = gc_champPK + gc_fieldList.
        ASSIGN gc_champIndex = gc_champIndex + gc_fieldList.

        PUT STREAM str_out UNFORMATTED
            "I" + {&sep} +
            ALIAS4DB._Index._Index-Name + {&sep} +
            STRING(ALIAS4DB._Index._Active, "1/0") + {&sep} +
            STRING(RECID(ALIAS4DB._Index) = ALIAS4DB._File._Prime-Index, "1/0") + {&sep} +
            STRING(ALIAS4DB._Index._Unique, "1/0") + {&sep} +
            STRING(ALIAS4DB._Index._Idxmethod = "W", "1/0") + {&sep} +
            STRING(ALIAS4DB._Index._Idx-CRC) + {&sep} +
            QUOTER(gb_area._Area-name) + {&sep} +
            RIGHT-TRIM(gc_fieldListWithSort, ",") + {&sep} +
            QUOTER(ALIAS4DB._Index._Desc)
            SKIP.
    END.
    PUT STREAM str_out UNFORMATTED "#" SKIP.

    ASSIGN
        gc_champIndex = RIGHT-TRIM(gc_champIndex, ",")
        gc_champPK = RIGHT-TRIM(gc_champPK, ",")
        .

    /* Write fields information */
    PUT STREAM str_out UNFORMATTED {&format_field} SKIP.
    FOR EACH ALIAS4DB._Field NO-LOCK OF ALIAS4DB._File BY _Order:
        PUT STREAM str_out UNFORMATTED
            "F" + {&sep} +
            ALIAS4DB._Field._Field-Name + {&sep} +
            ALIAS4DB._Field._Data-Type + {&sep} +
            QUOTER(ALIAS4DB._Field._Format) + {&sep} +
            QUOTER(ALIAS4DB._Field._Format-SA) + {&sep} +
            STRING(ALIAS4DB._Field._Order) + {&sep} +
            STRING(ALIAS4DB._Field._field-rpos) + {&sep} +
            STRING(ALIAS4DB._Field._Mandatory, "1/0") + {&sep} +
            STRING(ALIAS4DB._Field._Fld-case, "1/0") + {&sep} +
            STRING(ALIAS4DB._Field._Extent) + {&sep} +
            STRING(LOOKUP(ALIAS4DB._Field._Field-Name, gc_champIndex) > 0, "1/0") + {&sep} +
            STRING(LOOKUP(ALIAS4DB._Field._Field-Name, gc_champPK) > 0, "1/0") + {&sep} +
            QUOTER(ALIAS4DB._Field._Initial) + {&sep} +
            QUOTER(ALIAS4DB._Field._Initial-SA) + {&sep} +
            QUOTER(ALIAS4DB._Field._Width) + {&sep} +
            QUOTER(ALIAS4DB._Field._Label) + {&sep} +
            QUOTER(ALIAS4DB._Field._Label-SA) + {&sep} +
            QUOTER(ALIAS4DB._Field._Col-label) + {&sep} +
            QUOTER(ALIAS4DB._Field._Col-label-SA) + {&sep} +
            QUOTER(ALIAS4DB._Field._Desc) + {&sep} +
            QUOTER(ALIAS4DB._Field._Help) + {&sep} +
            QUOTER(ALIAS4DB._Field._Help-SA) + {&sep} +
            QUOTER(ALIAS4DB._Field._Decimals) + {&sep} +
            QUOTER(ALIAS4DB._Field._Charset) + {&sep} +
            QUOTER(ALIAS4DB._Field._Collation) + {&sep} +
            QUOTER(ALIAS4DB._Field._Fld-misc2[1]) + {&sep} +
            QUOTER(ALIAS4DB._Field._Fld-Misc3[1]) + {&sep} +
            QUOTER(ALIAS4DB._Field._Help-SA)
            SKIP.
    END.
    PUT STREAM str_out UNFORMATTED "#" SKIP.
END.

PUT STREAM str_out UNFORMATTED "#" SKIP.

OUTPUT STREAM str_out CLOSE.

RETURN "".
