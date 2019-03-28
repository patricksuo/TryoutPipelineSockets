using System;
using System.Diagnostics.Tracing;
using System.Text;

namespace RuntimeTracing
{
    public class RuntimeEventListener : EventListener
    {
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
            Console.WriteLine(sb.ToString());
        }
    }
}
