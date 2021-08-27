using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class RequestHandler
    {
        private Queue<ThreadRequest> req_queue;
        
        public RequestHandler()
        {
            req_queue = new Queue<ThreadRequest>();
        }

        public virtual void AddRequest(ref ThreadRequest req)
        {
            lock (req_queue)
            {
                req_queue.Enqueue(req);
            }
        }

        public bool HasOpenRequests()
        {
            return req_queue.Count > 0;
        }

        public int GetOpenRequestNum()
        {
            return req_queue.Count;
        }

        public ThreadRequest Peek()
        {
            lock (req_queue)
            {
                return req_queue.Peek();
            }
        }

        public ThreadRequest Fetch()
        {
            lock (req_queue)
            {
                return req_queue.Dequeue();
            }
        }

    }
}
