using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JKWatcher
{
    static class TaskManager
    {

        public class RunningTask
        {
            public string taskName { get; init; }
            public UInt64 taskNum { get; init; }
            public DateTime startTime { get; init; } = DateTime.Now;
            public bool essential { get; init; } = false;
            public Task task { get; init; }
        }

        private static object taskNumberLockObject = new object();
        private static UInt64 nextTaskNumber = 0;

        private static List<RunningTask> runningTasks = new List<RunningTask> ();

        public static UInt64 RegisterTask(Task task, string taskName, bool essential=false)
        {
            while (task is Task<Task>)
            {
                task = (task as Task<Task>).Unwrap();
            }

            UInt64 taskNum = 0;
            lock (taskNumberLockObject)
            {
                taskNum = nextTaskNumber++;
            }

            RunningTask runningTask = new RunningTask()
            { 
                taskName = taskName, taskNum = taskNum, task= task, essential = essential
            };

            lock (runningTasks)
            {
                runningTasks.Add(runningTask);
            }

            Debug.WriteLine($"TaskManager: Task #{taskNum} ({taskName}) registered.");
            task.ContinueWith((a)=> {

                lock (runningTasks)
                {
                    runningTasks.Remove(runningTask);
                }
                Debug.WriteLine($"TaskManager: Task #{taskNum} ({taskName}) ended, status: {a.Status}.");
            });

            return taskNum;
        }

        public static RunningTask[] GetRunningTasks()
        {
            lock (runningTasks)
            {
                return runningTasks.ToArray();
            }
        }

        public static Task TaskRun(Action action, string taskName, bool essential = false)
        {
            Task task = Task.Run(action);
            while (task is Task<Task>)
            {
                task = (task as Task<Task>).Unwrap();
            }
            RegisterTask(task,taskName,essential);
            return task;
        }
        public static Task TaskRun(Func<Task> action, string taskName, bool essential = false)
        {
            Task task = action();
            while (task is Task<Task>)
            {
                task = (task as Task<Task>).Unwrap();
            }
            RegisterTask(task,taskName, essential);
            return task;
        }

    }
}
