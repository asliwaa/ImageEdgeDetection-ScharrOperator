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
        // DllImport dla C++
        [DllImport("EdgeDetectionCPP.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ApplyScharrOperatorCpp")]
        public static extern void ApplyScharrOperatorCpp(IntPtr inputPtr, IntPtr outputPtr, int width, int height, int stride);

        // DllImport dla ASM (zauważ nową nazwę DLL i funkcji)
        [DllImport("EdgeDetectionASM.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ApplyScharrOperatorAsm")]
        public static extern void ApplyScharrOperatorAsm(IntPtr inputPtr, IntPtr outputPtr, int width, int height, int stride);

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
            if (rdBtnCpp.IsChecked == true) RunAlgorithm(false); // C++
            else if (rdBtnAsm.IsChecked == true) RunAlgorithm(true); // ASM
        }

        //Metoda obsługująca logikę przesłania obrazu do DLL w C++, odebrania przetworzonych danych i wyświetlenia gotowego obrazu
        private void RunAlgorithm(bool useAsm)
        {
            if (imgUploaded.Source is not BitmapImage originalImage) return;

            // 1. Przygotowanie WEJŚCIA (Input)
            // Konwertujemy oryginał na Bgr24
            FormatConvertedBitmap converter = new FormatConvertedBitmap();
            converter.BeginInit();
            converter.Source = originalImage;
            converter.DestinationFormat = PixelFormats.Bgr24;
            converter.EndInit();

            // InputBitmap - z tego czytamy
            WriteableBitmap inputBitmap = new WriteableBitmap(converter);

            // 2. Przygotowanie WYJŚCIA (Output)
            // Tworzymy pustą bitmapę o tych samych wymiarach
            WriteableBitmap outputBitmap = new WriteableBitmap(
                inputBitmap.PixelWidth,
                inputBitmap.PixelHeight,
                inputBitmap.DpiX,
                inputBitmap.DpiY,
                PixelFormats.Bgr24,
                null); // null = pusta

            // 3. Blokada pamięci obu obrazów
            inputBitmap.Lock();
            outputBitmap.Lock();

            // Dane do przekazania
            IntPtr inPtr = inputBitmap.BackBuffer;
            IntPtr outPtr = outputBitmap.BackBuffer;
            int w = inputBitmap.PixelWidth;
            int h = inputBitmap.PixelHeight;
            int stride = inputBitmap.BackBufferStride;

            // 4. Wykonanie (Mierzymy czas)
            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (useAsm)
            {
                try
                {
                    ApplyScharrOperatorAsm(inPtr, outPtr, w, h, stride);
                }
                catch (DllNotFoundException)
                {
                    MessageBox.Show("Nie znaleziono pliku EdgeDetectionASM.dll!");
                }
            }
            else
            {
                ApplyScharrOperatorCpp(inPtr, outPtr, w, h, stride);
            }

            watch.Stop();

            // 5. Sprzątanie
            // Oznaczamy output jako zmieniony, żeby WPF go odświeżył
            outputBitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
            inputBitmap.Unlock();
            outputBitmap.Unlock();

            // Przypisanie wyniku
            imgConverted.Source = outputBitmap;

            // Informacja o czasie
            string lang = useAsm ? "ASM" : "C++";
            MessageBox.Show($"Czas wykonania {lang}: {watch.ElapsedMilliseconds} ms");
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