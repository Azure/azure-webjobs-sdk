using Microsoft.Azure.WebJobs.Host.Abstractions;
using System;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class ActivitySourceWrapper : IActivitySourceAbstraction
    {
        private readonly ActivitySource _activitySource;

        public ActivitySourceWrapper(string sourceName)
        {
            _activitySource = new ActivitySource(sourceName);
        }

        public IActivityAbstraction? StartActivity(string name, ActivityKindAbstraction kind = ActivityKindAbstraction.Internal)
        {
            var activityKind = ConvertActivityKind(kind);
            var activity = _activitySource.StartActivity(name, activityKind);
            return activity != null ? new ActivityWrapper(activity) : null;
        }

        private static ActivityKind ConvertActivityKind(ActivityKindAbstraction kind)
        {
            return kind switch
            {
                ActivityKindAbstraction.Internal => ActivityKind.Internal,
                ActivityKindAbstraction.Server => ActivityKind.Server,
                ActivityKindAbstraction.Client => ActivityKind.Client,
                ActivityKindAbstraction.Producer => ActivityKind.Producer,
                ActivityKindAbstraction.Consumer => ActivityKind.Consumer,
                _ => ActivityKind.Internal
            };
        }
    }

    public class ActivityWrapper : IActivityAbstraction
    {
        private readonly Activity _activity;

        public ActivityWrapper(Activity activity)
        {
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        }

        public string? DisplayName
        {
            get => _activity.DisplayName;
            set => _activity.DisplayName = value;
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }
    }

    public class ActivityContextProvider : IActivityContextProvider
    {
        public bool HasCurrentActivity => Activity.Current != null;
    }
}