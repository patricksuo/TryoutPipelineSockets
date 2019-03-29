using System;
using System.Diagnostics.Tracing;
using System.Text;
using System.Threading;

namespace RuntimeTracing
{
    public class RuntimeEventListener : EventListener
    {
        public Action ThreadPoolWorkerThreadWait;
        public DateTime ThreadPoolWorkerThreadWaitFireTime = DateTime.UtcNow;
        public readonly TimeSpan ThreadPoolWorkerThreadWaitCD = TimeSpan.FromMilliseconds(10);


        public long EnqueueCnt;
        public long DequeueCnt;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            Console.WriteLine("OnEventSourceCreated {0} {1}", eventSource.Name, eventSource.Guid);
            if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
            {
                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)0x10000);
                return;
            }
            if (eventSource.Name == "System.Diagnostics.Eventing.FrameworkEventSource")
            {
                EnableEvents(eventSource, EventLevel.LogAlways, (EventKeywords)0x0002);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == 30)
            {
                Interlocked.Increment(ref EnqueueCnt);
                return;
            }
            if (eventData.EventId == 31)
            {
                Interlocked.Increment(ref DequeueCnt);
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append((long)(eventData.TimeStamp -DateTimeOffset.UnixEpoch).TotalSeconds);
            sb.Append(" ");
            sb.Append(eventData.EventName);
            sb.Append(" ");
            int i = 0;
            foreach(var payloadname in eventData.PayloadNames)
            {
                sb.Append(payloadname);
                sb.Append(":");
                sb.Append(eventData.Payload[i]);
                sb.Append(" ");
                i++;
            }

            sb.Append((long)(eventData.TimeStamp - DateTimeOffset.UnixEpoch).TotalMilliseconds);
            Console.WriteLine(sb.ToString());

            if (eventData.EventId == 57)
            {
                DateTime now = DateTime.UtcNow;
                if (now - ThreadPoolWorkerThreadWaitFireTime > ThreadPoolWorkerThreadWaitCD)
                {
                    ThreadPoolWorkerThreadWaitFireTime = now;
                    Action cb = ThreadPoolWorkerThreadWait;
                    cb?.Invoke();
                }

            }
        }
    }
}
