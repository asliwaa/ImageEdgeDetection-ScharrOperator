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

namespace EdgeDetectionApp
{
    /// <summary>
    /// Main window class handling UI logic and DLL calls.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Import function from C++ library
        [DllImport("EdgeDetectionCPP.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ApplyScharrOperatorCpp")]
        public static extern void ApplyScharrOperatorCpp(IntPtr inputPtr, IntPtr outputPtr, int width, int height, int stride);

        // Import function from ASM library
        [DllImport("EdgeDetectionASM.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ApplyScharrOperatorAsm")]
        public static extern void ApplyScharrOperatorAsm(IntPtr inputPtr, IntPtr outputPtr, int width, int height, int stride);

        public MainWindow()
        {
            InitializeComponent();
            // Run button and algorihm choice buttons are disaled by defalut (no image loaded yet)
            btnRun.IsEnabled = false;
            rdBtnCpp.IsEnabled = false;
            rdBtnAsm.IsEnabled = false;
        }

        /*
         * Method: btnImageUpload_Click
         * Desc: Event handler for "Upload Image" button click. Opens a file dialog and displays the image.
         * 
         * Input params:
         *   sender - object that runs the event.
         *   e - event arguments.
         */
        private void btnImageUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Image files | *.png;*.jpg;*.bmp";

            if (fileDialog.ShowDialog() == true)
            {
                //Load an image for display
                BitmapImage tempBitmap = new BitmapImage(new Uri(fileDialog.FileName));
                imgUploaded.Source = tempBitmap;

                //Clear previous result
                imgConverted.Source = null;

                //Enable UI controls
                rdBtnCpp.IsEnabled = true;
                rdBtnAsm.IsEnabled = true;
                CheckRunButton();
            }
        }

        /*
         * Method: RunAlgorithm
         * Desc: Prepares memory and executes the selected algorithm (C++ or ASM). Uses the "Caller Allocates" pattern - buffers are allocated in C#.
         * 
         * Input params:
         *   useAsm - algorithm selection flag: true = ASM, false = C++.
         */
        private void RunAlgorithm(bool useAsm)
        {
            if (imgUploaded.Source is not BitmapImage originalImage) return;

            //Converting image format to Bgr24 (3 bytes per pixel),
            //because C++/ASM algorithms operate on raw RGB bytes.
            FormatConvertedBitmap converter = new FormatConvertedBitmap();
            converter.BeginInit();
            converter.Source = originalImage;
            converter.DestinationFormat = PixelFormats.Bgr24;
            converter.EndInit();

            //Create Input Bitmap with pointer access
            WriteableBitmap inputBitmap = new WriteableBitmap(converter);

            //Create empty Output Bitmap
            WriteableBitmap outputBitmap = new WriteableBitmap(
                inputBitmap.PixelWidth,
                inputBitmap.PixelHeight,
                inputBitmap.DpiX,
                inputBitmap.DpiY,
                PixelFormats.Bgr24,
                null);

            //Memory lock - prevents Garbage Collector from moving buffers in memory during DLL execution.
            inputBitmap.Lock();
            outputBitmap.Lock();

            //Retrieve pointers and parameters
            IntPtr inPtr = inputBitmap.BackBuffer;
            IntPtr outPtr = outputBitmap.BackBuffer;
            int w = inputBitmap.PixelWidth;
            int h = inputBitmap.PixelHeight;
            int stride = inputBitmap.BackBufferStride; //Stride includes padding

            //Measure time and Execute
            var watch = System.Diagnostics.Stopwatch.StartNew();

            //Finally try running the algorithm
            try
            {
                if (useAsm)
                    ApplyScharrOperatorAsm(inPtr, outPtr, w, h, stride);
                else
                    ApplyScharrOperatorCpp(inPtr, outPtr, w, h, stride);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Library execution error: " + ex.Message);
            }

            watch.Stop();

            //Unlock and refresh bitmap
            outputBitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
            inputBitmap.Unlock();
            outputBitmap.Unlock();

            //Display result
            imgConverted.Source = outputBitmap;

            string lang = useAsm ? "ASM" : "C++";
            MessageBox.Show($"Execution time {lang}: {watch.ElapsedMilliseconds} ms");
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (rdBtnCpp.IsChecked == true) RunAlgorithm(false);
            else if (rdBtnAsm.IsChecked == true) RunAlgorithm(true);
        }

        private void rdBtnCpp_Click(object sender, RoutedEventArgs e) { CheckRunButton(); }
        private void rdBtnAsm_Click(object sender, RoutedEventArgs e) { CheckRunButton(); }

        private void CheckRunButton()
        {
            btnRun.IsEnabled = (imgUploaded.Source != null) &&
                               (rdBtnCpp.IsChecked == true || rdBtnAsm.IsChecked == true);
        }
    }
}