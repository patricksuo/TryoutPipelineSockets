using System;
using System.Diagnostics.Tracing;

namespace RuntimeTracing
{
    public class RuntimeEventListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            Console.WriteLine("OnEventSourceCreated {0} {1}", eventSource.Name, eventSource.Guid);
            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            base.OnEventWritten(eventData);
        }
    }
}
