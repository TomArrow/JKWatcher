using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace JKWatcher
{
    static class Helpers
    {
        public static string GetUnusedDemoFilename(string baseFilename, JKClient.ProtocolVersion protocolVersion)
        {
            string extension = ".dm_" + ((int)protocolVersion).ToString();
            if (!File.Exists("demos/"+baseFilename+ extension))
            {
                return baseFilename;
            }
            //string extension = Path.GetExtension(baseFilename);

            int index = 1;
            while (File.Exists("demos/" + baseFilename+ "("+ (++index).ToString()+")" + extension)) ;

            return baseFilename + "(" + (++index).ToString() + ")";
        }

        static public BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        public static ByteImage BitmapToByteArray(Bitmap bmp)
        {

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int stride = Math.Abs(bmpData.Stride);
            int bytes = stride * bmp.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            bmp.UnlockBits(bmpData);

            return new ByteImage(rgbValues, stride, bmp.Width, bmp.Height, bmp.PixelFormat);
        }

        public static Bitmap ByteArrayToBitmap(ByteImage byteImage)
        {
            Bitmap myBitmap = new Bitmap(byteImage.width, byteImage.height, byteImage.pixelFormat);
            Rectangle rect = new Rectangle(0, 0, myBitmap.Width, myBitmap.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                myBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                myBitmap.PixelFormat);

            bmpData.Stride = byteImage.stride;

            IntPtr ptr = bmpData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(byteImage.imageData, 0, ptr, byteImage.imageData.Length);

            myBitmap.UnlockBits(bmpData);
            return myBitmap;

        }
    }
}
