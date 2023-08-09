using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace JKWatcher
{
    class CalendarEvent : INotifyPropertyChanged
    {
        [PrimaryKey, AutoIncrement]
        public Int64 id { get; set; }
        [Indexed]
        public DateTime eventTime { get; set; }
        public bool perpetual { get; set; } = false;
        public string name { get; set; }
        public string announcementTemplate { get; set; }
        public string announcementTemplateSilent { get; set; }
        public string serverIP { get; set; }
        public int minPlayersToBeConsideredActive { get; set; } = 4;
        public bool active { get; set; } = false;
        public bool deleted { get; set; } = false;

        public void ActivateAutoUpdate()
        {
            this.PropertyChanged += CalendarEvent_PropertyChanged;
        }
        ~CalendarEvent()
        {
            this.PropertyChanged -= CalendarEvent_PropertyChanged;
        }
        private void CalendarEvent_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            CalendarManager.UpdateCalendarEvent(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    static class CalendarManager
    {
        static readonly string calendarDatabaseFilename = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "calendar.db");
        static readonly string mutexName = "JKWatcherCalendarSQLiteMutex";
        static CalendarManager()
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    var db = new SQLiteConnection(calendarDatabaseFilename, false);

                    db.CreateTable<CalendarEvent>();
                    db.Close();
                    db.Dispose();
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to create calendar database.", e.ToString() });
            }
        }

        public static CalendarEvent[] GetCalendarEvents(bool onlyUpcoming = true)
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    using (var db = new SQLiteConnection(calendarDatabaseFilename, false))
                    {
                        db.CreateTable<CalendarEvent>();
                        DateTime twoHoursBeforeNow = DateTime.Now - new TimeSpan(6, 0, 0);
                        List<CalendarEvent> res = null;
                        if (onlyUpcoming)
                        {
                            res = db.Query<CalendarEvent>($"SELECT * FROM CalendarEvent WHERE deleted=0 AND (eventTime > ? OR perpetual>0)", twoHoursBeforeNow) as List<CalendarEvent>;
                        }
                        else
                        {
                            res = db.Query<CalendarEvent>($"SELECT * FROM CalendarEvent WHERE deleted=0") as List<CalendarEvent>;
                        }
                        db.Close();
                        if (res == null)
                        {
                            return null;
                        }
                        else
                        {
                            foreach(var item in res)
                            {
                                item.ActivateAutoUpdate();
                            }
                            return res.ToArray();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to get calendar events.", e.ToString() });
                return null;
            }
        }
        public static void CreateNewCalendarEvent()
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    using (var db = new SQLiteConnection(calendarDatabaseFilename, false))
                    {
                        db.CreateTable<CalendarEvent>();
                        db.Insert(new CalendarEvent() { eventTime=DateTime.Now, active = false});
                        db.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to create new calendar event.", e.ToString() });
            }
        }
        public static void UpdateCalendarEvent(CalendarEvent ce)
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    using (var db = new SQLiteConnection(calendarDatabaseFilename, false))
                    {
                        db.CreateTable<CalendarEvent>();
                        db.Update(ce);
                        db.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to update calendar event.", e.ToString() });
            }
        }
    }
}
