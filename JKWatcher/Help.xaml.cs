using MdXaml;
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
    /// Interaction logic for Help.xaml
    /// </summary>
    public partial class Help : Window
    {
        public Help()
        {
            InitializeComponent();

            string configsHelpString = Encoding.UTF8.GetString(Helpers.GetResourceData("documentation/configs.md"));

            configsReader.Markdown = configsHelpString;
            configsReader.MarkdownStyle = MarkdownStyle.SasabuneStandard;
           //configsReader.MarkdownStyle = MarkdownStyle.Sasabune;
        }
    }
}
