/*
 =================================================================================================
 Project Topic:     Edge Detection in Image using Scharr Operator
 Algorithm Desc:    C++ implementation of the Scharr operator. The algorithm calculates the
                    convolution of the image with Gx and Gy kernels, then determines the
                    gradient magnitude. The result is normalized (divided by 8) and
                    clamped to 0-255.

 Date:              sem. 5, 2024/25
 Author:            Adam Œliwa
 Version:           Final
 =================================================================================================
*/

#include "pch.h"
#include "EdgeDetectionCpp.h"
#include <cmath>
#include <algorithm>

/*
 * Function: ApplyScharrOperatorCpp
 * Desc: Main C++ library function for image processing. Uses pointers for direct access to image memory.
 *
 * Input params:
 *   inputData  - Pointer to the first byte of input image data (B, G, R...).
 *   outputData - Pointer to the first byte of the output buffer.
 *   width - Image width in pixels.
 *   height - Image height in pixels.
 *   stride - Actual row width in bytes (including padding).
 *
 * Output params:
 *   Result is written to outputData.
*/
void ApplyScharrOperatorCpp(unsigned char* inputData, unsigned char* outputData, int width, int height, int stride)
{
    //Definition of Scharr operator kernels
    //Gx
    int Gx[3][3] = {
        { -3, 0,  3 },
        { -10, 0, 10 },
        { -3, 0,  3 }
    };

    //Gy
    int Gy[3][3] = {
        { -3, -10, -3 },
        {  0,   0,  0 },
        {  3,  10,  3 }
    };

    //Loop over image rows (skip 1 px margin from top and bottom)
    for (int y = 1; y < height - 1; y++)
    {
        //Calculating row addresses once per Y iteration
        //Row above
        const unsigned char* rowPtrPrev = inputData + ((y - 1) * stride);
        //Current row
        const unsigned char* rowPtr = inputData + (y * stride);
        //Row below
        const unsigned char* rowPtrNext = inputData + ((y + 1) * stride);

        //Helper array for easier access within the loop
        const unsigned char* rows[3] = { rowPtrPrev, rowPtr, rowPtrNext };

        //Pointer for writing to the output buffer
        unsigned char* outRowPtr = outputData + (y * stride);

        //Loop over columns (skip 1 px margin from left and right)
        for (int x = 1; x < width - 1; x++)
        {
            int sumX = 0; //Horizontal gradient sum
            int sumY = 0; //Vertical gradient sum

            //Convolution with 3x3 kernel
            for (int i = 0; i < 3; i++) //Loop over kernel rows
            {
                for (int j = 0; j < 3; j++) //Loop over kernel columns
                {
                    //Calculate byte offset for the neighboring pixel
                    int pixelOffset = (x + (j - 1)) * 3;

                    //Retrieve color components
                    unsigned char b = rows[i][pixelOffset];
                    unsigned char g = rows[i][pixelOffset + 1];
                    unsigned char r = rows[i][pixelOffset + 2];

                    //Convert to grayscale (arithmetic mean)
                    int grayVal = (r + g + b) / 3;

                    //Accumulate results for both kernels
                    sumX += grayVal * Gx[i][j];
                    sumY += grayVal * Gy[i][j];
                }
            }

            //Calculate gradient magnitude
            //Dividing by 8 for brightness normalization
            int magnitude = (int)(std::sqrt((sumX * sumX) + (sumY * sumY)) / 8);

            //Clamping the image while ensuring value stays within byte range
            if (magnitude > 255) magnitude = 255;
            if (magnitude < 0) magnitude = 0;

            //Write result to output buffer
            int outOffset = x * 3;
            outRowPtr[outOffset] = (unsigned char)magnitude; //B
            outRowPtr[outOffset + 1] = (unsigned char)magnitude; //G
            outRowPtr[outOffset + 2] = (unsigned char)magnitude; //R
        }
    }
}