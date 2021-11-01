﻿using System;
using System.Collections.Generic;
using System.Threading;
using MVCore;
using MVCore.Utils;


namespace MVCore
{
    class Task
    {
        public ulong task_uid;
        public Thread thread;
        public ThreadRequest thread_request;
    }
    
    class WorkThreadDispacher : System.Timers.Timer
    {
        private List<Task> tasks = new List<Task>();
        private ulong taskGUIDCounter = 0;

        public WorkThreadDispacher()
        {
            Interval = 10; //10 ms
            Elapsed += queryTasks;
        }

        public Task sendRequest(ThreadRequest tr)
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

            //Create and start Thread
            Thread t = null;
            switch (tr.type)
            {
                case THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST:
                    string filepath = (string) tr.arguments[0];
                    string gameDir = (string) tr.arguments[1];
                    ResourceManager resMgr = (ResourceManager) tr.arguments[2];
                    MVCore.Common.CallBacks.Log("* Issuing PAK Loading Work Thread", MVCore.Common.LogVerbosityLevel.INFO);
                    t = new Thread(() => NMSUtils.loadNMSArchives(filepath, gameDir, ref resMgr, ref tk.thread_request.response));
                    break;
                default:
                    Console.WriteLine("");
                    MVCore.Common.CallBacks.Log("* DISPATCHER : Unsupported Thread Request", MVCore.Common.LogVerbosityLevel.WARNING);
                    break;
            }

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
                        tk.thread_request.status = THREAD_REQUEST_STATUS.FINISHED;
                    }
                    tasks.RemoveAt(i);
                    continue;
                }
                i++;
            }
        }

    }
}
