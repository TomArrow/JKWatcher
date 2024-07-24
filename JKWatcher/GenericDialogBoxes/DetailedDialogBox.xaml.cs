using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for DetailedDialogBox.xaml
    /// </summary>
    public partial class DetailedDialogBox : Window, INotifyPropertyChanged
    {
        public MessageBoxResult result = MessageBoxResult.Cancel;

        public event PropertyChangedEventHandler PropertyChanged;

        public string OkBtnText { get; set; } = "OK";
        public string CancelBtnText { get; set; } = "Cancel";
        public string DetailsText { get; set; } = "Details....";
        public string QuestionText { get; set; } = "Do you want Blah?";
        public string HeaderText { get; set; } = "Dialog box";
        [DependsOn("OkBtnText")]
        public Visibility OkBtnVisibility => string.IsNullOrWhiteSpace(OkBtnText) ? Visibility.Collapsed: Visibility.Visible;
        [DependsOn("CancelBtnText")]
        public Visibility CancelBtnVisibility => string.IsNullOrWhiteSpace(CancelBtnText) ? Visibility.Collapsed: Visibility.Visible;
        public DetailedDialogBox(string questionText, string detailsText, string headerText, string okBtnText = "OK", string cancelBtnText = "Cancel")
        {
            InitializeComponent();
            this.OkBtnText = okBtnText;
            this.CancelBtnText = cancelBtnText;
            this.QuestionText = questionText;
            this.DetailsText = detailsText;
            this.HeaderText = headerText;
            this.DataContext = this;
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DialogResult = false;
        }

        private void yesBtn_Click(object sender, RoutedEventArgs e)
        {
            result = MessageBoxResult.Yes;
            DialogResult = true;
        }

        private void noBtn_Click(object sender, RoutedEventArgs e)
        {
            result = MessageBoxResult.No;
            DialogResult = false;
        }

        private void okBtn_Click(object sender, RoutedEventArgs e)
        {
            result = MessageBoxResult.OK;
            DialogResult = false;
        }

        private void cancelBtn_Click(object sender, RoutedEventArgs e)
        {
            result = MessageBoxResult.Cancel;
            DialogResult = false;
        }
    }
}
