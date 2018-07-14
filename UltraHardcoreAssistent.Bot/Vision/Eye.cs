using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tesseract;
using Color = System.Windows.Media.Color;
using Size = OpenCvSharp.Size;

namespace UltraHardcoreAssistent.Bot.Vision
{
    internal class Eye
    {
        private readonly string tessdataDir;

#if DEBUG
        private readonly string testImgPath = "testImg/testWind.tif";
#endif
        private readonly bool testRun;
        private int logImgNum;

        internal Eye()
        {
            tessdataDir = ConfigurationManager.AppSettings.Get("TessdataDir");
            testRun = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("TestRun"));
            TriggerCollor = new Color
            {
                R = 0,
                G = 0,
                B = 0
            };
            NumColorApprox = 1000;
        }

        public int NumColorApprox { get; set; }
        public Color TriggerCollor { get; }

        internal enum TimerPosition
        {
            Top,
            Bottom,
            Left,
            Right,
            Empty
        }

        /// <summary>
        ///     Структура хронящаа координаты экрана
        /// </summary>
        private struct Coordinate
        {
            internal Coordinate(int x, int y)
            {
                X = x;
                Y = y;
            }

            internal int X { get; }
            internal int Y { get; }
        }

        /// <summary>
        ///     Класс инкапсулирующий систему координат экрана с нулем в центре экрана
        /// </summary>
        private class NewScreenCoordinateSystem
        {
            internal NewScreenCoordinateSystem(Rectangle bounds)
            {
                Bounds = bounds;
            }

            private Rectangle Bounds { get; }
            private int X => Bounds.Width / 2;
            private int Y => Bounds.Height / 2;

            internal IList<Coordinate> GetAllBottomCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (var y = 1; y < Y; y++)
                    allTopCoordinate.Add(new Coordinate(X, Y + y));

                return allTopCoordinate;
            }

            internal IList<Coordinate> GetAllLeftCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (var x = 1; x < X; x++)
                    allTopCoordinate.Add(new Coordinate(X - x, Y));

                return allTopCoordinate;
            }

            internal IList<Coordinate> GetAllRightCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (var x = 1; x < X; x++)
                    allTopCoordinate.Add(new Coordinate(X + x, Y));

                return allTopCoordinate;
            }

            internal IList<Coordinate> GetAllTopCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (var y = 1; y < Y; y++)
                    allTopCoordinate.Add(new Coordinate(X, Y - y));

                return allTopCoordinate;
            }
        }

        #region GameTimer

        internal TimerPosition GetTimerPosition()
        {
            var timerPosition = TimerPosition.Empty;
            var gameScreen = GetGameScreenshot();

            var units = GraphicsUnit.Point;
            var bmpRectangleF = gameScreen.GetBounds(ref units);
            var bmpRectangle = Rectangle.Round(bmpRectangleF);

            var newCoordSys = new NewScreenCoordinateSystem(bmpRectangle);

            var bitmap = GetImgForTimerSerch(gameScreen);

            if (SearchInColorsList(bitmap, newCoordSys.GetAllTopCoordinate()))
                timerPosition = TimerPosition.Top;
            if (SearchInColorsList(bitmap, newCoordSys.GetAllRightCoordinate()))
                timerPosition = TimerPosition.Right;
            if (SearchInColorsList(bitmap, newCoordSys.GetAllBottomCoordinate()))
                timerPosition = TimerPosition.Bottom;
            if (SearchInColorsList(bitmap, newCoordSys.GetAllLeftCoordinate()))
                timerPosition = TimerPosition.Left;

            bitmap = null;
            GC.Collect();

            return timerPosition;
        }

        /// <summary>
        ///     Получить изображение подготовленное для поиска игравого таймера (фон белый, таймер черный)
        /// </summary>
        /// <param name="bmp">Изображение без черных полос</param>
        /// <returns></returns>
        private Bitmap GetImgForTimerSerch(Bitmap bmp)
        {
            var mat = bmp.ToMat();

            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2HSV);
            var chanel = mat.Split();

            mat = chanel[0].Blur(new Size(50, 50));
            mat = mat.Threshold(70, 255, ThresholdTypes.Binary);

            bmp = new Bitmap(mat.ToBitmap());

            if (testRun)
                LogImage(bmp);
            return bmp;
        }

        #endregion GameTimer

        #region TextBlock

        internal string GetText()
        {
            var bmp = GetGameScreenshot();

            bmp = GetImgText(bmp);
            return GetRecognizedText(bmp);
        }

        /// <summary>
        ///     Получить граници блока с текстом
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private Rectangle GetBoundsTextBlock(Bitmap bmp, bool cropTextBorder = false)
        {
            var firstY = 0;
            var secondY = 0;
            var firstX = 0;
            var secontX = 0;

            var cropW = (int)(bmp.Width * 0.1);
            var cropH = (int)(bmp.Height * 0.1);
            var cropBounds = new Rectangle(0 + cropW, 0 + cropH,
                bmp.Width - cropW * 2, bmp.Height - cropH * 2);

            var cropBmp = bmp.Clone(cropBounds, bmp.PixelFormat);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.White);
                g.CompositingMode = CompositingMode.SourceOver;
                g.DrawImage(cropBmp, cropW, cropH);
            }

            Mat mat;
            mat = bmp.ToMat();
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2GRAY);
            mat = mat.Blur(new Size(5, 5));

            mat = mat.Threshold(75, 255, ThresholdTypes.Binary);

            bmp = mat.ToBitmap();

            var bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var newCoordSys = new NewScreenCoordinateSystem(bounds);

            var topCord = newCoordSys.GetAllTopCoordinate().Reverse();
            var botCord = newCoordSys.GetAllBottomCoordinate().Reverse();
            var leftCord = newCoordSys.GetAllLeftCoordinate().Reverse();
            var rightCord = newCoordSys.GetAllRightCoordinate().Reverse();

            foreach (var item in topCord)
            {
                var color = bmp.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.White.ToArgb())
                {
                    if (cropTextBorder == false)
                        firstY = item.Y + 10;
                    else
                        firstY = item.Y;
                    break;
                }
            }

            foreach (var item in botCord)
            {
                var color = bmp.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.White.ToArgb())
                {
                    if (cropTextBorder == false)
                        secondY = item.Y - 10;
                    else
                        secondY = item.Y;
                    break;
                }
            }

            foreach (var item in leftCord)
            {
                var color = bmp.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.White.ToArgb())
                {
                    if (cropTextBorder == false)
                        firstX = item.X + 10;
                    else
                        firstX = item.X;
                    break;
                }
            }

            foreach (var item in rightCord)
            {
                var color = bmp.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.White.ToArgb())
                {
                    if (cropTextBorder == false)
                        secontX = item.X - 10;
                    else
                        secontX = item.X;
                    break;
                }
            }

            var imgBounds = new Rectangle(firstX, firstY, secontX - firstX, secondY - firstY);
            return imgBounds;
        }

        /// <summary>
        ///     Получить изображение блока с текстом подготовленного к распознанию
        /// </summary>
        /// <param name="bmp">Изображение без черных полос</param>
        /// <returns></returns>
        private Bitmap GetImgText(Bitmap bmp)
        {
            var imgBounds = GetBoundsTextBlock(bmp);
            var mat = bmp.ToMat();
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2GRAY);

            mat = mat.Blur(new Size(5, 5));
            mat = mat.Threshold(70, 255, ThresholdTypes.Binary);
            bmp = mat.ToBitmap();

            bmp = bmp.Clone(imgBounds, bmp.PixelFormat);

            if (testRun)
                LogImage(bmp);
            return bmp;
        }

        /// <summary>
        ///     Получить распознаный текст
        /// </summary>
        /// <param name="bmp">Изображение подготовленное к распознанию текста</param>
        /// <returns></returns>
        private string GetRecognizedText(Bitmap bmp)
        {
            string recognizeText;
            var result = "";
            var PatternCorrectSymbols = "QWERTYUIOPASDFGHJKLZXCVBNM";

            using (var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.Default))
            {
                var page = engine.Process(bmp);
                recognizeText = page.GetText();
            }

            foreach (var item in recognizeText.ToUpper())
                if (PatternCorrectSymbols.Contains(item))
                    result += item;

            return result;
        }

        #endregion TextBlock

        #region SupportMethod

        private Rectangle GetWindowRectangle()
        {
#if DEBUG
            var bitmap = new Mat(testImgPath).ToBitmap();

            var units = GraphicsUnit.Point;
            var bmpRectangleF = bitmap.GetBounds(ref units);
            var bmpRectangle = Rectangle.Round(bmpRectangleF);

            return bmpRectangle;
#endif
#if !DEBUG
            // Старый способ, до Aero Theme
            //GetWindowRect(GetGameProcess().MainWindowHandle, out var rect);

            // Новый способ, с Aero Theme
            var res = DwmGetWindowAttribute(GetGameProcess().MainWindowHandle,
                9,
                out var rect,
                Marshal.SizeOf(typeof(Rect)));

            return rect.ToRectangle();
#endif
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        internal bool IsGameWindowActive()
        {
#if DEBUG
            return true;
#endif
#if !DEBUG
            var gameProcess = GetGameProcess();

            var fwHwnd = GetForegroundWindow();
            var pid = 0;
            GetWindowThreadProcessId(fwHwnd, ref pid);
            var foregroundWindow = Process.GetProcessById(pid);
            var isGameActive = gameProcess != null
                               && foregroundWindow.Id == gameProcess.Id;
            return isGameActive;
#endif
        }

        private Process GetGameProcess()
        {
            return Process.GetProcessesByName("ultra_hardcore").FirstOrDefault();
        }

        [DllImport(@"dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute,
            int cbAttribute);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public readonly int Left;
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;

            public Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(Left, Top, Right, Bottom);
            }
        }

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, ref int pid);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>
        ///     Поиск похожего цвета
        /// </summary>
        /// <param name="currentColor">Текущий цвет</param>
        /// <param name="desiretColor">Искомый цвет</param>
        /// <returns></returns>
        private bool ApproximateColorSearch(Color currentColor, Color desiretColor)
        {
            var fi = Math.Pow(currentColor.R - desiretColor.R, 2)
                     + Math.Pow(currentColor.G - desiretColor.G, 2)
                     + Math.Pow(currentColor.B - desiretColor.B, 2);

            var result = fi <= NumColorApprox;
            return result;
        }

        /// <summary>
        ///     Обрезка черных полос сверху и снизу изображения.
        /// </summary>
        /// <param name="bitmap">Орегинальный скриншот экрана</param>
        /// <returns></returns>
        private Bitmap CropLetterbox(Bitmap bitmap)
        {
            var topBlackLine = new Coordinate();
            var botBlackLine = new Coordinate();
            var leftBlackLine = new Coordinate();

            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            var newCoordSys = new NewScreenCoordinateSystem(bounds);

            // Подрезка боковых рамок окна и заголовка окна
            var borderSize = SystemInformation.BorderSize;
            int titleHeight = (int)(SystemInformation.CaptionHeight * 1.5);

            var workAreaRec = new Rectangle(borderSize.Width, titleHeight,
                bitmap.Width - borderSize.Width * 2, bitmap.Height - titleHeight);
            bitmap = bitmap.Clone(workAreaRec, bitmap.PixelFormat);

            bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            newCoordSys = new NewScreenCoordinateSystem(bounds);
            var top = newCoordSys.GetAllTopCoordinate().Reverse();
            var bot = newCoordSys.GetAllBottomCoordinate().Reverse();
            var left = newCoordSys.GetAllLeftCoordinate().Reverse();
            var right = newCoordSys.GetAllRightCoordinate().Reverse();

            foreach (var item in top)
            {
                var color = bitmap.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.Black.ToArgb())
                {
                    topBlackLine = new Coordinate(item.X, item.Y);
                    break;
                }
            }

            foreach (var item in bot)
            {
                var color = bitmap.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.Black.ToArgb())
                {
                    botBlackLine = new Coordinate(item.X, item.Y);
                    break;
                }
            }

            foreach (var item in left)
            {
                var color = bitmap.GetPixel(item.X, item.Y);
                if (color.ToArgb() != System.Drawing.Color.Black.ToArgb())
                {
                    leftBlackLine = new Coordinate(item.X, item.Y);
                    break;
                }
            }

            var imgBounds = new Rectangle(leftBlackLine.X, topBlackLine.Y,
                bitmap.Width - leftBlackLine.X * 2, botBlackLine.Y - topBlackLine.Y);

            bitmap = bitmap.Clone(imgBounds, bitmap.PixelFormat);

            return bitmap;
        }

        /// <summary>
        ///     Получить скриншот области экрана
        /// </summary>
        /// <param name="rect">Область скриншота</param>
        /// <returns></returns>
        private Bitmap GetScreenshotWindow(Rectangle rect)
        {
            var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bmp))
            {
                graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            }

            return bmp;
        }

        /// <summary>
        ///     Получить скриншот всего экрана
        /// </summary>
        /// <returns></returns>
        private Bitmap GetScreenshotFullScreen()
        {
            return GetScreenshotWindow(Screen.PrimaryScreen.Bounds);
        }

        private Bitmap GetGameScreenshot()
        {
#if DEBUG
            return CropLetterbox(new Mat(testImgPath).ToBitmap());
#endif
#if !DEBUG
            var gameProcWin = GetWindowRectangle();
            Bitmap gameScreen = null;
            var i = gameProcWin.Width + gameProcWin.Height;

            if (gameProcWin.Width + gameProcWin.Height == 0)
                gameScreen = GetScreenshotFullScreen();
            else
                gameScreen = GetScreenshotWindow(gameProcWin);

            gameScreen = CropLetterbox(gameScreen);

            if (testRun)
                LogImage(gameScreen);

            return gameScreen;
#endif
        }

        private void LogImage(Bitmap bmp)
        {
            if (Directory.Exists("л") == false)
                Directory.CreateDirectory("outTestImg");
            bmp.ToMat().ImWrite("outTestImg/" + logImgNum + ".tif");
            logImgNum++;
        }

        /// <summary>
        ///     Поиск примерного цвета по списку координат
        /// </summary>
        /// <param name="bitmap">Изображение подготовленное к поиску таймера</param>
        /// <param name="list">Список координат</param>
        /// <returns></returns>
        private bool SearchInColorsList(Bitmap bitmap, IList<Coordinate> list)
        {
            var numСlippedCord = (int)(list.Count * 0.4);
            for (var i = 0; i < numСlippedCord; i++)
                list.RemoveAt(0);

            foreach (var cord in list)
            {
                var pixel = bitmap.GetPixel(cord.X, cord.Y);
                var mColor = new Color
                {
                    A = 255,
                    R = pixel.R,
                    G = pixel.G,
                    B = pixel.B
                };

                if (ApproximateColorSearch(mColor, TriggerCollor))
                    return true;
            }

            return false;
        }

        #endregion SupportMethod
    }
}