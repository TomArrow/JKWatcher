using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace JKWatcher.RandomHelpers
{
    public static class NumberImages
    {
        const int width = 64;
        const int height = 64;
        static ImageSource[] imageSources = new ImageSource[64+1];
        static NumberImages()
        {
            for(int i = 0; i < imageSources.Length; i++)
            {
                Bitmap numberImage = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                numberImage.MakeTransparent();
                Graphics g = Graphics.FromImage(numberImage);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                int fontSize = height + 10;
                Font theFont = new Font("Segoe UI", fontSize);
                bool fontFits = false;
                float topOffset = 0;
                float leftOffset = 0;
                while (!fontFits)
                {
                    SizeF size = g.MeasureString(i.ToString(), theFont);
                    fontFits = size.Width < width && size.Height < height;
                    if (fontFits)
                    {
                        topOffset = ((float)height - size.Height) / 2f;
                        leftOffset = ((float)width - size.Width) / 2f;
                    }
                    else
                    {
                        fontSize--;
                        theFont.Dispose();
                        theFont = new Font("Segoe UI", fontSize);
                    }
                }
                g.DrawString(i.ToString(), theFont, System.Drawing.Brushes.Black,new RectangleF(0f+leftOffset,0f+ topOffset, width,height));
                g.DrawString(i.ToString(), theFont, System.Drawing.Brushes.White,new RectangleF(1f + leftOffset, 1f + topOffset, width,height));
                g.Flush();
                g.Dispose();
                imageSources[i] = Helpers.BitmapToImageSource(numberImage);
                imageSources[i].Freeze();
                //if(i == 50)
                //{
                //    numberImage.Save("50test.png");
                //
#if DEBUG
                //Directory.CreateDirectory("numberImgsDebug");
                //numberImage.Save($"numberImgsDebug/{i}.png");
#endif
                numberImage.Dispose();
                theFont.Dispose();
            }
        }
        public static ImageSource getImageSource(int number)
        {
            if(number >= imageSources.Length)
            {
                return null;
            }
            return imageSources[number];
        }

        public static void Init()
        {
            // just force to call the static constructor
        }
    }
}
