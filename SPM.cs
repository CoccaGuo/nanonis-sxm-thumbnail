
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        public bool isUp = false;
        public bool chFreq = false;
        public Bitmap img;
        public float ratio = 1;
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
                    string[] xy = str.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    x = int.Parse(xy[0]);
                    y = int.Parse(xy[1]);
                }
                // scan ratio?
                if (line.StartsWith(":SCAN_RANGE:"))
                {
                    var str = ReadLine(reader).Trim();
                    string[] xy_ = str.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    float x_ = float.Parse(xy_[0]);
                    float y_ = float.Parse(xy_[1]);
                    ratio = y_ / x_;
                }
                // is afm?
                if (line.StartsWith(":Z-Controller>Controller status:"))
                {
                    chFreq = ReadLine(reader).Contains("OFF");
                }

                // scan direction?
                if (line.StartsWith(":SCAN_DIR:"))
                {
                    isUp = ReadLine(reader).ToUpper().Contains("UP");
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
                chName = "Freq";
            }
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].Name.Contains(chName))
                    break;
                else
                {
                    if (channels[i].Direction == "both")
                        chID += 2;
                    else
                        chID++;
                }
            }
            int size = x * y;
            reader.BaseStream.Seek(chID * size * 4, SeekOrigin.Current);
            float[] rawPixel = new float[size];
            for (int i = 0; i < size; i++)
            {
                byte[] next = reader.ReadBytes(4);
                if (isLE) next = next.Reverse().ToArray();
                float num = BitConverter.ToSingle(next, 0);
                rawPixel[i] = num;
            }
            reader.Close();
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
            var _img = new Bitmap(thumbnailSize.Width, thumbnailSize.Height,
                            PixelFormat.Format32bppRgb);
            Rectangle rect = new Rectangle(0, 0, x, y);
            BitmapData bmpdata = _img.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
            int b = 0, g = 1, r = 2;  // BGR
            unsafe
            {
                byte* ptr = (byte*)bmpdata.Scan0;
                if (isUp)
                {
                    for (int row = y - 1; row >= 0; --row)
                    {
                        for (int col = 0; col < x; ++col)
                        {
                            int pix = pixalData[row * x + col];
                            RGB rgb = BluesCMap(pix);
                            ptr[b] = rgb.blue;
                            ptr[g] = rgb.green;
                            ptr[r] = rgb.red;
                            ptr += 4;
                        }
                        // Handling byte alignment issues
                        ptr += bmpdata.Stride - bmpdata.Width * 4;
                    }
                }
                else
                {
                    for (int row = 0; row < y; row++)
                    {
                        for (int col = 0; col < x; ++col)
                        {
                            int pix = pixalData[row * x + col];
                            RGB rgb = BluesCMap(pix);
                            ptr[b] = rgb.blue;
                            ptr[g] = rgb.green;
                            ptr[r] = rgb.red;
                            ptr += 4;
                        }
                        // Handling byte alignment issues
                        ptr += bmpdata.Stride - bmpdata.Width * 4;
                    }
                }
                _img.UnlockBits(bmpdata);
                int scaled_x = x;
                if (scaled_x > 256) scaled_x = 256;
                int scaled_y = (int)(scaled_x * ratio);
                img = new Bitmap(scaled_x, scaled_y);
                using (var gg = Graphics.FromImage(img))
                {
                    gg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gg.DrawImage(_img, 0, 0, scaled_x, scaled_y);
                }
            }
        }

        static int[] NormalizeData(float[] data)
        {
            float[] flitered = data.Where(num => !float.IsNaN(num)).ToArray();
            float dataMax = flitered.Max();
            float dataMin = flitered.Min();
            Console.WriteLine("max" + dataMax + " min" + dataMin);
            int[] re = new int[data.Length];
            double k = 0;
            if (dataMin != dataMax)
            {
                k = 255.0 / (dataMax - dataMin);
            }
            for (int i = 0; i < data.Length; i++)
            {
                if (!float.IsNaN(data[i]))
                    re[i] = (int)((data[i] - dataMin) * k);
                else
                {
                    re[i] = -1;
                }
            }
            return re;
        }

        static RGB BluesCMap(int pix)
        {
            RGB rgb = new RGB();
            switch (pix)
            {
                case -1: rgb.red = 255; rgb.green = 0; rgb.blue = 0; break; // no data
                /* code generated by python, merged from Blue_like_nanonis.lut */
                case 0: rgb.red = 0; rgb.green = 0; rgb.blue = 0; break;
                case 1: rgb.red = 0; rgb.green = 0; rgb.blue = 2; break;
                case 2: rgb.red = 0; rgb.green = 1; rgb.blue = 4; break;
                case 3: rgb.red = 0; rgb.green = 2; rgb.blue = 6; break;
                case 4: rgb.red = 0; rgb.green = 3; rgb.blue = 8; break;
                case 5: rgb.red = 0; rgb.green = 4; rgb.blue = 10; break;
                case 6: rgb.red = 0; rgb.green = 5; rgb.blue = 12; break;
                case 7: rgb.red = 0; rgb.green = 6; rgb.blue = 14; break;
                case 8: rgb.red = 0; rgb.green = 7; rgb.blue = 16; break;
                case 9: rgb.red = 0; rgb.green = 8; rgb.blue = 18; break;
                case 10: rgb.red = 0; rgb.green = 9; rgb.blue = 20; break;
                case 11: rgb.red = 0; rgb.green = 10; rgb.blue = 22; break;
                case 12: rgb.red = 0; rgb.green = 11; rgb.blue = 24; break;
                case 13: rgb.red = 0; rgb.green = 12; rgb.blue = 26; break;
                case 14: rgb.red = 0; rgb.green = 13; rgb.blue = 28; break;
                case 15: rgb.red = 0; rgb.green = 14; rgb.blue = 30; break;
                case 16: rgb.red = 0; rgb.green = 15; rgb.blue = 32; break;
                case 17: rgb.red = 0; rgb.green = 16; rgb.blue = 34; break;
                case 18: rgb.red = 0; rgb.green = 17; rgb.blue = 36; break;
                case 19: rgb.red = 0; rgb.green = 18; rgb.blue = 38; break;
                case 20: rgb.red = 0; rgb.green = 19; rgb.blue = 40; break;
                case 21: rgb.red = 0; rgb.green = 20; rgb.blue = 42; break;
                case 22: rgb.red = 0; rgb.green = 21; rgb.blue = 44; break;
                case 23: rgb.red = 0; rgb.green = 22; rgb.blue = 46; break;
                case 24: rgb.red = 0; rgb.green = 23; rgb.blue = 48; break;
                case 25: rgb.red = 0; rgb.green = 24; rgb.blue = 50; break;
                case 26: rgb.red = 0; rgb.green = 24; rgb.blue = 52; break;
                case 27: rgb.red = 0; rgb.green = 25; rgb.blue = 54; break;
                case 28: rgb.red = 0; rgb.green = 26; rgb.blue = 56; break;
                case 29: rgb.red = 0; rgb.green = 27; rgb.blue = 58; break;
                case 30: rgb.red = 0; rgb.green = 28; rgb.blue = 60; break;
                case 31: rgb.red = 0; rgb.green = 29; rgb.blue = 62; break;
                case 32: rgb.red = 0; rgb.green = 30; rgb.blue = 64; break;
                case 33: rgb.red = 0; rgb.green = 31; rgb.blue = 66; break;
                case 34: rgb.red = 0; rgb.green = 32; rgb.blue = 68; break;
                case 35: rgb.red = 0; rgb.green = 33; rgb.blue = 70; break;
                case 36: rgb.red = 0; rgb.green = 34; rgb.blue = 72; break;
                case 37: rgb.red = 0; rgb.green = 35; rgb.blue = 74; break;
                case 38: rgb.red = 0; rgb.green = 36; rgb.blue = 76; break;
                case 39: rgb.red = 0; rgb.green = 37; rgb.blue = 78; break;
                case 40: rgb.red = 0; rgb.green = 38; rgb.blue = 80; break;
                case 41: rgb.red = 0; rgb.green = 39; rgb.blue = 82; break;
                case 42: rgb.red = 0; rgb.green = 40; rgb.blue = 85; break;
                case 43: rgb.red = 0; rgb.green = 41; rgb.blue = 87; break;
                case 44: rgb.red = 0; rgb.green = 42; rgb.blue = 89; break;
                case 45: rgb.red = 0; rgb.green = 43; rgb.blue = 91; break;
                case 46: rgb.red = 0; rgb.green = 44; rgb.blue = 93; break;
                case 47: rgb.red = 0; rgb.green = 45; rgb.blue = 95; break;
                case 48: rgb.red = 0; rgb.green = 46; rgb.blue = 97; break;
                case 49: rgb.red = 0; rgb.green = 47; rgb.blue = 99; break;
                case 50: rgb.red = 0; rgb.green = 48; rgb.blue = 101; break;
                case 51: rgb.red = 0; rgb.green = 48; rgb.blue = 103; break;
                case 52: rgb.red = 0; rgb.green = 49; rgb.blue = 105; break;
                case 53: rgb.red = 0; rgb.green = 50; rgb.blue = 107; break;
                case 54: rgb.red = 0; rgb.green = 51; rgb.blue = 109; break;
                case 55: rgb.red = 0; rgb.green = 52; rgb.blue = 111; break;
                case 56: rgb.red = 0; rgb.green = 53; rgb.blue = 113; break;
                case 57: rgb.red = 0; rgb.green = 54; rgb.blue = 115; break;
                case 58: rgb.red = 0; rgb.green = 55; rgb.blue = 117; break;
                case 59: rgb.red = 0; rgb.green = 56; rgb.blue = 119; break;
                case 60: rgb.red = 0; rgb.green = 57; rgb.blue = 121; break;
                case 61: rgb.red = 0; rgb.green = 58; rgb.blue = 123; break;
                case 62: rgb.red = 0; rgb.green = 59; rgb.blue = 125; break;
                case 63: rgb.red = 0; rgb.green = 60; rgb.blue = 127; break;
                case 64: rgb.red = 0; rgb.green = 61; rgb.blue = 129; break;
                case 65: rgb.red = 0; rgb.green = 62; rgb.blue = 131; break;
                case 66: rgb.red = 0; rgb.green = 63; rgb.blue = 133; break;
                case 67: rgb.red = 0; rgb.green = 64; rgb.blue = 135; break;
                case 68: rgb.red = 0; rgb.green = 65; rgb.blue = 137; break;
                case 69: rgb.red = 0; rgb.green = 66; rgb.blue = 139; break;
                case 70: rgb.red = 0; rgb.green = 67; rgb.blue = 141; break;
                case 71: rgb.red = 0; rgb.green = 68; rgb.blue = 143; break;
                case 72: rgb.red = 0; rgb.green = 69; rgb.blue = 145; break;
                case 73: rgb.red = 0; rgb.green = 70; rgb.blue = 147; break;
                case 74: rgb.red = 0; rgb.green = 71; rgb.blue = 149; break;
                case 75: rgb.red = 0; rgb.green = 72; rgb.blue = 151; break;
                case 76: rgb.red = 0; rgb.green = 72; rgb.blue = 153; break;
                case 77: rgb.red = 0; rgb.green = 73; rgb.blue = 155; break;
                case 78: rgb.red = 0; rgb.green = 74; rgb.blue = 157; break;
                case 79: rgb.red = 0; rgb.green = 75; rgb.blue = 159; break;
                case 80: rgb.red = 0; rgb.green = 76; rgb.blue = 161; break;
                case 81: rgb.red = 0; rgb.green = 77; rgb.blue = 163; break;
                case 82: rgb.red = 0; rgb.green = 78; rgb.blue = 165; break;
                case 83: rgb.red = 0; rgb.green = 79; rgb.blue = 167; break;
                case 84: rgb.red = 0; rgb.green = 80; rgb.blue = 170; break;
                case 85: rgb.red = 0; rgb.green = 81; rgb.blue = 172; break;
                case 86: rgb.red = 0; rgb.green = 82; rgb.blue = 174; break;
                case 87: rgb.red = 0; rgb.green = 83; rgb.blue = 176; break;
                case 88: rgb.red = 0; rgb.green = 84; rgb.blue = 178; break;
                case 89: rgb.red = 0; rgb.green = 85; rgb.blue = 180; break;
                case 90: rgb.red = 0; rgb.green = 86; rgb.blue = 182; break;
                case 91: rgb.red = 0; rgb.green = 87; rgb.blue = 184; break;
                case 92: rgb.red = 0; rgb.green = 88; rgb.blue = 186; break;
                case 93: rgb.red = 0; rgb.green = 89; rgb.blue = 188; break;
                case 94: rgb.red = 0; rgb.green = 90; rgb.blue = 190; break;
                case 95: rgb.red = 0; rgb.green = 91; rgb.blue = 192; break;
                case 96: rgb.red = 0; rgb.green = 92; rgb.blue = 194; break;
                case 97: rgb.red = 0; rgb.green = 93; rgb.blue = 196; break;
                case 98: rgb.red = 0; rgb.green = 94; rgb.blue = 198; break;
                case 99: rgb.red = 0; rgb.green = 95; rgb.blue = 200; break;
                case 100: rgb.red = 0; rgb.green = 96; rgb.blue = 202; break;
                case 101: rgb.red = 0; rgb.green = 96; rgb.blue = 204; break;
                case 102: rgb.red = 0; rgb.green = 97; rgb.blue = 206; break;
                case 103: rgb.red = 0; rgb.green = 98; rgb.blue = 208; break;
                case 104: rgb.red = 0; rgb.green = 99; rgb.blue = 210; break;
                case 105: rgb.red = 0; rgb.green = 100; rgb.blue = 212; break;
                case 106: rgb.red = 0; rgb.green = 101; rgb.blue = 214; break;
                case 107: rgb.red = 0; rgb.green = 102; rgb.blue = 216; break;
                case 108: rgb.red = 0; rgb.green = 103; rgb.blue = 218; break;
                case 109: rgb.red = 0; rgb.green = 104; rgb.blue = 220; break;
                case 110: rgb.red = 0; rgb.green = 105; rgb.blue = 222; break;
                case 111: rgb.red = 0; rgb.green = 106; rgb.blue = 224; break;
                case 112: rgb.red = 0; rgb.green = 107; rgb.blue = 226; break;
                case 113: rgb.red = 0; rgb.green = 108; rgb.blue = 228; break;
                case 114: rgb.red = 0; rgb.green = 109; rgb.blue = 230; break;
                case 115: rgb.red = 0; rgb.green = 110; rgb.blue = 232; break;
                case 116: rgb.red = 0; rgb.green = 111; rgb.blue = 234; break;
                case 117: rgb.red = 0; rgb.green = 112; rgb.blue = 236; break;
                case 118: rgb.red = 0; rgb.green = 113; rgb.blue = 238; break;
                case 119: rgb.red = 0; rgb.green = 114; rgb.blue = 240; break;
                case 120: rgb.red = 0; rgb.green = 115; rgb.blue = 242; break;
                case 121: rgb.red = 0; rgb.green = 116; rgb.blue = 244; break;
                case 122: rgb.red = 0; rgb.green = 117; rgb.blue = 246; break;
                case 123: rgb.red = 0; rgb.green = 118; rgb.blue = 248; break;
                case 124: rgb.red = 0; rgb.green = 119; rgb.blue = 250; break;
                case 125: rgb.red = 0; rgb.green = 120; rgb.blue = 252; break;
                case 126: rgb.red = 0; rgb.green = 121; rgb.blue = 255; break;
                case 127: rgb.red = 1; rgb.green = 122; rgb.blue = 255; break;
                case 128: rgb.red = 3; rgb.green = 123; rgb.blue = 255; break;
                case 129: rgb.red = 5; rgb.green = 124; rgb.blue = 255; break;
                case 130: rgb.red = 7; rgb.green = 125; rgb.blue = 255; break;
                case 131: rgb.red = 9; rgb.green = 126; rgb.blue = 255; break;
                case 132: rgb.red = 11; rgb.green = 127; rgb.blue = 255; break;
                case 133: rgb.red = 13; rgb.green = 128; rgb.blue = 255; break;
                case 134: rgb.red = 15; rgb.green = 129; rgb.blue = 255; break;
                case 135: rgb.red = 17; rgb.green = 130; rgb.blue = 255; break;
                case 136: rgb.red = 19; rgb.green = 131; rgb.blue = 255; break;
                case 137: rgb.red = 21; rgb.green = 132; rgb.blue = 255; break;
                case 138: rgb.red = 23; rgb.green = 133; rgb.blue = 255; break;
                case 139: rgb.red = 25; rgb.green = 134; rgb.blue = 255; break;
                case 140: rgb.red = 27; rgb.green = 135; rgb.blue = 255; break;
                case 141: rgb.red = 29; rgb.green = 136; rgb.blue = 255; break;
                case 142: rgb.red = 31; rgb.green = 138; rgb.blue = 255; break;
                case 143: rgb.red = 33; rgb.green = 139; rgb.blue = 255; break;
                case 144: rgb.red = 35; rgb.green = 140; rgb.blue = 255; break;
                case 145: rgb.red = 37; rgb.green = 141; rgb.blue = 255; break;
                case 146: rgb.red = 39; rgb.green = 142; rgb.blue = 255; break;
                case 147: rgb.red = 41; rgb.green = 143; rgb.blue = 255; break;
                case 148: rgb.red = 43; rgb.green = 144; rgb.blue = 255; break;
                case 149: rgb.red = 45; rgb.green = 145; rgb.blue = 255; break;
                case 150: rgb.red = 47; rgb.green = 146; rgb.blue = 255; break;
                case 151: rgb.red = 49; rgb.green = 147; rgb.blue = 255; break;
                case 152: rgb.red = 51; rgb.green = 148; rgb.blue = 255; break;
                case 153: rgb.red = 53; rgb.green = 149; rgb.blue = 255; break;
                case 154: rgb.red = 55; rgb.green = 150; rgb.blue = 255; break;
                case 155: rgb.red = 57; rgb.green = 151; rgb.blue = 255; break;
                case 156: rgb.red = 59; rgb.green = 152; rgb.blue = 255; break;
                case 157: rgb.red = 61; rgb.green = 153; rgb.blue = 255; break;
                case 158: rgb.red = 63; rgb.green = 155; rgb.blue = 255; break;
                case 159: rgb.red = 65; rgb.green = 156; rgb.blue = 255; break;
                case 160: rgb.red = 67; rgb.green = 157; rgb.blue = 255; break;
                case 161: rgb.red = 69; rgb.green = 158; rgb.blue = 255; break;
                case 162: rgb.red = 71; rgb.green = 159; rgb.blue = 255; break;
                case 163: rgb.red = 73; rgb.green = 160; rgb.blue = 255; break;
                case 164: rgb.red = 75; rgb.green = 161; rgb.blue = 255; break;
                case 165: rgb.red = 77; rgb.green = 162; rgb.blue = 255; break;
                case 166: rgb.red = 79; rgb.green = 163; rgb.blue = 255; break;
                case 167: rgb.red = 81; rgb.green = 164; rgb.blue = 255; break;
                case 168: rgb.red = 83; rgb.green = 165; rgb.blue = 255; break;
                case 169: rgb.red = 85; rgb.green = 166; rgb.blue = 255; break;
                case 170: rgb.red = 86; rgb.green = 167; rgb.blue = 255; break;
                case 171: rgb.red = 88; rgb.green = 168; rgb.blue = 255; break;
                case 172: rgb.red = 90; rgb.green = 169; rgb.blue = 255; break;
                case 173: rgb.red = 92; rgb.green = 170; rgb.blue = 255; break;
                case 174: rgb.red = 94; rgb.green = 172; rgb.blue = 255; break;
                case 175: rgb.red = 96; rgb.green = 173; rgb.blue = 255; break;
                case 176: rgb.red = 98; rgb.green = 174; rgb.blue = 255; break;
                case 177: rgb.red = 100; rgb.green = 175; rgb.blue = 255; break;
                case 178: rgb.red = 102; rgb.green = 176; rgb.blue = 255; break;
                case 179: rgb.red = 104; rgb.green = 177; rgb.blue = 255; break;
                case 180: rgb.red = 106; rgb.green = 178; rgb.blue = 255; break;
                case 181: rgb.red = 108; rgb.green = 179; rgb.blue = 255; break;
                case 182: rgb.red = 110; rgb.green = 180; rgb.blue = 255; break;
                case 183: rgb.red = 112; rgb.green = 181; rgb.blue = 255; break;
                case 184: rgb.red = 114; rgb.green = 182; rgb.blue = 255; break;
                case 185: rgb.red = 116; rgb.green = 183; rgb.blue = 255; break;
                case 186: rgb.red = 118; rgb.green = 184; rgb.blue = 255; break;
                case 187: rgb.red = 120; rgb.green = 185; rgb.blue = 255; break;
                case 188: rgb.red = 122; rgb.green = 186; rgb.blue = 255; break;
                case 189: rgb.red = 124; rgb.green = 188; rgb.blue = 255; break;
                case 190: rgb.red = 126; rgb.green = 189; rgb.blue = 255; break;
                case 191: rgb.red = 128; rgb.green = 190; rgb.blue = 255; break;
                case 192: rgb.red = 130; rgb.green = 191; rgb.blue = 255; break;
                case 193: rgb.red = 132; rgb.green = 192; rgb.blue = 255; break;
                case 194: rgb.red = 134; rgb.green = 193; rgb.blue = 255; break;
                case 195: rgb.red = 136; rgb.green = 194; rgb.blue = 255; break;
                case 196: rgb.red = 138; rgb.green = 195; rgb.blue = 255; break;
                case 197: rgb.red = 140; rgb.green = 196; rgb.blue = 255; break;
                case 198: rgb.red = 142; rgb.green = 197; rgb.blue = 255; break;
                case 199: rgb.red = 144; rgb.green = 198; rgb.blue = 255; break;
                case 200: rgb.red = 146; rgb.green = 199; rgb.blue = 255; break;
                case 201: rgb.red = 148; rgb.green = 200; rgb.blue = 255; break;
                case 202: rgb.red = 150; rgb.green = 201; rgb.blue = 255; break;
                case 203: rgb.red = 152; rgb.green = 202; rgb.blue = 255; break;
                case 204: rgb.red = 154; rgb.green = 203; rgb.blue = 255; break;
                case 205: rgb.red = 156; rgb.green = 205; rgb.blue = 255; break;
                case 206: rgb.red = 158; rgb.green = 206; rgb.blue = 255; break;
                case 207: rgb.red = 160; rgb.green = 207; rgb.blue = 255; break;
                case 208: rgb.red = 162; rgb.green = 208; rgb.blue = 255; break;
                case 209: rgb.red = 164; rgb.green = 209; rgb.blue = 255; break;
                case 210: rgb.red = 166; rgb.green = 210; rgb.blue = 255; break;
                case 211: rgb.red = 168; rgb.green = 211; rgb.blue = 255; break;
                case 212: rgb.red = 170; rgb.green = 212; rgb.blue = 255; break;
                case 213: rgb.red = 171; rgb.green = 213; rgb.blue = 255; break;
                case 214: rgb.red = 173; rgb.green = 214; rgb.blue = 255; break;
                case 215: rgb.red = 175; rgb.green = 215; rgb.blue = 255; break;
                case 216: rgb.red = 177; rgb.green = 216; rgb.blue = 255; break;
                case 217: rgb.red = 179; rgb.green = 217; rgb.blue = 255; break;
                case 218: rgb.red = 181; rgb.green = 218; rgb.blue = 255; break;
                case 219: rgb.red = 183; rgb.green = 219; rgb.blue = 255; break;
                case 220: rgb.red = 185; rgb.green = 220; rgb.blue = 255; break;
                case 221: rgb.red = 187; rgb.green = 222; rgb.blue = 255; break;
                case 222: rgb.red = 189; rgb.green = 223; rgb.blue = 255; break;
                case 223: rgb.red = 191; rgb.green = 224; rgb.blue = 255; break;
                case 224: rgb.red = 193; rgb.green = 225; rgb.blue = 255; break;
                case 225: rgb.red = 195; rgb.green = 226; rgb.blue = 255; break;
                case 226: rgb.red = 197; rgb.green = 227; rgb.blue = 255; break;
                case 227: rgb.red = 199; rgb.green = 228; rgb.blue = 255; break;
                case 228: rgb.red = 201; rgb.green = 229; rgb.blue = 255; break;
                case 229: rgb.red = 203; rgb.green = 230; rgb.blue = 255; break;
                case 230: rgb.red = 205; rgb.green = 231; rgb.blue = 255; break;
                case 231: rgb.red = 207; rgb.green = 232; rgb.blue = 255; break;
                case 232: rgb.red = 209; rgb.green = 233; rgb.blue = 255; break;
                case 233: rgb.red = 211; rgb.green = 234; rgb.blue = 255; break;
                case 234: rgb.red = 213; rgb.green = 235; rgb.blue = 255; break;
                case 235: rgb.red = 215; rgb.green = 236; rgb.blue = 255; break;
                case 236: rgb.red = 217; rgb.green = 237; rgb.blue = 255; break;
                case 237: rgb.red = 219; rgb.green = 239; rgb.blue = 255; break;
                case 238: rgb.red = 221; rgb.green = 240; rgb.blue = 255; break;
                case 239: rgb.red = 223; rgb.green = 241; rgb.blue = 255; break;
                case 240: rgb.red = 225; rgb.green = 242; rgb.blue = 255; break;
                case 241: rgb.red = 227; rgb.green = 243; rgb.blue = 255; break;
                case 242: rgb.red = 229; rgb.green = 244; rgb.blue = 255; break;
                case 243: rgb.red = 231; rgb.green = 245; rgb.blue = 255; break;
                case 244: rgb.red = 233; rgb.green = 246; rgb.blue = 255; break;
                case 245: rgb.red = 235; rgb.green = 247; rgb.blue = 255; break;
                case 246: rgb.red = 237; rgb.green = 248; rgb.blue = 255; break;
                case 247: rgb.red = 239; rgb.green = 249; rgb.blue = 255; break;
                case 248: rgb.red = 241; rgb.green = 250; rgb.blue = 255; break;
                case 249: rgb.red = 243; rgb.green = 251; rgb.blue = 255; break;
                case 250: rgb.red = 245; rgb.green = 252; rgb.blue = 255; break;
                case 251: rgb.red = 247; rgb.green = 253; rgb.blue = 255; break;
                case 252: rgb.red = 249; rgb.green = 255; rgb.blue = 255; break;
                case 253: rgb.red = 251; rgb.green = 255; rgb.blue = 255; break;
                case 254: rgb.red = 253; rgb.green = 255; rgb.blue = 255; break;
                case 255: rgb.red = 255; rgb.green = 255; rgb.blue = 255; break;
            }
            return rgb;
        }


    }

    struct RGB
    {
        public byte red, green, blue;
    }


    struct DataInfoItem
    {
        public string Name;
        public string Direction;
    }


}
