using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher
{

    class SleepInterrupter // To avoid any issues with calling cancellationtokensources that are already disposed etc.
    {
        struct TokenPair
        {
            public CancellationTokenSource cts;
            public CancellationToken ct;
        }
        ConcurrentBag<TokenPair> tokens = new ConcurrentBag<TokenPair>();

        public CancellationToken getCancellationToken()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            TokenPair tp = new TokenPair { cts = cts, ct = ct };
            lock (tokens)
            {
                tokens.Add(tp);
            }
            return ct;
        }
        public void CancelAll()
        {
            lock (tokens)
            {
                foreach (TokenPair tp in tokens)
                {
                    tp.cts.Cancel();
                    tp.cts.Dispose();
                }
                tokens.Clear();
            }
        }
    }

    public class LeakyBucketRequester<TCommand,TKind> where TKind:IComparable
    {
        private volatile int burst = 1;
        private volatile int period = 1100;

        private int thisBucketBurst = 0;
        private DateTime lastTime = DateTime.Now;

        

        public enum RequestBehavior
        {
            ENQUEUE,
            DELETE_PREVIOUS_OF_SAME_TYPE,
            DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS,
        }

        public struct Request
        {
            public TCommand command { get; init; }
            public TKind type { get; init; } // custom defined by caller
            public int minimumDelayFromSameType { get; init; }
            public int priority { get; init; }
        }

        public struct FinishedRequest
        {
            public DateTime time { get; init; }
            public Request request { get; init; }
        }

        // Some things to get stats from the outside
        //public IReadOnlyCollection<Request> ReadOnlyRequestList => requestQueue.AsReadOnly();
        public event EventHandler<Tuple<Request[],FinishedRequest[]>> RequestListUpdated;
        public void OnRequestListUpdated()
        {
            RequestListUpdated?.Invoke(this,new Tuple<Request[], FinishedRequest[]>(requestQueue.ToArray(),recentExecutedCommands.ToArray()));
        }
        private List<FinishedRequest> recentExecutedCommands = new List<FinishedRequest>();
        //public IReadOnlyCollection<FinishedRequest> RecentExecutedCommandsReadOnly => recentExecutedCommands.AsReadOnly();
        private void LogCommand(Request command)
        {
            lock (recentExecutedCommands)
            {
                int removeCount = 0;
                for (int i = 0; i < recentExecutedCommands.Count; i++)
                {
                    // Remove stuff older than 1 minute
                    if ((DateTime.Now - recentExecutedCommands[i].time).TotalMilliseconds > 60000)
                    {
                        removeCount++;
                    }
                    else
                    {
                        break;
                    }
                }
                if(removeCount > 0)
                {
                    recentExecutedCommands.RemoveRange(0, removeCount);
                }
                recentExecutedCommands.Add(new FinishedRequest() { time = DateTime.Now, request = command });
            }
        }


        // Back to real stuff
        List<Request> requestQueue = new List<Request>();
        Dictionary<TKind, DateTime> lastRequestTimesOfType = new Dictionary<TKind, DateTime>();

        private CancellationTokenSource cts = null;
        private TaskCompletionSource<bool> loopEnded;

        public LeakyBucketRequester(int burstA, int periodA)
        {
            burst = burstA;
            period = periodA;

            loopEnded = new TaskCompletionSource<bool>();
            cts = new CancellationTokenSource();
            Task.Factory.StartNew(()=> { this.Run(cts.Token); }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).ContinueWith((t)=> {
                loopEnded.TrySetResult(true);
            });
        }

        public void changeParameters(int burstA, int periodA)
        {
            burst = burstA;
            period = periodA;
        }

        ~LeakyBucketRequester()
        {
            Stop();
            //sleepInterruptor.Dispose();
            //sleepInterruptor = null;
        }

        public void Stop()
        {
            cts.Cancel();
            loopEnded.Task.Wait();
            sleepInterrupter.CancelAll();
            //sleepInterruptor.Cancel();
            //CancellationTokenSource tmpCts = sleepInterruptor;
            //sleepInterruptor = new CancellationTokenSource();
            //tmpCts.Dispose();
            cts.Dispose(); 
            cts = null;
        }

        //CancellationTokenSource sleepInterruptor = new CancellationTokenSource();
        SleepInterrupter sleepInterrupter = new SleepInterrupter();

        const int defaultTimeOut = 1100;
        const int safetyPadding = 10;

        private async void Run(CancellationToken ct)
        {
            int soonestPredictedEvent = defaultTimeOut;
            while (true)
            {
                //CancellationTokenSource tmpCts = sleepInterruptor;
                //sleepInterruptor = new CancellationTokenSource();
                //tmpCts.Dispose();
                try { 
                    //await Task.Delay(soonestPredictedEvent, sleepInterruptor.Token);
                    await Task.Delay(soonestPredictedEvent, sleepInterrupter.getCancellationToken());
                } catch (TaskCanceledException e)
                {
                    // Do nothing, it's expected.
                }
                //ct.ThrowIfCancellationRequested();
                if (ct.IsCancellationRequested) return;
                soonestPredictedEvent = defaultTimeOut;

                int timeUntilNextRequestAllowed = whenCanIRequest();
                if(timeUntilNextRequestAllowed > 0)
                {
                    soonestPredictedEvent = timeUntilNextRequestAllowed + safetyPadding;
                    continue; // Can't really execute any requests right now so wait until we can.
                }

                // Now we should be able to actually send requests.
                bool somethingToDo = true;
                while (somethingToDo)
                {
                    bool requestQueueChanged = false;
                    lock (requestQueue)
                    {
                        if(requestQueue.Count == 0)
                        {
                            somethingToDo = false;
                            break;
                        }

                        // Find highest priority and work down from there
                        int priorityToMatch = 0;
                        foreach(Request tmpReq in requestQueue)
                        {
                            priorityToMatch = Math.Max(priorityToMatch, tmpReq.priority);
                        }

                        bool atLeastOneWantedExecution = false;
                        while (priorityToMatch >= 0) {

                            // Actually start executing stuff
                            for(int i = 0; i < requestQueue.Count; i++)
                            {
                                if (requestQueue[i].priority != priorityToMatch) continue;
                                bool wantstoExecute = true;

                                // Check if this even wants to execute or if the last event of this type was too recent.
                                if(requestQueue[i].minimumDelayFromSameType > 0)
                                {
                                    if (lastRequestTimesOfType.ContainsKey(requestQueue[i].type))
                                    {
                                        int timeSinceLastRequestOfThisType = (int)(DateTime.Now - lastRequestTimesOfType[requestQueue[i].type]).TotalMilliseconds;
                                        int timeUntilThisAllowed = requestQueue[i].minimumDelayFromSameType - timeSinceLastRequestOfThisType;
                                        if (timeUntilThisAllowed > 0) // Execute this in the next round.
                                        {
                                            soonestPredictedEvent = Math.Min(timeUntilThisAllowed, soonestPredictedEvent);
                                            wantstoExecute = false;
                                        }
                                    }
                                }

                                if (!wantstoExecute) continue;

                                atLeastOneWantedExecution = true;

                                timeUntilNextRequestAllowed = whenCanIRequest(true);
                                if (timeUntilNextRequestAllowed == 0)
                                {
                                    CommandExecutingEventArgs args = new CommandExecutingEventArgs(requestQueue[i].command);
                                    OnCommandExecuting(args);
                                    if (args.Cancel) { // Allow the event receivers to cancel the execution for whatever reason and tell us when we can continue.
                                        somethingToDo = false;
                                        soonestPredictedEvent = Math.Max(soonestPredictedEvent, args.NextRequestAllowedIfCancelled);
                                    } else
                                    {
                                        lastRequestTimesOfType[requestQueue[i].type] = DateTime.Now;
                                        LogCommand(requestQueue[i]);
                                        requestQueue.RemoveAt(i);
                                        requestQueueChanged = true;
                                    }
                                } else
                                {
                                    somethingToDo = false;
                                    soonestPredictedEvent = Math.Max(soonestPredictedEvent, timeUntilNextRequestAllowed);
                                }
                                break;
                            }
                            priorityToMatch--;
                        }
                        if (!atLeastOneWantedExecution) somethingToDo = false;
                    }
                    if (requestQueueChanged) OnRequestListUpdated();
                }


            }
        }

        // The kind parameter is a user-defined request kind parameter to be able to group requests of a desired kind together and be able to specify the request behavior of that group (like ignore previous commands of the same type for choosing who to follow)
        // overriddenKinds defines kinds that the current request will override. If any requests of that kind exist, they will be deleted by adding this request.
        public async void requestExecution(TCommand command, TKind kind, int priority = 0,int minimumDelayFromSameType=0, RequestBehavior requestBehavior = RequestBehavior.ENQUEUE, TKind[] overriddenKinds = null)
        {
            Request request = new Request() {
                command = command,
                type = kind,
                minimumDelayFromSameType = minimumDelayFromSameType,
                priority = priority
            };
            bool requestQueueChanged = false;
            lock (requestQueue)
            {
                // Remove overridden kinds from queue
                if(overriddenKinds != null)
                {
                    foreach(TKind overriddenKind in overriddenKinds)
                    {
                        for (int i = requestQueue.Count - 1; i >= 0; i--)
                        {
                            if (requestQueue[i].type.CompareTo(overriddenKind) == 0)
                            {
                                requestQueue.RemoveAt(i);
                                requestQueueChanged = true;
                            }
                        }
                    }
                }

                // Actually do the adding
                switch (requestBehavior)
                {
                    case RequestBehavior.DISCARD_IF_ONE_OF_TYPE_ALREADY_EXISTS:
                        // Only add to list if another request of the same type doesn't already exist.
                        bool kindAlreadyExists = false;
                        foreach(Request tmpReq in requestQueue)
                        {
                            if(tmpReq.type.CompareTo(kind) == 0)
                            {
                                kindAlreadyExists = true;
                            }
                        }
                        if (!kindAlreadyExists)
                        {
                            requestQueue.Add(request);
                            requestQueueChanged = true;
                        }
                        break;
                    case RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE:
                        // If another request of the same type already exists, remove that previous request.
                        for(int i = requestQueue.Count - 1; i >= 0; i--)
                        {
                            if (requestQueue[i].type.CompareTo(kind) == 0)
                            {
                                requestQueue.RemoveAt(i);
                                requestQueueChanged = true;
                            }
                        }
                        requestQueue.Add(request);
                        requestQueueChanged = true;
                        break;
                    case RequestBehavior.ENQUEUE:
                    default:
                        requestQueue.Add(request);
                        requestQueueChanged = true;
                        break;
                }
            }
            if (requestQueueChanged) OnRequestListUpdated();
            //sleepInterruptor.Cancel(); /// TODO Fix error where this is already disposed and crashes the program.
            sleepInterrupter.CancelAll();
        }


        private bool canRequestNow(bool actuallyRequesting = false)
        {
            updateBucketInfo();

            if (thisBucketBurst < burst)
            {
                if (actuallyRequesting)
                {
                    thisBucketBurst++;
                }

                return true;
            }

            return false;
        }
        
        // Returns milliseconds until next request is allowed.
        private int whenCanIRequest(bool actuallyRequesting = false)
        {
            int interval = (int)(DateTime.Now - lastTime).TotalMilliseconds;
            int expired = interval / period;
            int expiredRemainder = interval % period;

            if (expired > thisBucketBurst || interval < 0)
            {
                thisBucketBurst = 0;
                lastTime = DateTime.Now;
            }
            else
            {
                thisBucketBurst -= expired;
                lastTime = DateTime.Now - new TimeSpan(0, 0, 0, 0, expiredRemainder);
            }

            if (thisBucketBurst < burst)
            {
                if (actuallyRequesting)
                {
                    thisBucketBurst++;
                }
                return 0; // Can request now.
            }

            return period - expiredRemainder;
        }

        private void updateBucketInfo()
        { 
            int interval = (int)(DateTime.Now - lastTime).TotalMilliseconds;
            int expired = interval / period;
            int expiredRemainder = interval % period;

            if (expired > thisBucketBurst || interval < 0)
            {
                thisBucketBurst = 0;
                lastTime = DateTime.Now;
            }
            else
            {
                thisBucketBurst -= expired;
                lastTime = DateTime.Now - new TimeSpan(0, 0, 0, 0, expiredRemainder);
            }   
        }

        public event EventHandler<CommandExecutingEventArgs> CommandExecuting;
        internal void OnCommandExecuting(CommandExecutingEventArgs eventArgs)
        {
            try {  // Want this to be robust and not result in killing the thread if eventhandlers have an issue.
                this.CommandExecuting?.Invoke(this, eventArgs);
            } catch (Exception e)
            {
                // Whatever
            }
        }
        public class CommandExecutingEventArgs : EventArgs
        {
            public TCommand Command { get; private set; }
            public bool Cancel = false; // If you want to cancel this one
            private int _nextRequestAllowedIfCancelled = 100; // We set 100 as default when we can continue if cancelled.
            public int NextRequestAllowedIfCancelled // If you cancel, tell us when we can continue pls.
            {
                get
                {
                    return _nextRequestAllowedIfCancelled;
                } 
                set
                {
                    _nextRequestAllowedIfCancelled = Math.Max(_nextRequestAllowedIfCancelled, value);
                }
            }
            public CommandExecutingEventArgs(TCommand commandA)
            {
                Command = commandA;
            }
        }
    }

}
