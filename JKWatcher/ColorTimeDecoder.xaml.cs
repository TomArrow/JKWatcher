using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace JKWatcher
{
    /// <summary>
    /// Interaction logic for ColorTimeDecoder.xaml
    /// </summary>
    public partial class ColorTimeDecoder : Window
    {

        Vector4[] colorsVectors = new Vector4[]
        {
            new Vector4(0.0f, 0.0f, 0.0f, 1.0f),           // ^0 -> black
	        new Vector4(1.0f, 0.0f, 0.0f, 1.0f),           // ^1 -> red
	        new Vector4(0.0f, 1.0f, 0.0f, 1.0f),           // ^2 -> green
	        new Vector4(1.0f, 1.0f, 0.0f, 1.0f),           // ^3 -> yellow
	        new Vector4(0.0f, 0.0f, 1.0f, 1.0f),           // ^4 -> blue
	        new Vector4(0.0f, 1.0f, 1.0f, 1.0f),           // ^5 -> cyan
	        new Vector4(1.0f, 0.0f, 1.0f, 1.0f),           // ^6 -> magenta
	        new Vector4(1.0f, 1.0f, 1.0f, 1.0f),           // ^7 -> white
        };
        SolidColorBrush[] colorBrushes = null;

        byte[] colorValues = new byte[12] { 9,9,9,9,9,9,9,9,9,9,9,9 };

        DateTime getLocalTimeFromColorString(string theString)
        {

            Int64 unixTime = Convert.ToInt64(theString, 8);
            DateTime utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
            DateTime hereTime = utcTime.ToLocalTime();
            return hereTime;
        }

        private void updateTimeDisplay()
        {
            StringBuilder sbMin = new StringBuilder();
            StringBuilder sbMax = new StringBuilder();
            bool hasEnded = false;
            for (int i = 0; i < 12; i++)
            {
                if (colorValues[i] == 9) hasEnded = true; // Lame way of doing it but meh.
                if (!hasEnded)
                {
                    sbMin.Append(colorValues[i].ToString());
                    sbMax.Append(colorValues[i].ToString());
                } else
                {
                    sbMin.Append("0");
                    sbMax.Append("7");
                }
            }
            DateTime hereTimeMin = getLocalTimeFromColorString(sbMin.ToString());
            DateTime hereTimeMax = getLocalTimeFromColorString(sbMax.ToString());
            minTimeTxt.Text = hereTimeMin.ToString("yyyy-MM-dd_HH-mm-ss");
            maxTimeTxt.Text = hereTimeMax.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        public ColorTimeDecoder()
        {
            InitializeComponent();
            colorBrushes = new SolidColorBrush[colorsVectors.Length];
            for(int i = 0; i < colorsVectors.Length; i++)
            {
                colorBrushes[i] = new SolidColorBrush(Color.FromArgb(
                    (byte)(colorsVectors[i].W*255f),
                    (byte)(colorsVectors[i].X*255f),
                    (byte)(colorsVectors[i].Y*255f),
                    (byte)(colorsVectors[i].Z*255f)
                    ));
            }
            for(int i = 0; i < 12; i++)
            {
                var stackPanel = new StackPanel();
                var noneRadio = new RadioButton();
                noneRadio.Content = "N/A";
                noneRadio.GroupName = i.ToString();
                int localIndex = i;
                noneRadio.Checked += (v, x) => {
                    colorValues[localIndex] = 9;
                    updateTimeDisplay();
                };
                stackPanel.Children.Add(noneRadio);
                for (byte a = 0; a < colorBrushes.Length; a++)
                {
                    var wrap = new WrapPanel();
                    var rectangle = new Rectangle();
                    rectangle.Fill = colorBrushes[a];
                    rectangle.Width = rectangle.Height = 20;

                    var thisRadio = new RadioButton();
                    thisRadio.GroupName = i.ToString();
                    byte localnum = a;
                    thisRadio.Checked += (v, x) => {
                        colorValues[localIndex] = localnum;
                        updateTimeDisplay();
                    };
                    rectangle.MouseUp += (v,x) => { thisRadio.IsChecked= true; };
                    wrap.Children.Add(thisRadio);
                    wrap.Children.Add(rectangle);

                    stackPanel.Children.Add(wrap);
                }
                selectStuffPanel.Children.Add(stackPanel);
            }
        }
    }
}
