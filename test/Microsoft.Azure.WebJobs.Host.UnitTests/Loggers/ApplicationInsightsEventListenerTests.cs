// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class ApplicationInsightsEventListenerTests : IDisposable
    {
        private readonly ApplicationInsightsEventListener listener = new ApplicationInsightsEventListener(System.Diagnostics.Tracing.EventLevel.Informational);
        CoreEventSource log = new CoreEventSource();
        public void Dispose()
        {
            listener.Dispose();
            log.Dispose();            
        }

        [Fact]
        public async Task TestEventHandling()
        {
            DiagnosticListener.AllListeners.Subscribe(new TestDiagnosticObserver());
            for (int i = 0; i < 40; i++) 
            {
                log.WarningEvent("Logging warning event");
                log.VerboseEvent("Logging verbose event");            
            }
            await Task.Delay(100);
            Assert.Equal(ValidateEvent.Assert, true);
        }
    }

    [EventSource(Name = "Microsoft-ApplicationInsights-Core")]
    internal sealed class CoreEventSource : EventSource
    {
        public static readonly CoreEventSource Log = new CoreEventSource();
        
        [Event(1, Message = "WarningMessage", Level = System.Diagnostics.Tracing.EventLevel.Warning)]
        public void WarningEvent(string message)
        {
            this.WriteEvent(1, message);
        }

        [Event(2, Message = "VerboseMessage", Level = System.Diagnostics.Tracing.EventLevel.Verbose)]
        public void VerboseEvent(string message)
        {
            this.WriteEvent(2, message);
        }
    }

    public class TestDiagnosticObserver : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.Azure.Functions.Host.ApplicationInsightsEventListener")
            {
                value.Subscribe(new TestKeyValueObserver());
            }
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    public class TestKeyValueObserver : IObserver<KeyValuePair<string, object>>
    {
        public void OnNext(KeyValuePair<string, object> value)
        {
            ValidateEvent.Assert = ValidateEvent.Validate(value);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    public static class ValidateEvent
    {
        public static bool Assert = false; 

        public static bool Validate(KeyValuePair<string, object> value)
        {
            // Validate event listener
            if (value.Key != "ApplicationInsightsEventListener")
            {
                return false;
            }

            // message is not null
            if (value.Value == null) {
                return false;
            }

            // verbose messages are filtered out
            if (value.Value.ToString().Contains("VerboseMessage"))
            {
                return false;
            }

            // validate batch size
            if (CountMessage(value.Value.ToString(), "WarningMessage") != 30)
            {
                return false;
            }
            return true;
        }

        static int CountMessage(string text, string substring)
        {
            int count = 0;
            int index = text.IndexOf(substring);
            while (index != -1)
            {
                count++;
                index = text.IndexOf(substring, index + 1);
            }
            return count;
        }
    }
}
