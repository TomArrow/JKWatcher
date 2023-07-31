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
        public string name { get; set; }
        public string announcementTemplate { get; set; }
        public string announcementTemplateSilent { get; set; }
        public string serverIP { get; set; }
        public bool active { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;
    }

    static class CalendarManager
    {

        static readonly string mutexName = "JKWatcherCalendarSQLiteMutex";
        static CalendarManager()
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    var db = new SQLiteConnection(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "calendar.db"), false);

                    db.CreateTable<CalendarEvent>();
                    //db.BeginTransaction();
                    //db.Commit();
                    db.Close();
                    db.Dispose();
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to create calendar database.", e.ToString() });
            }
        }

        static CalendarEvent[] GetCalendarEvents(bool onlyUpcoming = true)
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    using (var db = new SQLiteConnection(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "calendar.db"), false))
                    {
                        db.CreateTable<CalendarEvent>();
                        DateTime twoHoursFromNow = DateTime.Now + new TimeSpan(2, 0, 0);
                        List<CalendarEvent> res = null;
                        if (onlyUpcoming)
                        {
                            res = db.Query<CalendarEvent>($"SELECT * FROM CalendarEvent WHERE eventTime > ?", twoHoursFromNow) as List<CalendarEvent>;
                        }
                        else
                        {
                            res = db.Query<CalendarEvent>($"SELECT * FROM CalendarEvent") as List<CalendarEvent>;
                        }
                        db.Close();
                        return res == null ? null: res.ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "Failed to get calendar events.", e.ToString() });
                return null;
            }
        }
        static void CreateNewCalendarEvent()
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    using (var db = new SQLiteConnection(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "calendar.db"), false))
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
        static void UpdateCalendarEvent(CalendarEvent ce)
        {
            try
            {
                using (new GlobalMutexHelper(mutexName))
                {
                    using (var db = new SQLiteConnection(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "calendar.db"), false))
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
