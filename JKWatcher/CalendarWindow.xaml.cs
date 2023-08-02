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
    /// Interaction logic for CalendarWindow.xaml
    /// </summary>
    public partial class CalendarWindow : Window
    {
        public CalendarWindow()
        {
            InitializeComponent();
            UpdateEvents();
        }

        private void UpdateEvents()
        {

            calendarEventsGrid.ItemsSource = CalendarManager.GetCalendarEvents();
        }

        private void newEventBtn_Click(object sender, RoutedEventArgs e)
        {
            CalendarManager.CreateNewCalendarEvent();
            UpdateEvents();

        }

        private void deleteEventsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (calendarEventsGrid.SelectedItems.Count == 0) return;

            List<CalendarEvent> eventsToDelete = calendarEventsGrid.SelectedItems.Cast<CalendarEvent>().ToList();
            foreach(CalendarEvent eventToDelete in eventsToDelete){
                eventToDelete.deleted = true;
            }
            UpdateEvents();
        }
    }
}
