
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace nanonis_sxm_thumbnail_ext
{
    class SPM
    {
        Stream stream;
        public int x, y;
        public bool chFreq = false;
        public Bitmap img;
        public List<DataInfoItem> channels = new List<DataInfoItem>(4);

        public SPM(Stream stream)
        {
            this.stream = stream;
            BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true);
            string line = "";
            bool isLE = false;
            while (line != ":SCANIT_END:")
            {
                line = ReadLine(reader).Trim();
                // float BigEnd?
                if (line.StartsWith(":SCANIT_TYPE:"))
                {
                    isLE = ReadLine(reader).Trim().Contains("MSBFIRST");
                }
                // scan size?
                if (line.StartsWith(":SCAN_PIXELS:"))
                {
                    var str = ReadLine(reader).Trim();
                    Console.WriteLine(str);
                    string[] xy = str.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    x = int.Parse(xy[0]);
                    y = int.Parse(xy[1]);
                }
                // is afm?
                if (line.StartsWith(":Z-Controller>Controller status:"))
                {
                    chFreq = ReadLine(reader).Contains("OFF");
                }
                // list channels?
                if (line.StartsWith(":DATA_INFO:"))
                {
                    ReadLine(reader).Trim();
                    while (true)
                    {
                        line = ReadLine(reader).Trim();
                        if (line.StartsWith(":") || line.Length == 0)
                        {
                            break;
                        }
                        string[] items = line.Split('\t');
                        channels.Add(new DataInfoItem
                        {
                            Name = items[1],
                            Direction = items[3]
                        });
                    }
                }
            }
            while (reader.Read() != 0x1a) ;
            System.Diagnostics.Debug.Assert(reader.Read() == 0x04);
            string chName = "Z";
            int chID = 0;
            if (chFreq)
            {
                chName = "Frequency_Shift";
            }
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].Name == chName)
                    break;
                else
                {
                    if (channels[i].Direction == "both")
                        chID += 2;
                    else
                        chID++;
                }
            }
            Console.WriteLine(chName);
            int size = x * y;
            reader.BaseStream.Seek(chID * size * 4, SeekOrigin.Current);
            float[] rawPixel = new float[size];
            for (int i = 0; i < size; i++)
            {   
                byte[] next = reader.ReadBytes(4);
                if (isLE) next = next.Reverse().ToArray();
                float num = BitConverter.ToSingle(next, 0);
                if (float.IsNaN(num))
                {
                    rawPixel[i] = rawPixel[0];
                }
                else
                {
                    rawPixel[i] = num;
                }
            }
            reader.Close();
            Console.WriteLine(rawPixel[20000]);
            int[] pixalData = NormalizeData(rawPixel);
            CreateThumbnail(pixalData, x, y);
        }

       // create method ReadLine for a BinaryReader
        private string ReadLine(BinaryReader reader)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                char c = reader.ReadChar();
                sb.Append(c);
                if (c == '\n')
                    break;
            }
            return sb.ToString();
        }

        void CreateThumbnail(int[] pixalData, int x, int y)
        {
            var thumbnailSize = new Size(x, y);
            img = new Bitmap(thumbnailSize.Width, thumbnailSize.Height,
                            PixelFormat.Format32bppArgb);
            Rectangle rect = new Rectangle(0, 0, x, y);
            BitmapData bmpdata = img.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            int b = 0, g = 1, r = 2;  // BGR
            unsafe
            {
                byte* ptr = (byte*)bmpdata.Scan0;

                for (int row = y - 1; row >= 0; --row)
                {
                    for (int col = 0; col < x; ++col)
                    {
                        byte pix = (byte)pixalData[row * x + col];
                        ptr[b] = pix;
                        ptr[g] = pix;
                        ptr[r] = pix;
                        ptr += 3;
                    }
                    // Handling byte alignment issues
                    ptr += bmpdata.Stride - bmpdata.Width * 3;
                }
                img.UnlockBits(bmpdata);
            }
        }

        static int[] NormalizeData(float[] data)
        {
            float dataMax = data.Max();
            float dataMin = data.Min();
            Console.WriteLine("max" + dataMax + " min" + dataMin);
            int[] re = new int[data.Length];
            double k = 0;
            if (dataMin != dataMax)
            {
                k = 255.0 / (dataMax - dataMin);
            }
            for (int i = 0; i < data.Length; i++)
            {
               re[i] = (int) ((data[i] - dataMin) * k);
            }
            return re;
        }


    }


    struct DataInfoItem
    {
        public string Name;
        public string Direction;
    }

}
