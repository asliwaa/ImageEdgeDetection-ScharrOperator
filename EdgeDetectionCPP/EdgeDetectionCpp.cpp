#include "pch.h"
#include "EdgeDetectionCpp.h"
#include <cmath>
#include <algorithm>

// ZMIANA: Dodano outputData jako argument
void ApplyScharrOperatorCpp(unsigned char* inputData, unsigned char* outputData, int width, int height, int stride)
{
    // Nie musimy ju¿ robiæ kopii (std::vector inputCopy)!
    // Czytamy z inputData, piszemy do outputData.

    int Gx[3][3] = { { -3, 0, 3 }, { -10, 0, 10 }, { -3, 0, 3 } };
    int Gy[3][3] = { { -3, -10, -3 }, { 0, 0, 0 }, { 3, 10, 3 } };

    for (int y = 1; y < height - 1; y++)
    {
        // WskaŸniki pomocnicze do INPUT (tylko odczyt)
        const unsigned char* rowPtrPrev = inputData + ((y - 1) * stride);
        const unsigned char* rowPtr = inputData + (y * stride);
        const unsigned char* rowPtrNext = inputData + ((y + 1) * stride);

        const unsigned char* rows[3] = { rowPtrPrev, rowPtr, rowPtrNext };

        // WskaŸnik do OUTPUT (zapis)
        unsigned char* outRowPtr = outputData + (y * stride);

        for (int x = 1; x < width - 1; x++)
        {
            int sumX = 0;
            int sumY = 0;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    // (x + (j-1)) to przesuniêcie w pikselach. *3 bo BGR.
                    int pixelOffset = (x + (j - 1)) * 3;

                    unsigned char b = rows[i][pixelOffset];
                    unsigned char g = rows[i][pixelOffset + 1];
                    unsigned char r = rows[i][pixelOffset + 2];

                    int grayVal = (r + g + b) / 3;

                    sumX += grayVal * Gx[i][j];
                    sumY += grayVal * Gy[i][j];
                }
            }

            int magnitude = (int)std::sqrt((sumX * sumX) + (sumY * sumY));
            if (magnitude > 255) magnitude = 255;
            if (magnitude < 0) magnitude = 0;

            // Zapisujemy wynik do bufora wyjœciowego
            int outOffset = x * 3;
            outRowPtr[outOffset] = (unsigned char)magnitude;
            outRowPtr[outOffset + 1] = (unsigned char)magnitude;
            outRowPtr[outOffset + 2] = (unsigned char)magnitude;
        }
    }
}