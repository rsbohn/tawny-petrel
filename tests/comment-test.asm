* Leading asterisk comment should be ignored
ORG 0100
DATA >1234 ; inline comment after operand
* Another full-line comment
DATA >5678
; Semicolon-only comment line
DATA >9ABC ; another comment
TXT /HELLO;WORLD/ ; semicolon inside TXT literal
END
