#include "pch.h"
#include "EdgeDetectionCpp.h"
#include <cmath>
#include <vector>
#include <algorithm> // dla std::min, std::max

void ApplyScharrOperator(unsigned char* imageData, int width, int height, int stride)
{
    //Kopia bufora obrazu, z niej algorytm odczytuje dane, ale zapisuje ju¿ do imageData
    //to zapobiega nadpisywaniu danych koniecznych do wykonywania obliczeñ
    std::vector<unsigned char> inputCopy(imageData, imageData + (height * stride));

    // Macierze operatora Scharra
    // Gx - wykrywa krawêdzie pionowe
    int Gx[3][3] = {
        { -3, 0,  3 },
        { -10, 0, 10 },
        { -3, 0,  3 }
    };

    // Gy - wykrywa krawêdzie poziome
    int Gy[3][3] = {
        { -3, -10, -3 },
        {  0,   0,  0 },
        {  3,  10,  3 }
    };

    // Iteracja po pixelach obrazu, zaczynamy od y=1 i koñczymy na height-2.
    // Pomijamy ramkê obrazu o szerokoœci 1 piksela, ¿eby nie wyjœæ poza tablicê
    // przy sprawdzaniu s¹siadów (s¹siad -1 lub +1).
    for (int y = 1; y < height - 1; y++)
    {
        // Obliczamy wskaŸniki na pocz¹tek wierszy w kopii (optymalizacja)
        // Dziêki temu w pêtli X nie musimy mno¿yæ y * stride za ka¿dym razem
        unsigned char* rowPtr = inputCopy.data() + (y * stride);

        // WskaŸniki na wiersz powy¿ej i poni¿ej (dla s¹siadów)
        unsigned char* rowPtrPrev = inputCopy.data() + ((y - 1) * stride);
        unsigned char* rowPtrNext = inputCopy.data() + ((y + 1) * stride);

        // WskaŸnik do zapisu wyniku (w oryginalnym obrazie)
        unsigned char* outRowPtr = imageData + (y * stride);

        for (int x = 1; x < width - 1; x++)
        {
            int sumX = 0;
            int sumY = 0;

            // 3. KONWOLUCJA (SPLOT)
            // Musimy odwiedziæ okno 3x3 wokó³ naszego piksela (x, y)

            // ¯eby uproœciæ kod, zdefiniujmy sobie pomocnicz¹ tablicê wskaŸników do 3 wierszy, które nas interesuj¹
            unsigned char* rows[3] = { rowPtrPrev, rowPtr, rowPtrNext };

            for (int i = 0; i < 3; i++) // pêtla po wierszach maski (-1, 0, 1)
            {
                for (int j = 0; j < 3; j++) // pêtla po kolumnach maski (-1, 0, 1)
                {
                    // Obliczamy przesuniêcie w poziomie dla s¹siada.
                    // x to nasz piksel œrodkowy. (j-1) da nam przesuniêcie -1, 0 lub +1 pikseli.
                    // Mno¿ymy * 3, bo jeden piksel ma 3 bajty (BGR).
                    int pixelOffset = (x + (j - 1)) * 3;

                    // Pobieramy kolory s¹siada z kopii
                    unsigned char b = rows[i][pixelOffset];
                    unsigned char g = rows[i][pixelOffset + 1];
                    unsigned char r = rows[i][pixelOffset + 2];

                    // Konwersja na odcieñ szaroœci (œrednia arytmetyczna dla prostoty)
                    // Mo¿na te¿ u¿yæ wzoru: 0.299*R + 0.587*G + 0.114*B
                    int grayVal = (r + g + b) / 3;

                    // Akumulacja wyniku
                    sumX += grayVal * Gx[i][j];
                    sumY += grayVal * Gy[i][j];
                }
            }

            // 4. MAGNITUDA (SI£A KRAWÊDZI)
            // Obliczamy pierwiastek z sumy kwadratów (twierdzenie Pitagorasa dla wektorów Gx, Gy)
            int magnitude = (int)std::sqrt((sumX * sumX) + (sumY * sumY));

            // Zabezpieczenie przed wyjœciem poza zakres 0-255 (Clamping)
            if (magnitude > 255) magnitude = 255;
            if (magnitude < 0) magnitude = 0;

            // 5. ZAPIS WYNIKU
            // Zapisujemy szar¹ wartoœæ do wszystkich 3 kana³ów (R, G, B), ¿eby obraz by³ czarno-bia³y
            int outOffset = x * 3;
            outRowPtr[outOffset] = (unsigned char)magnitude; // Blue
            outRowPtr[outOffset + 1] = (unsigned char)magnitude; // Green
            outRowPtr[outOffset + 2] = (unsigned char)magnitude; // Red
        }
    }
}