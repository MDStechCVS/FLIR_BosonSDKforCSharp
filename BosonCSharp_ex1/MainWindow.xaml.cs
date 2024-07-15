using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing; 
using System.Drawing.Imaging; 
using System.Runtime.InteropServices; 
using System.Diagnostics; 

namespace BosonCSharp_ex1
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private VideoCapture cam;
        private Mat frame;
        private DispatcherTimer timer;
        private bool is_initCam, is_initTimer;

        // 카메라 해상도 
        private int CameraHeight = 512;
        private int CameraWidth = 640; 

        // ROI 영역 표시 
        private readonly int startX = 200;
        private readonly int startY = 200;
        private readonly int endX = 350;
        private readonly int endY = 350;

        private int step = 64;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try {
                is_initCam = InitCamera();
                is_initTimer = InitTimer(10);

                if (is_initTimer && is_initCam)
                {
                    timer.Start();
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Window_Loaded : " + ex); 
            }
          
        }

        private bool InitTimer(double intervalMs)
        {
            try
            {
                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(intervalMs)
                };
                timer.Tick += Timer_Tick;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Timer 초기화 오류: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 카메라 설정 
        /// </summary>
        /// <returns></returns>
        private bool InitCamera()
        {
            try
            {
                // 연결할 카메라 선택 
                cam = new VideoCapture(1);

                // 카메라 해상도 값 설정 
                cam.Set(CaptureProperty.FrameHeight, CameraHeight);
                cam.Set(CaptureProperty.FrameWidth, CameraWidth);

                // 비디오 코덱을 16비트 그레이스케일 이미지로 지정 
                cam.Set(CaptureProperty.FourCC, VideoWriter.FourCC('Y', '1', '6', ' '));

                // RGB 변환 비활성화
                cam.Set(CaptureProperty.ConvertRgb, 0); 

                frame = new Mat();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"카메라 초기화 오류: {ex.Message}");
                return false;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                //연결된 카메라로부터 프레임을 읽어옴 
                cam.Read(frame);

                //프레임을 정상적으로 받아온 경우 코드 수행 
                if (!frame.Empty())
                {
                    Mat gray16 = frame.Clone();
                    var (maxTempStr, minTempStr, avgTempStr, minTemp, maxTemp, tempArray) = PixelToTemp(gray16);

                    // 레인보우 팔레트 적용 
                    Mat colorMat = ApplyRainbowPalette(gray16, tempArray, minTemp, maxTemp);

                    // Mat를 Bitmap으로 변환 
                    Bitmap bitmap = BitmapConverter.ToBitmap(colorMat);

                    // Draw rectangle and temperature data on the Bitmap
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3))
                        {
                            graphics.DrawRectangle(pen, new System.Drawing.Rectangle(startX, startY, endX - startX, endY - startY));
                        }

                        using (System.Drawing.Font font = new System.Drawing.Font("Arial", 12))
                        using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.Green))
                        {
                            graphics.DrawString(maxTempStr, font, brush, new System.Drawing.PointF(10, 15));
                            graphics.DrawString(minTempStr, font, brush, new System.Drawing.PointF(10, 35));
                            graphics.DrawString(avgTempStr, font, brush, new System.Drawing.PointF(10, 55));
                        }
                    }
                    Cam_1.Source = BitmapSourceConvert.ToBitmapSource(bitmap);
                }
                else
                {
                    Debug.WriteLine("카메라에서 프레임을 읽을 수 없습니다.");
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Timer_Tick : {ex.Message}");
            }
            
        }

        private unsafe (string maxTempStr, string minTempStr, string avgTempStr, double minTemp, double maxTemp, double[] tempArray) PixelToTemp(Mat image)
        {
         
            int width = image.Width;
            int height = image.Height;

            ushort[] rawArray = new ushort[width * height];
            fixed (ushort* rawArrayPtr = rawArray)
            {
                Buffer.MemoryCopy(image.Data.ToPointer(), rawArrayPtr, rawArray.Length * sizeof(ushort), rawArray.Length * sizeof(ushort));
            }

            double[] tempArray = new double[width * height];
            
            // raw 값을 보정하여 섭씨 온도 값 저장 
            for (int i = 0; i < rawArray.Length; i++)
            {
                tempArray[i] = (rawArray[i] / 100.0) - 273.15;
            }

            // 온도 값 중 최대, 최소, 평균 온도 값 
            double maxTemp = tempArray.Max();
            double minTemp = tempArray.Min();
            double avgTemp = tempArray.Average();

            // 전시할 온도 값 
            string maxTempStr = $"max_temp = {Math.Round(maxTemp, 2)}";
            string minTempStr = $"min_temp = {Math.Round(minTemp, 2)}";
            string avgTempStr = $"avg_temp = {Math.Round(avgTemp, 2)}";

            return (maxTempStr, minTempStr, avgTempStr, minTemp, maxTemp, tempArray);
        }

        /// <summary>
        /// Rainbow 팔레트 적용 
        /// </summary>
        /// <param name="gray16"></param>
        /// <param name="tempArray"></param>
        /// <param name="minTemp"></param>
        /// <param name="maxTemp"></param>
        /// <returns></returns>
        private Mat ApplyRainbowPalette(Mat gray16, double[] tempArray, double minTemp, double maxTemp)
        {
           
            int width = gray16.Width;
            int height = gray16.Height;

            double tempDiff = maxTemp - minTemp;
            if (tempDiff == 0) tempDiff = 1;

            byte[] colorData = new byte[width * height * 3]; // 3 channels (BGR)
            int colorIndex = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    double tempValue = tempArray[index];
                    double normalizedValue = (tempValue - minTemp) / tempDiff;
                    int rVal = (int)(normalizedValue * 255);

                    byte blue = 0, green = 0, red = 0;
                    if (rVal < step) // Blue to Cyan
                    {
                        blue = 255;
                        green = (byte)(rVal * 4);
                    }
                    else if (rVal < step * 2) // Cyan to Green
                    {
                        blue = (byte)(255 - (rVal - step) * 4);
                        green = 255;
                    }
                    else if (rVal < step * 3) // Green to Yellow
                    {
                        green = 255;
                        red = (byte)((rVal - step * 2) * 4);
                    }
                    else // Yellow to Red
                    {
                        green = (byte)(255 - (rVal - step * 3) * 4);
                        red = 255;
                    }

                    colorData[colorIndex++] = blue;
                    colorData[colorIndex++] = green;
                    colorData[colorIndex++] = red;
                }
            }

            Mat colorMat = new Mat(height, width, MatType.CV_8UC3);
            Marshal.Copy(colorData, 0, colorMat.Data, colorData.Length);

            return colorMat;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                timer.Stop();
                cam.Dispose();
                frame.Dispose();
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Window_Closing : {ex.Message}");
            }
        }
    }

    public static class BitmapSourceConvert
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
    }
}
