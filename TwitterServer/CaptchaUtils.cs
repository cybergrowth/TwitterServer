using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace TwitterServer
{
    /// <summary>
    /// responsible for creating  captchas in the login or register process
    /// </summary>
    internal static class CaptchaUtils
    {
        static Random random = new Random();
        /// <summary>
        /// generating captcha text with a given length
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string GenerateCaptchaText(int num)
        {
            
            string res = "";
            for (int i = 0; i < num; i++)
            {
                int c = random.Next(48, 83);
                string add;
                if (c > 57)
                {
                    c += 39;
                    if (random.Next(0, 2) == 0) { c -= 32; }
                }
                add = Encoding.ASCII.GetString(new byte[] { (byte)c });
                
                res += add;
            }
            return res;
        }
        /// <summary>
        /// generating a captcha image from text and returning bytes
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public static byte[] GenerateCapchaImage(string word)
        {
            const int ImgW = 800;
            const int ImgH = 300;
            const int FontSize = 90;
            const int LetterGap = 10;
            int letters = word.Length;

            float[] offsetsY = new float[letters];
            float[] rotations = new float[letters];
            for (int i = 0; i < letters; i++)
            {
                offsetsY[i] = random.Next(-25, 26);
                rotations[i] = random.Next(-25, 26);
            }

            Color[] colors =
            {
            Color.FromArgb(255, 80,  80),
            Color.FromArgb(255, 180, 50),
            Color.FromArgb(80,  255, 120),
            Color.FromArgb(80,  180, 255),
            Color.FromArgb(220, 80,  255),
        };

            var bmp = new Bitmap(ImgW, ImgH, PixelFormat.Format32bppArgb);
            var canvas = Graphics.FromImage(bmp);

            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            canvas.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            canvas.Clear(Color.White);

            var font = new Font("Arial", FontSize, FontStyle.Bold, GraphicsUnit.Pixel);

            float totalWidth = 0;
            var sizes = new SizeF[word.Length];
            for (int i = 0; i < word.Length; i++)
            {
                sizes[i] = canvas.MeasureString(word[i].ToString(), font);
                totalWidth += sizes[i].Width;
                if (i < word.Length - 1) totalWidth += LetterGap;
            }

            float cx = (ImgW - totalWidth) / 2f;
            float cy = ImgH / 2f;

            for (int i = 0; i < word.Length; i++)
            {
                string letter = word[i].ToString();
                float lw = sizes[i].Width;
                float lh = sizes[i].Height;
                float angle = rotations[i % rotations.Length];
                float oy = offsetsY[i % offsetsY.Length];

                float pivotX = cx + lw / 2f;
                float pivotY = cy + oy;

                canvas.TranslateTransform(pivotX, pivotY);
                canvas.RotateTransform(angle);

                var shadowBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
                canvas.DrawString(letter, font, shadowBrush, -lw / 2f + 3, -lh / 2f + 3);

                var brush = new SolidBrush(colors[i % colors.Length]);
                canvas.DrawString(letter, font, brush, -lw / 2f, -lh / 2f);

                canvas.ResetTransform();

                cx += lw + LetterGap;
            }

            var pen = new Pen(colors[random.Next(colors.Length)], 5);

            for (int i = 0; i < 7; i++)
            {
                int x1 = random.Next(0, ImgW);
                int y1 = random.Next(0, ImgH);
                int x2 = random.Next(0, ImgW);
                int y2 = random.Next(0, ImgH);

                canvas.DrawLine(pen, x1, y1, x2, y2);
                pen.Color = colors[random.Next(colors.Length)];

            }
            byte[] res;
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Bmp);
                res = ms.ToArray();
            }

            return res;
        }


    }
}
