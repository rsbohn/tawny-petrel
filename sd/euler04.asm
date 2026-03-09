****
** euler04.asm -- Largest palindrome from two 3-digit numbers
**
** Algorithm:
**   For I = 999 downto 100:
**     For J = I downto 100:
**       P = I * J  (32-bit via MPY)
**       If P <= BEST: break inner loop (J decreasing, no improvement)
**       If palindrome(P): BEST = P
**   Store result at >17FC (high) / >17FE (low)
**
** Register usage:
**   R00 = I (outer loop counter)
**   R01 = J (inner loop counter)
**   R02 = BEST_H (high word of best palindrome found)
**   R03 = BEST_L (low word)
**   R04 = P_H (product high / extraction work)
**   R05 = P_L (product low / extraction work)
**   R06 = Q_H  (step-1 DIV quotient: P_H / 10)
**   R07 = REM1 / Q_L (step-1 remainder, then step-2 quotient)
**   R08 = digit  (step-2 remainder)
**   R09 = 10  (constant divisor)
**   R10 = D1  (hundred-thousands digit)
**   R11 = D2  (ten-thousands)
**   R12 = D3  (thousands)
**   R13 = D4  (hundreds)
**   R14 = D5  (tens)
**   R15 = D6  (units)
**
** DIV two-step workaround:
**   P_HIGH can be up to 15 (for P=998001). Since 10 <= 15, a direct
**   DIV 10, R04 would overflow (ST4 set, no-op).
**   Instead: divide (0:P_H) by 10 first (0 < 10, safe), then
**   divide (REM1:P_L) by 10 (REM1 < 10, safe).
**
** P is saved to >17F8/>17FA before digit extraction so BEST can be
** updated after a palindrome is confirmed.
**
** Answer: 906609 = 913 * 993
**   >17FC = >000D (13 decimal)  high word
**   >17FE = >D2F1 (54129 decimal... wait: 906609 mod 65536 = 54641 = >D571)
**   Actually: 906609 = 13*65536 + 54641 = >000D:>D571
****

    AORG >0000
    DATA WPA        ; WP vector
    DATA START      ; PC vector

    AORG >1000      ; workspace
WPA BSS >20

    AORG >1800
START
    CLR  R02            ; BEST_H = 0
    CLR  R03            ; BEST_L = 0
    LI   R09, >000A     ; divisor = 10 (constant)
    LI   R00, >03E7     ; I = 999

OUTER
    CI   R00, >0064     ; I - 100
    JLT  DONE           ; I < 100: finished

    MOV  R00, R01       ; J = I

INNER
    CI   R01, >0064     ; J - 100
    JLT  NEXT_I         ; J < 100: next I

    ;; P = I * J (32-bit)
    MOV  R00, R04
    MPY  R01, R04       ; R04:R05 = I * J

    ;; Save P before digit extraction (needed to update BEST)
    MOV  R04, @>17F8
    MOV  R05, @>17FA

    ;; Compare P vs BEST; break inner loop if P <= BEST
    ;; C src, dst computes dst - src; JH = dst > src (unsigned)
    C    R02, R04       ; P_H - BEST_H
    JH   PALCHK         ; P_H > BEST_H: P > BEST
    JNE  NEXT_I         ; P_H != BEST_H means P_H < BEST_H: P < BEST
    C    R03, R05       ; P_H == BEST_H: compare P_L - BEST_L
    JH   PALCHK         ; P_L > BEST_L: P > BEST
    JMP  NEXT_I         ; P_L <= BEST_L: P <= BEST

PALCHK
    ;; Extract D6 (units digit)
    CLR  R06
    MOV  R04, R07
    DIV  R09, R06       ; (0:P_H)/10  -> R06=Q_H,  R07=REM1
    MOV  R05, R08
    DIV  R09, R07       ; (REM1:P_L)/10 -> R07=Q_L, R08=D6
    MOV  R08, R15
    MOV  R06, R04       ; P_H = Q_H
    MOV  R07, R05       ; P_L = Q_L

    ;; Extract D5 (tens digit)
    CLR  R06
    MOV  R04, R07
    DIV  R09, R06
    MOV  R05, R08
    DIV  R09, R07
    MOV  R08, R14
    MOV  R06, R04
    MOV  R07, R05

    ;; Extract D4 (hundreds digit)
    CLR  R06
    MOV  R04, R07
    DIV  R09, R06
    MOV  R05, R08
    DIV  R09, R07
    MOV  R08, R13
    MOV  R06, R04
    MOV  R07, R05

    ;; Extract D3 (thousands digit)
    CLR  R06
    MOV  R04, R07
    DIV  R09, R06
    MOV  R05, R08
    DIV  R09, R07
    MOV  R08, R12
    MOV  R06, R04
    MOV  R07, R05

    ;; Extract D2 (ten-thousands digit)
    CLR  R06
    MOV  R04, R07
    DIV  R09, R06
    MOV  R05, R08
    DIV  R09, R07
    MOV  R08, R11
    MOV  R06, R04
    MOV  R07, R05

    ;; D1 = remaining quotient (R05 after 5 extractions)
    MOV  R05, R10

    ;; Palindrome check: D1==D6, D2==D5, D3==D4
    C    R10, R15       ; D1 vs D6 (C src,dst: dst-src; JNE if != 0)
    JNE  NEXT_J
    C    R11, R14       ; D2 vs D5
    JNE  NEXT_J
    C    R12, R13       ; D3 vs D4
    JNE  NEXT_J

    ;; Palindrome found, P > BEST: update BEST from saved P
    MOV  @>17F8, R02    ; BEST_H = P_H
    MOV  @>17FA, R03    ; BEST_L = P_L

NEXT_J
    DEC  R01            ; J--
    JMP  INNER

NEXT_I
    DEC  R00            ; I--
    JMP  OUTER

DONE
    ;; Store result at >17FC / >17FE (same convention as factor.asm output)
    MOV  R02, @>17FC    ; high word
    MOV  R03, @>17FE    ; low word
    IDLE
