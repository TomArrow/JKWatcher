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
    public partial class BooleanInput : Window
    {

        private Action<bool[]> callback = null;

        private bool[] data = new bool[0];

        public BooleanInput(string[] prompts, Action<bool[]> callbackA)
        {
            InitializeComponent();
            elementsPanel.Children.Clear();
            data = new bool[prompts.Length];
            int index = 0;
            foreach(string prompt in prompts)
            {
                int localIndex = index;
                CheckBox chkbox = new CheckBox() { Content = prompt,  };
                chkbox.Checked += (a,b)=> {
                    data[localIndex] = true;
                };
                chkbox.Unchecked += (a,b)=> {
                    data[localIndex] = false;
                };
                elementsPanel.Children.Add(chkbox);
                index++;
            }
            callback = callbackA;
        }


        string response = null;

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            callback?.Invoke(data);
            this.Close();
        }
    }
}
