using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JKWatcher
{
    class LeakyBucketRequester<TCommand,TKind> where TKind:IComparable
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

        private struct Request {
            public TCommand command;
            public TKind type; // custom defined by caller
            public int minimumDelayFromSameType;
            public int priority;
        }

        List<Request> requestQueue = new List<Request>();
        Dictionary<TKind, DateTime> lastRequestTimesOfType = new Dictionary<TKind, DateTime>();

        private CancellationTokenSource cts = null;

        public LeakyBucketRequester(int burstA, int periodA)
        {
            burst = burstA;
            period = periodA;

            cts = new CancellationTokenSource();
            Task.Factory.StartNew(()=> { this.Run(cts.Token); }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void changeParameters(int burstA, int periodA)
        {
            burst = burstA;
            period = periodA;
        }

        ~LeakyBucketRequester()
        {
            Stop();
            sleepInterruptor.Dispose();
            sleepInterruptor = null;
        }

        public void Stop()
        {
            cts.Cancel();
            sleepInterruptor.Cancel();
            CancellationTokenSource tmpCts = sleepInterruptor;
            sleepInterruptor = new CancellationTokenSource();
            tmpCts.Dispose();
            cts.Dispose();
            cts = null;
        }

        CancellationTokenSource sleepInterruptor = new CancellationTokenSource();

        const int defaultTimeOut = 1100;
        const int safetyPadding = 10;

        private async void Run(CancellationToken ct)
        {
            int soonestPredictedEvent = defaultTimeOut;
            while (true)
            {
                CancellationTokenSource tmpCts = sleepInterruptor;
                sleepInterruptor = new CancellationTokenSource();
                tmpCts.Dispose();
                try { 
                    await Task.Delay(soonestPredictedEvent, sleepInterruptor.Token);
                } catch (TaskCanceledException e)
                {
                    // Do nothing, it's expected.
                }
                ct.ThrowIfCancellationRequested();
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
                                    OnCommandExecuting(new CommandExecutingEventArgs(requestQueue[i].command));
                                    lastRequestTimesOfType[requestQueue[i].type] = DateTime.Now;
                                    requestQueue.RemoveAt(i);
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
                        }
                        break;
                    case RequestBehavior.DELETE_PREVIOUS_OF_SAME_TYPE:
                        // If another request of the same type already exists, remove that previous request.
                        for(int i = requestQueue.Count - 1; i >= 0; i--)
                        {
                            if (requestQueue[i].type.CompareTo(kind) == 0)
                            {
                                requestQueue.RemoveAt(i);
                            }
                        }
                        requestQueue.Add(request);
                        break;
                    case RequestBehavior.ENQUEUE:
                    default:
                        requestQueue.Add(request);
                        break;
                }
            }
            sleepInterruptor.Cancel();
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
            public CommandExecutingEventArgs(TCommand commandA)
            {
                Command = commandA;
            }
        }
    }

}
