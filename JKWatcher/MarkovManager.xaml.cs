using Microsoft.Win32;
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
    /// Interaction logic for MarkovManager.xaml
    /// </summary>
    public partial class MarkovManager : Window
    {
        public MarkovManager()
        {
            InitializeComponent();
        }

        private void markovTrainBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if(ofd.ShowDialog() == true)
            {
                string file = ofd.FileName;
                TaskManager.TaskRun(() => {
                    Markov.RegisterMarkovChain(file,(a,b)=> {
                        UpdateTrainingStatus(a, b);
                    });
                }, $"Training Markov Chain on {file}");
            }
        }

        DateTime lastStatusUpdate = DateTime.Now;
        private void UpdateTrainingStatus(Int64 progress, Int64 total)
        {
            if((DateTime.Now- lastStatusUpdate).TotalMilliseconds < 100)
            {
                return;
            }
            Dispatcher.Invoke(()=> {
                progressBar.Maximum = total;
                progressBar.Value = progress;
            });
            lastStatusUpdate = DateTime.Now;
        }
    }
}
