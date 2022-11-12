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

namespace JKWatcher.GenericDialogBoxes
{
    /// <summary>
    /// Interaction logic for TextInput.xaml
    /// </summary>
    public partial class TextInput : Window
    {

        private Action<string> callback = null;

        public TextInput(string prompt, Action<string> callbackA)
        {
            InitializeComponent();
            promptTxt.Text = prompt;
            callback = callbackA;
        }

        string response = null;

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            response = responseTxt.Text;
            callback?.Invoke(response);
            this.Close();
        }
    }
}
