.code

; ==============================================================================
; Procedura: ApplyScharrOperatorAsm
; Wersja: INLINED (Bez wywo³añ call w pêtli) + Sliding Window
; ==============================================================================

ApplyScharrOperatorAsm proc

    ; --- 1. PROLOG (Rêczne zarz¹dzanie stosem) ---
    push rbp
    mov rbp, rsp
    
    ; Zapisujemy rejestry, których bêdziemy u¿ywaæ (Non-volatile)
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15

    ; --- 2. POBRANIE DANYCH ---
    ; RCX = InputPtr
    ; RDX = OutputPtr
    ; R8  = Width
    ; R9  = Height
    ; [rbp + 48] = Stride (5. argument, le¿y na stosie nad Shadow Space)
    
    ; Pobieramy Stride jako liczbê 32-bit ze znakiem i rozszerzamy do 64-bit
    movsxd r10, dword ptr [rbp + 48] 

    ; Sprawdzenie wymiarów (min 3x3)
    cmp r8, 3
    jl Done
    cmp r9, 3
    jl Done

    ; --- 3. PRZYGOTOWANIE WSKANIKÓW (Sliding Window) ---
    ; R12 = Wiersz Górny (Input)
    mov r12, rcx
    
    ; R13 = Wiersz Œrodkowy (Input)
    mov r13, rcx
    add r13, r10
    
    ; R14 = Wiersz Dolny (Input)
    mov r14, rcx
    add r14, r10
    add r14, r10

    ; R15 = Wiersz Wyjœciowy (Output - œrodek)
    mov r15, rdx
    add r15, r10

    ; Licznik pêtli Y (Height - 2)
    mov rbx, r9
    sub rbx, 2
    cmp rbx, 0
    jle Done

    ; Szerokoœæ - 1 (do pêtli X)
    dec r8

LoopY:
    ; RSI = Licznik X (zaczynamy od 1)
    mov rsi, 1

LoopX:
    cmp rsi, r8         ; Czy x >= Width - 1?
    jge NextLine

    ; RDI = Offset piksela (x * 3)
    mov rdi, rsi
    lea rdi, [rdi + rdi*2] ; Szybkie mno¿enie przez 3 (rdi = rdi + 2*rdi)

    ; Zerujemy sumy
    xor r11d, r11d      ; SumX (u¿ywamy R11D jako akumulatora)
    xor ecx, ecx        ; SumY (u¿ywamy ECX jako akumulatora)

    ; ==========================================================
    ; BLOK OBLICZEÑ (INLINED)
    ; Zamiast "call", kod jest tutaj.
    ; U¿ywamy EAX jako tymczasowego rejestru na jasnoœæ.
    ; ==========================================================

    ; --- WIERSZ GÓRNY (R12) ---
    
    ; Lewy (-1): GX=-3, GY=-3
    ; Pobranie piksela [R12 + RDI - 3]
    movzx eax, byte ptr [r12 + rdi - 3]     ; B
    movzx edx, byte ptr [r12 + rdi - 2]     ; G
    add eax, edx
    movzx edx, byte ptr [r12 + rdi - 1]     ; R
    add eax, edx
    imul eax, 0AAABh                        ; Dzielenie przez 3
    shr eax, 17                             ; EAX = Jasnoœæ
    
    sub r11d, eax ; SumX -= 1x
    sub r11d, eax ; -2x
    sub r11d, eax ; -3x
    sub ecx, eax  ; SumY -= 3x

    ; Œrodkowy (0): GX=0, GY=-10
    movzx eax, byte ptr [r12 + rdi]
    movzx edx, byte ptr [r12 + rdi + 1]
    add eax, edx
    movzx edx, byte ptr [r12 + rdi + 2]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10
    sub ecx, eax  ; SumY -= 10x

    ; Prawy (+1): GX=+3, GY=-3
    movzx eax, byte ptr [r12 + rdi + 3]
    movzx edx, byte ptr [r12 + rdi + 4]
    add eax, edx
    movzx edx, byte ptr [r12 + rdi + 5]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    add r11d, eax ; SumX += 1x
    add r11d, eax ; +2x
    add r11d, eax ; +3x
    sub ecx, eax  ; SumY -= 1x
    sub ecx, eax  ; -2x
    sub ecx, eax  ; -3x

    ; --- WIERSZ ŒRODKOWY (R13) ---

    ; Lewy (-1): GX=-10, GY=0
    movzx eax, byte ptr [r13 + rdi - 3]
    movzx edx, byte ptr [r13 + rdi - 2]
    add eax, edx
    movzx edx, byte ptr [r13 + rdi - 1]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10
    sub r11d, eax ; SumX -= 10x

    ; Prawy (+1): GX=+10, GY=0
    movzx eax, byte ptr [r13 + rdi + 3]
    movzx edx, byte ptr [r13 + rdi + 4]
    add eax, edx
    movzx edx, byte ptr [r13 + rdi + 5]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10
    add r11d, eax ; SumX += 10x

    ; --- WIERSZ DOLNY (R14) ---

    ; Lewy (-1): GX=-3, GY=+3
    movzx eax, byte ptr [r14 + rdi - 3]
    movzx edx, byte ptr [r14 + rdi - 2]
    add eax, edx
    movzx edx, byte ptr [r14 + rdi - 1]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    sub r11d, eax ; SumX -= 3x
    sub r11d, eax
    sub r11d, eax
    add ecx, eax  ; SumY += 3x
    add ecx, eax
    add ecx, eax

    ; Œrodkowy (0): GX=0, GY=+10
    movzx eax, byte ptr [r14 + rdi]
    movzx edx, byte ptr [r14 + rdi + 1]
    add eax, edx
    movzx edx, byte ptr [r14 + rdi + 2]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10
    add ecx, eax  ; SumY += 10x

    ; Prawy (+1): GX=+3, GY=+3
    movzx eax, byte ptr [r14 + rdi + 3]
    movzx edx, byte ptr [r14 + rdi + 4]
    add eax, edx
    movzx edx, byte ptr [r14 + rdi + 5]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    add r11d, eax ; SumX += 3x
    add r11d, eax
    add r11d, eax
    add ecx, eax  ; SumY += 3x
    add ecx, eax
    add ecx, eax

    ; --- MAGNITUDA (SSE) ---
    cvtsi2ss xmm0, r11d ; SumX -> float
    mulss xmm0, xmm0    ; ^2
    cvtsi2ss xmm1, ecx  ; SumY -> float
    mulss xmm1, xmm1    ; ^2
    addss xmm0, xmm1    ; Sum^2
    sqrtss xmm0, xmm0   ; Sqrt
    cvtss2si eax, xmm0  ; -> int

    ; Clamping (Przyciêcie do 0-255)
    cmp eax, 255
    jg SetMax
    cmp eax, 0
    jl SetMin
    jmp SavePixel
SetMax:
    mov eax, 255
    jmp SavePixel
SetMin:
    xor eax, eax

SavePixel:
    ; Zapisz wynik (B, G, R)
    ; R15 = WskaŸnik wyjœciowy wiersza, RDI = Offset
    mov [r15 + rdi], al
    mov [r15 + rdi + 1], al
    mov [r15 + rdi + 2], al

    ; Nastêpny piksel
    inc rsi
    jmp LoopX

NextLine:
    ; Przesuwamy okno o jeden wiersz w dó³
    add r12, r10
    add r13, r10
    add r14, r10
    add r15, r10
    
    dec rbx
    cmp rbx, 0
    jg LoopY

Done:
    ; --- EPILOG ---
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
    pop rbp
    ret

ApplyScharrOperatorAsm endp
end