****
** MMIO UART
THRE    EQU >F000   ; UART transmit empty
THRL    EQU >F002   ; UART transmit load

** Reset Vector
    AORG >0000
    DATA >1000
    DATA >0080

** Main loop
    RORG >0080
START
    LI 2,MESSAGE
    BL @PRINTSTR
    LI 2,CRLF
    BL @PRINTSTR
    JMP START

* Print the null terminated string at R2.
PRINTSTR
* wait for THRE (transmit empty)
UBUSY
    MOV @THRE, 1
    ANDI 1, >00001
    JNE UBUSY
* send *2
    MOV *2, 1
    MOV 1, @THRL
* advance R2 pointer
* stop at null terminator 0
    INCT 2
    MOV *2, 1
    JNE UBUSY
    RT

MESSAGE
    DATA >0041  ; A
    DATA >006C  ; l
    DATA >006C  ; l
    DATA >0020  ; (space)
    DATA >0077  ; w
    DATA >006F  ; o
    DATA >0072  ; r
    DATA >006B  ; k
    DATA >0020  ; (space)
    DATA >0061  ; a
    DATA >006E  ; n
    DATA >0064  ; d
    DATA >0020  ; (space)
    DATA >006E  ; n
    DATA >006F  ; o
    DATA >0020  ; (space)
    DATA >0070  ; p
    DATA >006C  ; l
    DATA >0061  ; a
    DATA >0079  ; y
    DATA >0020  ; (space)
    DATA >006D  ; m
    DATA >0061  ; a
    DATA >006B  ; k
    DATA >0065  ; e
    DATA >0020  ; (space)
    DATA >004A  ; J
    DATA >0061  ; a
    DATA >0063  ; c
    DATA >006B  ; k
    DATA >0020  ; (space)
    DATA >0061  ; a
    DATA >0020  ; (space)
    DATA >0064  ; d
    DATA >0075  ; u
    DATA >006C  ; l
    DATA >006C  ; l
    DATA >0020  ; (space)
    DATA >0062  ; b
    DATA >006F  ; o
    DATA >0079  ; y
    DATA >002E  ; .
    DATA >0000  ; null

CRLF
    DATA >000D  ; CR
    DATA >000A  ; LF
    DATA >0000  ; null