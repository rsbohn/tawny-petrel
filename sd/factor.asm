****
** factor.asm -- factor 32 bit numbers
****
    AORG >0000
    DATA WPA    ; WP
    DATA START  ; PC

    AORG >1000  ; workspaces
WPA BSS >20
WPB BSS >20

    AORG >1800
START
    LI 3, >0010
    LI 4, >0000
    LI 5, >0003
    DIV 5,3
    IDLE
    SOC 1,0