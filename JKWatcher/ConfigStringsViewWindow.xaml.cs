using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Interaction logic for ConfigStringsViewWindow.xaml
    /// </summary>
    public partial class ConfigStringsViewWindow : Window
    {

        class ConfigString
        {
            public int index { get; init; } = 0;
            public string value { get; init; } = "";
            public KeyValuePair<string, string>[] infoStringValues { get; init; } = null;
        }
        ConfigString[] CSStrings = null;

        public ConfigStringsViewWindow(string[] configStrings)
        {
            InitializeComponent();
            if (configStrings == null)
            {
                CSStrings = new ConfigString[0];
            }
            else
            {
                List<ConfigString> configStringsNew = new List<ConfigString>();
                for (int i = 0; i < configStrings.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(configStrings[i]))
                    {
                        configStringsNew.Add(new ConfigString() { index = i, value = configStrings[i], infoStringValues = (new JKClient.InfoString(configStrings[i])).ToArray() });
                    }
                }
                CSStrings = configStringsNew.ToArray();
            }
            configStringListGrid.ItemsSource = CSStrings;
        }
        /*
        private void configStringList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfigString item = configStringListGrid.SelectedItem as ConfigString;
            if(item != null)
            {
                JKClient.InfoString infoString = new JKClient.InfoString(item.value);
            }
        }*/
    }
}
