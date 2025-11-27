using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace EdgeDetectionApp
{
    public partial class MainWindow : Window
    {
        // Import funkcji z DLL w C++
        [DllImport("EdgeDetectionCPP.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ApplyScharrOperator(IntPtr imageData, int width, int height, int stride);

        //Zmienna, która będzie przechowywać zmienione wartości pikseli oryginalnego obrazu, zainicjalizowana jako pusta zmienna
        private WriteableBitmap? processingBitmap;

        public MainWindow()
        {
            InitializeComponent();
            //Przycisk run ustawiony domyślnie na wyłączony
            btnRun.IsEnabled = false;
        }

        //Metoda obsługująca logikę działania przycisku ładowania obrazu do przetworzenia
        private void btnImageUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Image files | *.png;*.jpg;*.bmp";

            if (fileDialog.ShowDialog() == true)
            {
                //Załadowanie oryginalnego obrazu na ekran użytkownika
                BitmapImage tempBitmap = new BitmapImage(new Uri(fileDialog.FileName));
                imgUploaded.Source = tempBitmap;

                //Czyszczenie klasy image i zmiennej przechowującej przetworzony obraz (konieczne zwłaszcza jeżeli wcześniej już przetwarzaliśmy jakiś obraz)
                imgConverted.Source = null;
                processingBitmap = null;

                //Aktywacja przycisków wyboru biblioteki DLL
                rdBtnCpp.IsEnabled = true;
                rdBtnAsm.IsEnabled = true;

                //Sprawdzenie czy można już uruchomić (jeśli user wcześniej zaznaczył radio button)
                CheckRunButton();
            }
        }

        //Metoda obsługująca logikę działania przycisku uruchomienia przetwarzania obrazu
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (rdBtnCpp.IsChecked == true)
            {
                RunCppAlgorithm();
            }
            else if (rdBtnAsm.IsChecked == true)
            { 
                MessageBox.Show("ASM not implemented yet.");
            }
        }

        //Metoda obsługująca logikę przesłania obrazu do DLL w C++, odebrania przetworzonych danych i wyświetlenia gotowego obrazu
        private void RunCppAlgorithm()
        {
            if (imgUploaded.Source is not BitmapImage originalImage) return;

            // Tworzymy kopię roboczą ze świeżego oryginału
            FormatConvertedBitmap converter = new FormatConvertedBitmap();
            converter.BeginInit();
            converter.Source = originalImage;
            converter.DestinationFormat = PixelFormats.Bgr24;
            converter.EndInit();

            processingBitmap = new WriteableBitmap(converter);

            // Blokada pamięci
            processingBitmap.Lock();

            // Wywołanie C++
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ApplyScharrOperator(processingBitmap.BackBuffer, processingBitmap.PixelWidth, processingBitmap.PixelHeight, processingBitmap.BackBufferStride);
            watch.Stop();

            // Odblokowanie
            processingBitmap.AddDirtyRect(new Int32Rect(0, 0, processingBitmap.PixelWidth, processingBitmap.PixelHeight));
            processingBitmap.Unlock();

            // Wyświetlenie wyniku
            imgConverted.Source = processingBitmap;

            MessageBox.Show($"Czas wykonania C++: {watch.ElapsedMilliseconds} ms");
        }

        //Metoda obsługująca logikę wyboru DLL w C++
        private void rdBtnCpp_Click(object sender, RoutedEventArgs e)
        {
            CheckRunButton();
        }

        //Metoda obsługująca logikę wyboru DLL w asm

        private void rdBtnAsm_Click(object sender, RoutedEventArgs e)
        {
            CheckRunButton();
        }

        //Metoda sprawdzająca czy można pozwolić użytkownikowi uruchomić algorytm
        private void CheckRunButton()
        {
            // Logika: Run aktywny <=> (MamyObrazek AND WybranoAlgorytm)
            btnRun.IsEnabled = (imgUploaded.Source != null) && IsAnyAlgorithmSelected();
        }

        //Metoda sprawdzająca czy użytkownik wybrał jakikolwiek algorytm C++ lub ASM
        private bool IsAnyAlgorithmSelected()
        {
            return (rdBtnCpp.IsChecked == true || rdBtnAsm.IsChecked == true);
        }
    }
}