using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher
{

    class PrimaryKeyEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y)
        {
            if (x is string && y is string)
            {
                return (x as string).Equals(y as string, StringComparison.InvariantCultureIgnoreCase);
            }
            else if (x is Int64 && y is Int64)
            {
                return (x as Int64?).Value.Equals((y as Int64?).Value);
            } // TODO Add more cases if needed
            return x.Equals(y);
        }

        public int GetHashCode([DisallowNull] object obj)
        {
            if(obj is string)
            {
                return (obj as string).GetHashCode(StringComparison.InvariantCultureIgnoreCase);
            } else if (obj is Int64)
            {
                return (obj as Int64?).Value.GetHashCode();
            } // TODO Add more cases if needed
            return obj.GetHashCode();
        }
    }


    static class AsyncPersistentDataManager<T> where T : class,INotifyPropertyChanged, new()
    {
        static string databasePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKWatcher", "persistentData.db");

        static ConcurrentDictionary<object, T> items = new ConcurrentDictionary<object,T>(new PrimaryKeyEqualityComparer());
        static ConcurrentBag<T> itemsToPersist = new ConcurrentBag<T>();

        static System.Reflection.PropertyInfo primaryKeyProperty = null;
        public static void Init()
        {
            Console.WriteLine($"Initing AsyncPersistentDataManager<{typeof(T).Name}>");
        }

        public static T getByPrimaryKey(object key)
        {
            if (items.TryGetValue(key,out T value))
            {
                return value;
            }
            return null;
        }

        public static void addItem(T item, bool overwrite = false)
        {
            object primaryKeyValue = primaryKeyProperty.GetValue(item);
            if (!items.ContainsKey(primaryKeyValue) || overwrite)
            {
                setupChangedListener(item);
                items[primaryKeyValue] = item;
                itemsToPersist.Add(item);
            }
        }

        static void setupChangedListener(T item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            item.PropertyChanged += Item_PropertyChanged;
        }


        static void CommitStuffIfNeeded()
        {
            if(itemsToPersist.Count > 0)
            {
                try
                {
                    using (new GlobalMutexHelper($"JKWatcherAsyncPersistentDataMutex"))
                    {
                        var db = new SQLiteConnection(databasePath, false);

                        db.CreateTable<T>();
                        db.BeginTransaction();
                        while(itemsToPersist.Count > 0)
                        {
                            while (itemsToPersist.TryTake(out T entry))
                            {
                                if (items.Values.Contains(entry))
                                {
                                    db.InsertOrReplace(entry);
                                }
                            }
                        }
                        db.Commit();
                        db.Close();
                        db.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Helpers.logToFile(new string[] { "AsyncPersistentDataManager: Failed to read initial data.", e.ToString() });
                }
            }
        }

        private static void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            itemsToPersist.Add(sender as T);
        }

        static AsyncPersistentDataManager(){

            // Find primary key
            System.Reflection.PropertyInfo[] members = typeof(T).GetProperties();
            foreach (System.Reflection.PropertyInfo property in members)
            {
                var attrs = property.CustomAttributes;
                foreach(System.Reflection.CustomAttributeData attr in attrs)
                {
                    if(attr.AttributeType == typeof(PrimaryKeyAttribute))
                    {
                        if(primaryKeyProperty != null)
                        {
                            throw new InvalidOperationException("Cannot use AsyncPersistentDataManager with types that have more than one primary key.");
                        }
                        primaryKeyProperty = property;
                    }
                }
            }

            if (primaryKeyProperty is null)
            {
                throw new InvalidOperationException("Cannot use AsyncPersistentDataManager with types that don't have a primary key.");
            }

            string typeName = typeof(T).Name;
            LoadData();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            startPersistLoop();
        }

        private static void LoadData() // TODO hmm how to make sure multiple processes can deal with data without overwriting each others data constantly?
        {
            try
            {
                using (new GlobalMutexHelper($"JKWatcherAsyncPersistentDataMutex"))
                {
                    var db = new SQLiteConnection(databasePath, false);

                    db.CreateTable<T>();
                    var dataQuery = db.Table<T>();
                    foreach (T entry in dataQuery)
                    {
                        setupChangedListener(entry);
                        object primaryKeyValue = primaryKeyProperty.GetValue(entry);
                        items[primaryKeyValue] = entry;
                    }
                    db.Close();
                    db.Dispose();
                    lastRead = DateTime.Now;
                }
            }
            catch (Exception e)
            {
                Helpers.logToFile(new string[] { "AsyncPersistentDataManager: Failed to read data.", e.ToString() });
            }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            CommitStuffIfNeeded();
            Destroy();
        }

        static CancellationTokenSource cts = null;

        static private Task backgroundTask = null;
        private static void startPersistLoop()
        {

            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            backgroundTask = Task.Factory.StartNew(() => { persistLoop(ct); }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t) => {

                Helpers.logToFile(new string[] { t.Exception.ToString() });
                TaskManager.TaskRun(() => {
                    Thread.Sleep(5000);
                    startPersistLoop(); // Try to recover.
                }, $"AsyncPersistentDataManager<{typeof(T).Name}> persist loop restarter");
            }, TaskContinuationOptions.OnlyOnFaulted);
            TaskManager.RegisterTask(backgroundTask, $"AsyncPersistentDataManager<{typeof(T).Name}> persist loop");
        }


        static DateTime lastRead = DateTime.Now;

        private static void persistLoop(CancellationToken ct)
        {
            while (true)
            {
                System.Threading.Thread.Sleep(5000);
                if (ct.IsCancellationRequested) return;
                CommitStuffIfNeeded();
                if((DateTime.Now-lastRead).TotalMinutes > 5)
                {
                    LoadData();
                }
            }
        }

        static bool isDestroyed = false;
        static Mutex destructionMutex = new Mutex();
        public static void Destroy()
        {
            lock (destructionMutex)
            {
                if (isDestroyed) return;
                cts.Cancel();
                if (backgroundTask != null)
                {
                    try
                    {
                        backgroundTask.Wait();
                    }
                    catch (Exception e)
                    {
                        // Task cancelled most likely.
                    }
                }
                cts.Dispose();
                isDestroyed = true;
            }

        }
    }
}
