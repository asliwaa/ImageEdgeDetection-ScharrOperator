/*
 =================================================================================================
 Project Topic:     Edge Detection in Image using Scharr Operator
 Algorithm Desc:    WPF Window Application serving as a user interface for DLL libraries.
                    Responsible for loading the image, preparing memory buffers,
                    invoking external functions (C++/ASM), and presenting results.
 
 Date:              sem. 5, 2024/25
 Author:            Adam Śliwa
 Version:           Final
 =================================================================================================
*/

using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks; // Potrzebne do Parallel.For

namespace EdgeDetectionApp
{
    public partial class MainWindow : Window
    {
        [DllImport("EdgeDetectionCPP.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ApplyScharrOperatorCpp")]
        public static extern void ApplyScharrOperatorCpp(IntPtr inputPtr, IntPtr outputPtr, int width, int height, int stride);

        [DllImport("EdgeDetectionASM.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ApplyScharrOperatorAsm")]
        public static extern void ApplyScharrOperatorAsm(IntPtr inputPtr, IntPtr outputPtr, int width, int height, int stride);

        public MainWindow()
        {
            InitializeComponent();
            btnRun.IsEnabled = false;
            UpdateThreadLabel();
        }

        /*
         * Method: btnImageUpload_Click
         * Desc: Handles the "Upload Image" button click event. Opens a system file dialog 
         *       to select an image, displays the original image in the UI, and resets 
         *       the application state for a new processing run.
         * Input params:
         * sender - The object that raised the event.
         * e - Event arguments.
         */
        private void btnImageUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Image files | *.png;*.jpg;*.bmp";

            if (fileDialog.ShowDialog() == true)
            {
                BitmapImage tempBitmap = new BitmapImage(new Uri(fileDialog.FileName));
                imgUploaded.Source = tempBitmap;
                imgConverted.Source = null;

                rdBtnCpp.IsEnabled = true;
                rdBtnAsm.IsEnabled = true;
                CheckRunButton();
                lblStatus.Text = "Image loaded. Ready to process.";
            }
        }

        /*
         * Method: sliderThreads_ValueChanged
         * Desc: Event handler for the thread count slider. Automatically updates the UI label 
         *       to reflect the currently selected number of threads whenever the slider moves.
         * Input params:
         * sender - The object that raised the event.
         * e - Event arguments containing the new value.
         */
        private void sliderThreads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateThreadLabel();
        }

        /*
         * Method: UpdateThreadLabel
         * Desc: Helper method to synchronize the thread count label content with the 
         *       current integer value of the slider.
         */
        private void UpdateThreadLabel()
        {
            if (lblThreads != null)
                lblThreads.Content = ((int)sliderThreads.Value).ToString();
        }

        /*
         * Method: RunAlgorithm
         * Desc: Core method that prepares memory buffers and executes the edge detection algorithm.
         *       Implements multi-threading logic by splitting the image into horizontal strips
         *       and processing them in parallel using Parallel.For.
         *       Uses the "Caller Allocates" pattern.
         * Input params:
         * useAsm - Algorithm selection flag: true = ASM library, false = C++ library.
         */
        private void RunAlgorithm(bool useAsm)
        {
            if (imgUploaded.Source is not BitmapImage originalImage) return;

            FormatConvertedBitmap converter = new FormatConvertedBitmap();
            converter.BeginInit();
            converter.Source = originalImage;
            converter.DestinationFormat = PixelFormats.Bgr24;
            converter.EndInit();

            WriteableBitmap inputBitmap = new WriteableBitmap(converter);
            WriteableBitmap outputBitmap = new WriteableBitmap(
                inputBitmap.PixelWidth,
                inputBitmap.PixelHeight,
                inputBitmap.DpiX,
                inputBitmap.DpiY,
                PixelFormats.Bgr24,
                null);

            int threadsCount = (int)sliderThreads.Value;

            inputBitmap.Lock();
            outputBitmap.Lock();

            IntPtr inPtr = inputBitmap.BackBuffer;
            IntPtr outPtr = outputBitmap.BackBuffer;
            int width = inputBitmap.PixelWidth;
            int height = inputBitmap.PixelHeight;
            int stride = inputBitmap.BackBufferStride;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                int chunkHeight = height / threadsCount;

                Parallel.For(0, threadsCount, i =>
                {
                    int startY = i * chunkHeight;
                    int endY = (i == threadsCount - 1) ? height : (i + 1) * chunkHeight;
                    int currentHeight = endY - startY;

                    if (currentHeight > 2)
                    {
                        IntPtr currentInPtr = IntPtr.Add(inPtr, startY * stride);
                        IntPtr currentOutPtr = IntPtr.Add(outPtr, startY * stride);

                        if (useAsm)
                            ApplyScharrOperatorAsm(currentInPtr, currentOutPtr, width, currentHeight, stride);
                        else
                            ApplyScharrOperatorCpp(currentInPtr, currentOutPtr, width, currentHeight, stride);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Processing error: " + ex.Message);
            }

            watch.Stop();
            double timeSeconds = watch.Elapsed.TotalSeconds;

            outputBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            inputBitmap.Unlock();
            outputBitmap.Unlock();

            imgConverted.Source = outputBitmap;

            string lang = useAsm ? "ASM" : "C++";
            lblStatus.Text = $"Done. Mode: {lang} | Threads: {threadsCount} | Time: {timeSeconds:F4} s";
        }

        /*
         * Method: btnRun_Click
         * Desc: Handles the "Run Algorithm" button click. Checks the state of the radio buttons 
         *       to determine the selected library mode (C++ or ASM) and invokes the main 
         *       processing method.
         * Input params:
         * sender - The object that raised the event.
         * e - Event arguments.
         */
        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (rdBtnCpp.IsChecked == true) RunAlgorithm(false);
            else if (rdBtnAsm.IsChecked == true) RunAlgorithm(true);
        }

        private void rdBtnCpp_Click(object sender, RoutedEventArgs e) { CheckRunButton(); }
        private void rdBtnAsm_Click(object sender, RoutedEventArgs e) { CheckRunButton(); }

        /*
         * Method: CheckRunButton
         * Desc: Validates the user interface state to enable or disable the "Run Algorithm" button.
         *       Ensures that an image is loaded and a processing mode is selected allowing execution.
         */
        private void CheckRunButton()
        {
            btnRun.IsEnabled = (imgUploaded.Source != null) &&
                               (rdBtnCpp.IsChecked == true || rdBtnAsm.IsChecked == true);
        }
    }
}