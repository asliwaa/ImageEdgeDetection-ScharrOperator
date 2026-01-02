# Image Edge Detection - Scharr Operator

A WPF desktop application project demonstrating edge detection using the **Scharr Operator**. The main goal of this project is to benchmark the performance of two implementations of the same algorithm: a high-level C++ implementation and a low-level x64 Assembly implementation using SSE vector instructions.

**New in v2.0:** The project now supports **Multi-threading**, allowing users to analyze performance scaling across multiple CPU cores.

## Project Structure

The solution consists of three main modules:

1.  **EdgeDetectionApp** (C# / WPF)
    * Client application with a Graphical User Interface (GUI).
    * **Multi-threading Manager:** Handles the logic of splitting the image into horizontal strips and processing them in parallel using `Parallel.For`.
    * **UI Controls:** Includes a slider to adjust the number of threads (1–64) dynamically.
    * Communicates with DLL libraries using the P/Invoke (`DllImport`) mechanism.

2.  **EdgeDetectionCPP** (C++ DLL)
    * Dynamic library implementing the Scharr operator in standard C++.
    * Stateless implementation designed to process specific memory buffers (compatible with multi-threaded calls from C#).

3.  **EdgeDetectionASM** (MASM x64 DLL)
    * Dynamic library written in x64 Assembly.
    * Utilizes SIMD instructions (SSE: `mulss`, `addss`, `sqrtss`) for parallel data processing.
    * Optimized for performance (sliding window technique, no function calls inside the inner loop).

## Features

* **Image Loading:** Supports common formats (JPG, PNG, BMP).
* **Multi-threading:** Adjustable thread count (1 to 64) via a UI slider to utilize multi-core processors.
* **Implementation Selection:** Ability to switch between C++ and ASM libraries in real-time.
* **Benchmark:** Automatic measurement of algorithm execution time (in milliseconds) displayed in the status bar.
* **Live Preview:** Side-by-side display of the original image and the processed result.

## Requirements

* **Operating System:** Windows 64-bit.
* **Environment:** Visual Studio.
* **Frameworks:**
    * .NET 8.0 (for the WPF application).
    * Platform Toolset v143 (for C++ and ASM).
* **CPU Architecture:** x64 (required due to the assembly code).

## Build and Run

1.  Open the `EdgeDetectionScharrSolution.sln` solution file in Visual Studio.
2.  Change the platform configuration to **x64** (this is crucial as the ASM code is written for 64-bit architecture).
3.  Change the build configuration to **Release** for accurate performance benchmarking.
4.  Build the solution (**Build -> Build Solution**).
    * A *Post-Build script* will automatically copy the `EdgeDetectionCPP.dll` and `EdgeDetectionASM.dll` files to the application's output folder.
5.  Run the `EdgeDetectionApp` project.

## Parallel Processing Strategy
1.  The C# application calculates the height of the image chunks based on the selected thread count.

2.  The image is logically divided into horizontal strips.

3.  Parallel.For is used to invoke the selected DLL function (ASM or C++) for each strip concurrently.

4.  Each thread processes its assigned memory region independently, writing directly to the output buffer (Caller Allocates pattern).

### Scharr Operator
The Scharr operator is an improved version of the Sobel operator, offering better rotational symmetry. Edge detection involves convolving the image with two 3x3 masks:

**Gx (Horizontal Derivative):**
```text
 -3   0   3
-10   0  10
 -3   0   3

**Gy (Vertical Derivative):**
```text
 -3 -10  -3
  0   0   0
  3  10   3

