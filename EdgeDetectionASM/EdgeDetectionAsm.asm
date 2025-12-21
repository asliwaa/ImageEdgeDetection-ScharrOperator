; =================================================================================================
; Project Topic:     Edge Detection in Image using Scharr Operator
; Algorithm Desc:    Convolution algorithm using two 3x3 kernels (Gx, Gy) to calculate
;                    horizontal and vertical gradients. The result (magnitude) is the
;                    square root of the sum of squared gradients, normalized (divided by 8),
;                    and clamped to the 0-255 range.
;
; Date:              sem. 5, 2024/25
; Author:            Adam Œliwa
; Version:           Final
; =================================================================================================

.code

; =================================================================================================
; Procedure: ApplyScharrOperatorAsm
; Desc: Main procedure processing the image. Implements the Scharr operator
;       using SSE2 vector instructions (for square root calculation)
;       and "Sliding Window" optimization for pointer management.
;
; Input params:
;   RCX - pointer to the input image buffer (B, G, R, ...).
;   RDX - pointer to the output image buffer.
;   R8 - image width in pixels.
;   R9 - image height in pixels.
;   [RBP + 48] - stride, the actual row width in bytes (including padding).
;
; Output params:
;   None (result is written directly to the memory pointed to by RDX).
;
; Modified Registers:
;   Volatile:      RAX, RCX, RDX, R8, R9, R10, R11, XMM0, XMM1.
;   Preserved:     RBX, RSI, RDI, R12, R13, R14, R15, RBP, RSP (saved/restored per x64 ABI).
;   Flags:         ZF, SF, OF, CF, PF, AF (modified by arithmetic instructions).
; =================================================================================================

ApplyScharrOperatorAsm proc

    ; --- 1. PROLOG (Preserve Registers) ---
    push rbp                            ; Save old base pointer
    mov rbp, rsp                        ; Set new base pointer
    
    ; Save "Non-volatile" registers that must be preserved across the call
    push rbx                            ; General purpose (used for Y counter)
    push rsi                            ; Source Index (used for X counter)
    push rdi                            ; Destination Index (used for pixel offset)
    push r12                            ; Pointer: Top Row
    push r13                            ; Pointer: Middle Row
    push r14                            ; Pointer: Bottom Row
    push r15                            ; Pointer: Output Row

    ; --- 2. RETRIEVE DATA FROM STACK ---
    ; The 'stride' parameter is located on the stack above Shadow Space + Ret Addr + RBP
    movsxd r10, dword ptr [rbp + 48]    ; Load Stride with sign extension to 64-bit

    ; --- 3. DATA VALIDATION ---
    cmp r8, 3                           ; Check if width < 3
    jl Done                             ; If yes, exit (image too small for 3x3 filter)
    cmp r9, 3                           ; Check if height < 3
    jl Done                             ; If yes, exit

    ; --- 4. POINTER INITIALIZATION (SLIDING WINDOW) ---
    ; Set pointers to the start of the first 3x3 block
    mov r12, rcx                        ; R12 = Address of Row 0 (Input)
    
    mov r13, rcx                        ; Copy base address
    add r13, r10                        ; R13 = Address of Row 1 (Input) = Base + Stride
    
    mov r14, rcx                        ; Copy base address
    add r14, r10                        ; Add one stride
    add r14, r10                        ; R14 = Address of Row 2 (Input) = Base + 2*Stride

    mov r15, rdx                        ; Output buffer address
    add r15, r10                        ; R15 = Address of Row 1 (Output) - writing to the middle

    ; Initialize vertical loop counter (Y)
    mov rbx, r9                         ; Load Height into RBX
    sub rbx, 2                          ; Subtract 2 (skip top and bottom borders)
    cmp rbx, 0                          ; Check if there are rows to process
    jle Done                            ; If not, jump to Done

    dec r8                              ; Decrease Width by 1 (X loop boundary)

    ; --- 5. MAIN PROCESSING LOOP ---
LoopY:
    mov rsi, 1                          ; Set X counter to 1 (skip first column)

LoopX:
    cmp rsi, r8                         ; Compare X with (Width - 1)
    jge NextLine                        ; If end of row, go to next line

    ; Calculate pixel offset in bytes (BGR format = 3 bytes per pixel)
    mov rdi, rsi                        ; Copy X to RDI
    lea rdi, [rdi + rdi*2]              ; RDI = RDI * 3 (fast multiplication: x + 2x)

    ; Zero out gradient accumulators
    xor r11d, r11d                      ; R11D = Sum X (Horizontal Gradient) = 0
    xor ecx, ecx                        ; ECX  = Sum Y (Vertical Gradient) = 0

    ; =========================================================================
    ; SCHARR GRADIENT CALCULATION (Convolution)
    ; =========================================================================

    ; --- TOP ROW (Pointed by R12) ---
    
    ; Top-Left Pixel [x-1]: Weights Gx=-3, Gy=-3
    movzx eax, byte ptr [r12 + rdi - 3] ; Load Blue
    movzx edx, byte ptr [r12 + rdi - 2] ; Load Green
    add eax, edx                        ; Sum B+G
    movzx edx, byte ptr [r12 + rdi - 1] ; Load Red
    add eax, edx                        ; Sum B+G+R
    imul eax, 0AAABh                    ; Multiply by reciprocal (approx. division by 3)
    shr eax, 17                         ; Bitwise shift (EAX = Pixel Brightness)
    
    sub r11d, eax                       ; SumX -= 1 * Brightness
    sub r11d, eax                       ; SumX -= 2 * Brightness
    sub r11d, eax                       ; SumX -= 3 * Brightness (Gx = -3)
    sub ecx, eax                        ; SumY -= 3 * Brightness (Gy = -3)

    ; Top-Center Pixel [x]: Weights Gx=0, Gy=-10
    movzx eax, byte ptr [r12 + rdi]     ; Load B
    movzx edx, byte ptr [r12 + rdi + 1] ; Load G
    add eax, edx                        ; Sum
    movzx edx, byte ptr [r12 + rdi + 2] ; Load R
    add eax, edx                        ; Sum
    imul eax, 0AAABh                    ; Approx. division by 3
    shr eax, 17                         ; EAX = Brightness
    
    imul eax, 10                        ; Multiply brightness by weight 10
    sub ecx, eax                        ; SumY -= 10 * Brightness (Gy = -10)

    ; Top-Right Pixel [x+1]: Weights Gx=+3, Gy=-3
    movzx eax, byte ptr [r12 + rdi + 3] ; Load B
    movzx edx, byte ptr [r12 + rdi + 4] ; Load G
    add eax, edx                        ; Sum
    movzx edx, byte ptr [r12 + rdi + 5] ; Load R
    add eax, edx                        ; Sum
    imul eax, 0AAABh                    ; Division by 3
    shr eax, 17                         ; EAX = Brightness
    
    add r11d, eax                       ; SumX += 1 * Brightness
    add r11d, eax                       ; SumX += 2 * Brightness
    add r11d, eax                       ; SumX += 3 * Brightness (Gx = +3)
    sub ecx, eax                        ; SumY -= 1 * Brightness
    sub ecx, eax                        ; SumY -= 2 * Brightness
    sub ecx, eax                        ; SumY -= 3 * Brightness (Gy = -3)

    ; --- MIDDLE ROW (Pointed by R13) ---

    ; Middle-Left Pixel [x-1]: Weights Gx=-10, Gy=0
    movzx eax, byte ptr [r13 + rdi - 3] ; Retrieve and calculate brightness
    movzx edx, byte ptr [r13 + rdi - 2]
    add eax, edx
    movzx edx, byte ptr [r13 + rdi - 1]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10                        ; Weight 10
    sub r11d, eax                       ; SumX -= 10 * Brightness (Gx = -10)

    ; Middle-Right Pixel [x+1]: Weights Gx=+10, Gy=0
    movzx eax, byte ptr [r13 + rdi + 3] ; Retrieve and calculate brightness
    movzx edx, byte ptr [r13 + rdi + 4]
    add eax, edx
    movzx edx, byte ptr [r13 + rdi + 5]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10                        ; Weight 10
    add r11d, eax                       ; SumX += 10 * Brightness (Gx = +10)

    ; --- BOTTOM ROW (Pointed by R14) ---

    ; Bottom-Left Pixel [x-1]: Weights Gx=-3, Gy=+3
    movzx eax, byte ptr [r14 + rdi - 3] ; Retrieve and calculate brightness
    movzx edx, byte ptr [r14 + rdi - 2]
    add eax, edx
    movzx edx, byte ptr [r14 + rdi - 1]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    sub r11d, eax                       ; SumX -= 3 * Brightness (Gx = -3)
    sub r11d, eax
    sub r11d, eax
    add ecx, eax                        ; SumY += 3 * Brightness (Gy = +3)
    add ecx, eax
    add ecx, eax

    ; Bottom-Center Pixel [x]: Weights Gx=0, Gy=+10
    movzx eax, byte ptr [r14 + rdi]     ; Retrieve and calculate brightness
    movzx edx, byte ptr [r14 + rdi + 1]
    add eax, edx
    movzx edx, byte ptr [r14 + rdi + 2]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    imul eax, 10                        ; Weight 10
    add ecx, eax                        ; SumY += 10 * Brightness (Gy = +10)

    ; Bottom-Right Pixel [x+1]: Weights Gx=+3, Gy=+3
    movzx eax, byte ptr [r14 + rdi + 3] ; Retrieve and calculate brightness
    movzx edx, byte ptr [r14 + rdi + 4]
    add eax, edx
    movzx edx, byte ptr [r14 + rdi + 5]
    add eax, edx
    imul eax, 0AAABh
    shr eax, 17
    
    add r11d, eax                       ; SumX += 3 * Brightness (Gx = +3)
    add r11d, eax
    add r11d, eax
    add ecx, eax                        ; SumY += 3 * Brightness (Gy = +3)
    add ecx, eax
    add ecx, eax

    ; --- 6. MAGNITUDE CALCULATION (SSE) ---
    cvtsi2ss xmm0, r11d                 ; Convert SumX (int) to float (XMM0)
    mulss xmm0, xmm0                    ; XMM0 = SumX * SumX (Squared)
    
    cvtsi2ss xmm1, ecx                  ; Convert SumY (int) to float (XMM1)
    mulss xmm1, xmm1                    ; XMM1 = SumY * SumY (Squared)
    
    addss xmm0, xmm1                    ; XMM0 = SumX^2 + SumY^2
    sqrtss xmm0, xmm0                   ; XMM0 = Square Root (Magnitude)
    
    cvtss2si eax, xmm0                  ; Convert float result back to int (EAX)

    ; --- 7. NORMALIZATION AND CLAMPING ---
    sar eax, 3                          ; Division by 8 (Bitwise shift right) - Brightness normalization

    ; Clamping (Clip value to 0-255 range)
    cmp eax, 255                        ; Compare with 255
    jg SetMax                           ; If > 255, jump to SetMax
    cmp eax, 0                          ; Compare with 0
    jl SetMin                           ; If < 0, jump to SetMin
    jmp SavePixel                       ; If ok, proceed to save
SetMax:
    mov eax, 255                        ; Set value to 255
    jmp SavePixel
SetMin:
    xor eax, eax                        ; Set value to 0

SavePixel:
    ; --- 8. STORE RESULT ---
    ; Save the same grayscale value to 3 channels (B, G, R)
    mov [r15 + rdi], al                 ; Store Blue
    mov [r15 + rdi + 1], al             ; Store Green
    mov [r15 + rdi + 2], al             ; Store Red

    inc rsi                             ; Increment X counter (next pixel)
    jmp LoopX                           ; Jump to start of X loop

NextLine:
    ; --- 9. SLIDE WINDOW (Update Pointers) ---
    add r12, r10                        ; Move 'Top' pointer one row down
    add r13, r10                        ; Move 'Middle' pointer one row down
    add r14, r10                        ; Move 'Bottom' pointer one row down
    add r15, r10                        ; Move 'Output' pointer one row down
    
    dec rbx                             ; Decrement Y counter
    cmp rbx, 0                          ; Check if end of image
    jg LoopY                            ; If not, jump to start of Y loop

Done:
    ; --- 10. EPILOG (Restore Registers) ---
    pop r15                             ; Restore R15
    pop r14                             ; Restore R14
    pop r13                             ; Restore R13
    pop r12                             ; Restore R12
    pop rdi                             ; Restore RDI
    pop rsi                             ; Restore RSI
    pop rbx                             ; Restore RBX
    pop rbp                             ; Restore RBP
    ret                                 ; Return from procedure

ApplyScharrOperatorAsm endp
end