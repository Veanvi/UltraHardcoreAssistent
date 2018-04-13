using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Tesseract;
using System.Configuration;
using System.Collections.Specialized;

namespace UltraHardcoreAssistent.Bot.Vision
{
    internal class Eye
    {
        private int logImgNum = 0;
        private string tessdataDir;
        private bool testRun;

        internal Eye()
        {
            tessdataDir = ConfigurationManager.AppSettings.Get("TessdataDir");
            testRun = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("TestRun"));
            this.TriggerCollor = new System.Windows.Media.Color()
            {
                R = 0,
                G = 0,
                B = 0
            };
            NumColorApprox = 1000;
        }

        internal enum TimerPosition
        {
            Top,
            Bottom,
            Left,
            Right,
            Empty
        }

        public int NumColorApprox { get; set; }
        public System.Windows.Media.Color TriggerCollor { get; private set; }

        #region GameTimer

        internal TimerPosition GetTimerPosition()
        {
            TimerPosition timerPosition = TimerPosition.Empty;

            var fullScreen = GetScreenImage();

            var bounds = new Rectangle(0, 0, fullScreen.Width, fullScreen.Height);
            var newCoordSys = new NewScreenCoordinateSystem(bounds);

            Bitmap bitmap = GetImgForTimerSerch(fullScreen);

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
        /// Получить изображение подготовленное для поиска игравого таймера (фон белый, таймер черный)
        /// </summary>
        /// <param name="bmp">Изображение без черных полос</param>
        /// <returns></returns>
        private Bitmap GetImgForTimerSerch(Bitmap bmp)
        {
            Mat mat = bmp.ToMat();

            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2HSV);
            var chanel = mat.Split();

            mat = chanel[0].Blur(new OpenCvSharp.Size(50, 50));
            mat = mat.Threshold(70, 255, ThresholdTypes.Binary);

            bmp = new Bitmap(mat.ToBitmap());

            //using (new Window("изображение", bmp.ToMat()))
            //{
            //    Cv2.WaitKey();
            //}

            if (testRun)
                LogImage(bmp);
            return bmp;
        }

        #endregion GameTimer

        #region TextBlock

        internal string GetText()
        {
            var bmp = GetScreenImage();
            bmp = GetImgText(bmp);
            return GetRecognizedText(bmp);
        }

        /// <summary>
        /// Получить граници блока с текстом
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private Rectangle GetBoundsTextBlock(Bitmap bmp, bool cropTextBorder = false)
        {
            int firstY = 0;
            int secondY = 0;
            int firstX = 0;
            int secontX = 0;

            int cropW = (int)(bmp.Width * 0.1);
            int cropH = (int)(bmp.Height * 0.1);
            var cropBounds = new Rectangle(0 + cropW, 0 + cropH,
                bmp.Width - cropW * 2, bmp.Height - cropH * 2);

            var cropBmp = bmp.Clone(cropBounds, bmp.PixelFormat);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.DrawImage(cropBmp, cropW, cropH);
            }

            Mat mat;
            mat = bmp.ToMat();
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2GRAY);
            mat = mat.Blur(new OpenCvSharp.Size(5, 5));

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
                if (color.ToArgb() != Color.White.ToArgb())
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
                if (color.ToArgb() != Color.White.ToArgb())
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
                if (color.ToArgb() != Color.White.ToArgb())
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
                if (color.ToArgb() != Color.White.ToArgb())
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
        /// Получить изображение блока с текстом подготовленного к распознанию
        /// </summary>
        /// <param name="bmp">Изображение без черных полос</param>
        /// <returns></returns>
        private Bitmap GetImgText(Bitmap bmp)
        {
            var imgBounds = GetBoundsTextBlock(bmp);
            Mat mat = bmp.ToMat();
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2GRAY);

            mat = mat.Threshold(75, 300, ThresholdTypes.Binary);
            bmp = mat.ToBitmap();

            bmp = bmp.Clone(imgBounds, bmp.PixelFormat);

            if (testRun)
                LogImage(bmp);
            return bmp;
        }

        /// <summary>
        /// Получить распознаный текст
        /// </summary>
        /// <param name="bmp">Изображение подготовленное к распознанию текста</param>
        /// <returns></returns>
        private string GetRecognizedText(Bitmap bmp)
        {
            string recognizeText;
            string result = "";
            string PatternCorrectSymbols = "QWERTYUIOPASDFGHJKLZXCVBNM";

            using (var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.Default))
            {
                var page = engine.Process(bmp);
                recognizeText = page.GetText();
            }

            foreach (var item in recognizeText.ToUpper())
            {
                if (PatternCorrectSymbols.Contains(item))
                {
                    result += item;
                }
            }

            return result;
        }

        #endregion TextBlock

        #region SupportMethod

        internal bool IsGameWindowActive()
        {
            bool result = false;
            var bmp = GetScreenImage();

            for (int y = 0; y < bmp.Height; y++)
            {
                var curCollor = bmp.GetPixel(bmp.Width / 2, y);
                if (curCollor.ToArgb() != Color.Black.ToArgb())
                {
                    result = true;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Поиск похожего цвета
        /// </summary>
        /// <param name="currentColor">Текущий цвет</param>
        /// <param name="desiretColor">Искомый цвет</param>
        /// <returns></returns>
        private bool ApproximateColorSearch(System.Windows.Media.Color currentColor, System.Windows.Media.Color desiretColor)
        {
            double fi = Math.Pow(currentColor.R - desiretColor.R, 2)
                + Math.Pow(currentColor.G - desiretColor.G, 2)
                + Math.Pow(currentColor.B - desiretColor.B, 2);

            bool result = fi <= NumColorApprox;
            return result;
        }

        /// <summary>
        /// Обрезка черных полос сверху и снизу изображения.
        /// </summary>
        /// <param name="bitmap">Орегинальный скриншот экрана</param>
        /// <returns></returns>
        private Bitmap CropLetterbox(Bitmap bitmap)
        {
            Coordinate topBlackLine = new Coordinate();
            Coordinate botBlackLine = new Coordinate();

            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            var newCoordSys = new NewScreenCoordinateSystem(bounds);
            var top = newCoordSys.GetAllTopCoordinate().Reverse();
            var bot = newCoordSys.GetAllBottomCoordinate().Reverse();

            foreach (var item in top)
            {
                Color color = bitmap.GetPixel(item.X, item.Y);
                if (color.ToArgb() != Color.Black.ToArgb())
                {
                    topBlackLine = new Coordinate(item.X, item.Y + 10);
                    break;
                }
            }

            foreach (var item in bot)
            {
                Color color = bitmap.GetPixel(item.X, item.Y);
                if (color.ToArgb() != Color.Black.ToArgb())
                {
                    botBlackLine = new Coordinate(item.X, item.Y - 10);
                    break;
                }
            }

            var imgBounds = new Rectangle(0, topBlackLine.Y,
                bitmap.Width, botBlackLine.Y - topBlackLine.Y);

            bitmap = bitmap.Clone(imgBounds, bitmap.PixelFormat);

            return bitmap;
        }

        /// <summary>
        /// Получить скриншот области экрана
        /// </summary>
        /// <param name="rect">Область скриншота</param>
        /// <returns></returns>
        private Bitmap GetScreenImage(Rectangle rect)
        {
            Bitmap bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
            }

            bmp = CropLetterbox(bmp);
            return bmp;
        }

        /// <summary>
        /// Полуить скриншот всего экрана
        /// </summary>
        /// <returns></returns>
        private Bitmap GetScreenImage()
        {
            return GetScreenImage(Screen.PrimaryScreen.Bounds);
            //return CropLetterbox(new Mat("testImg/test.jpg").ToBitmap());
        }

        private void LogImage(Bitmap bmp)
        {
            if (System.IO.Directory.Exists("л") == false)
                System.IO.Directory.CreateDirectory("outTestImg");
            bmp.ToMat().ImWrite("outTestImg/" + logImgNum.ToString() + ".tif");
            logImgNum++;
        }

        /// <summary>
        /// Поиск примерного цвета по списку координат
        /// </summary>
        /// <param name="bitmap">Изображение подготовленное к поиску таймера</param>
        /// <param name="list">Список координат</param>
        /// <returns></returns>
        private bool SearchInColorsList(Bitmap bitmap, IList<Coordinate> list)
        {
            int numСlippedCord = (int)((double)list.Count * (double)0.4);
            for (int i = 0; i < numСlippedCord; i++)
                list.RemoveAt(0);

            foreach (var cord in list)
            {
                var pixel = bitmap.GetPixel(cord.X, cord.Y);
                var mColor = new System.Windows.Media.Color()
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

        /// <summary>
        /// Структура хронящаа координаты экрана
        /// </summary>
        private struct Coordinate
        {
            internal Coordinate(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            internal int X { get; private set; }
            internal int Y { get; private set; }
        }

        /// <summary>
        /// Класс инкапсулирующий систему координат экрана с нулем в центре экрана
        /// </summary>
        private class NewScreenCoordinateSystem
        {
            internal NewScreenCoordinateSystem(Rectangle bounds)
            {
                Bounds = bounds;
            }

            private Rectangle Bounds { get; set; }
            private int X => Bounds.Width / 2;
            private int Y => Bounds.Height / 2;

            internal IList<Coordinate> GetAllBottomCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (int y = 1; y < Y; y++)
                    allTopCoordinate.Add(new Coordinate(X, Y + y));

                return allTopCoordinate;
            }

            internal IList<Coordinate> GetAllLeftCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (int x = 1; x < X; x++)
                    allTopCoordinate.Add(new Coordinate(X - x, Y));

                return allTopCoordinate;
            }

            internal IList<Coordinate> GetAllRightCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (int x = 1; x < X; x++)
                    allTopCoordinate.Add(new Coordinate(X + x, Y));

                return allTopCoordinate;
            }

            internal IList<Coordinate> GetAllTopCoordinate()
            {
                var allTopCoordinate = new List<Coordinate>();

                for (int y = 1; y < Y; y++)
                    allTopCoordinate.Add(new Coordinate(X, Y - y));

                return allTopCoordinate;
            }
        }
    }
}