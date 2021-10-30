using System;
using System.Collections.Generic;
using System.Threading;
using MVCore;
using MVCore.Utils;


namespace MVCore
{
    public class Task
    {
        public ulong task_uid;
        public Thread thread;
        public ThreadRequest thread_request;
    }
    
    public class WorkThreadDispacher : System.Timers.Timer
    {
        private List<Task> tasks = new List<Task>();
        private ulong taskGUIDCounter = 0;

        public WorkThreadDispacher()
        {
            Interval = 10; //10 ms
            Elapsed += queryTasks;
        }

        public Task sendRequest(ref ThreadRequest tr)
        {
            Task t = createTask(tr);
            tasks.Add(t);

            return t;
        }

        private Task createTask(ThreadRequest tr)
        {
            Task tk = new Task();
            tk.task_uid = taskGUIDCounter;
            tk.thread_request = tr;

            lock (tr)
            {
                tr.Status = THREAD_REQUEST_STATUS.ACTIVE;
            }

            //Create and start Thread
            Thread t = null;

            t = new Thread(() => tr.Method.Invoke(null, (object?[]) tr.Data));
            Common.Callbacks.Log("* Issuing Requested Method", Common.LogVerbosityLevel.INFO);

            tk.thread = t;
            tk.thread.IsBackground = true;
            tk.thread.Start();
            
            return tk;
        }



        private void queryTasks(object sender, System.Timers.ElapsedEventArgs e)
        {
            int i = 0;
            while(i < tasks.Count)
            {
                Task tk = tasks[i];

                //Check if task has finished 
                if (!tk.thread.IsAlive)
                {
                    lock (tk.thread_request)
                    {
                        tk.thread_request.Status = THREAD_REQUEST_STATUS.FINISHED;
                    }
                    tasks.RemoveAt(i);
                    continue;
                }
                i++;
            }
        }

    }
}
