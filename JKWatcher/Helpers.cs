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
        // Takes array of strings and turns them into chunks of maxSize with a chosen separator.
        // If any input chunk is too big, it will be split, first using empty spaces and commas,
        // then just hard cutoffs if any individual word is still over maxSize
        public static string[] StringChunksOfMaxSize(string[] input,int maxSize,string separator=", ",string chunkStart = "")
        {
            if(chunkStart.Length > maxSize)
            {
                throw new Exception("wtf man, chunkstart cant be bigger than maxsize");
            }
            int chunkStartLength = chunkStart.Length;
            int separatorLength = separator.Length;
            int maxActualSize = maxSize - chunkStartLength;
            List<string> noChunksTooBig = new List<string>();
            List<string> output = new List<string>();

            foreach (string inputString in input)
            {
                if (inputString == null) continue;
                if (inputString.Length > maxActualSize)
                {

                    string[] inputStringParts = inputString.Split(new char[] { ' ', ',' });
                    foreach(string inputStringPart in inputStringParts)
                    {
                        if(inputStringPart.Length > maxActualSize)
                        {
                            string[] inputStringPartsLimited = ChunksUpto(inputStringPart, maxActualSize).ToArray();
                            noChunksTooBig.AddRange(inputStringPartsLimited);
                        } else
                        {
                            noChunksTooBig.Add(inputString);
                        }
                    }
                }
                else
                {
                    noChunksTooBig.Add(inputString);
                }
            }

            string tmp = chunkStart;
            int stringsAdded = 0;
            foreach(string inputString in noChunksTooBig)
            {
                if(stringsAdded == 0)
                {
                    tmp += inputString; // Will only happen if chunkStart is an empty string
                    stringsAdded++;
                    continue;
                }
                int newLengthWouldBe = tmp.Length + separatorLength + inputString.Length;
                if (newLengthWouldBe < maxSize) // Still leaves some room
                {
                    tmp += separator + inputString;
                    stringsAdded++;
                }
                else if (newLengthWouldBe == maxSize) // exactly hits the limit
                {
                    tmp += separator + inputString;
                    output.Add(tmp);
                    tmp = chunkStart; 
                    stringsAdded = 0;
                } else
                {
                    // Too big to fit in. Turn into new string
                    output.Add(tmp);
                    tmp = chunkStart + inputString;
                    stringsAdded = 1;
                }
            }
            if(stringsAdded > 0)
            {
                output.Add(tmp);
                tmp = "";
            }

            return output.ToArray();
        }

        // following function from: https://stackoverflow.com/a/1450889
        static IEnumerable<string> ChunksUpto(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }

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
