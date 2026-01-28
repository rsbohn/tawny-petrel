* a simple addition problem
    AORG >0000   ; reset vector
    DATA >0000  ; WP
    DATA >1000  ; PC

    RORG >1000
    LI 2, >0FFF
    LI 3, >0001
; add 1 to R2 until carry is set
LOOP:
    A 3, 2
    JNC LOOP
    IDLE
