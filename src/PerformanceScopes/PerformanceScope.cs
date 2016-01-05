using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceScopes
{
    public class PerformanceScope
    {
        public static bool IsEnabled { get; set; }
        private readonly PerformanceScope _parent;
        private PerformanceScope[] _subScopesArray;
        private readonly ConcurrentBag<PerformanceScope> _subProfilers;
        private readonly ConcurrentDictionary<string, long> _groupedProfilers;

        public PerformanceScope(string name, PerformanceScope parent)
        {
            Name = name;
            StartedOn = DateTime.Now;
            _parent = parent;
            Elapsed = TimeSpan.Zero;
            _subProfilers = new ConcurrentBag<PerformanceScope>();
            _groupedProfilers = new ConcurrentDictionary<string, long>();
            if (parent != null)
            {
                parent.AddSubProfiler(this);
            }
        }

        public string Name { get; private set; }
        public DateTime StartedOn { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public Dictionary<string, TimeSpan> SubGroups { get { return _groupedProfilers.ToDictionary(x => x.Key, x => new TimeSpan(x.Value)); } }

        public override string ToString()
        {
            return string.Format("{0} ({1}ms)", Name, Elapsed.Milliseconds);
        }

        public PerformanceScope[] Scopes
        {
            get { return _subScopesArray ?? (_subScopesArray = _subProfilers.OrderBy(x => x.StartedOn).ToArray()); }
        }
        private void AddSubProfiler(PerformanceScope scope)
        {
            _subScopesArray = null;
            _subProfilers.Add(scope);
        }
        public interface IPerformanceScopeWrapper : IDisposable
        {
            PerformanceScope Scope { get; }
        }

        class ProfilerDisabled : IPerformanceScopeWrapper
        {
            public void Dispose()
            {
            }

            public PerformanceScope Scope
            {
                get { return null; }
            }
        }
        class PerformanceScopeStopWatch : IPerformanceScopeWrapper
        {
            private Stopwatch _sw;
            private bool _disposing;

            public PerformanceScopeStopWatch(PerformanceScope scope)
            {
                Scope = scope;
                _sw = new Stopwatch();
                _sw.Start();
            }

            public PerformanceScope Scope { get; private set; }

            public void Dispose()
            {
                if (!_disposing)
                {
                    _disposing = true;
                    _sw.Stop();
                    Scope.Elapsed += _sw.Elapsed;

                    if (Scope._parent == null)
                    {
                        CallContext.FreeNamedDataSlot(Key);
                    }
                    else
                    {
                        CallContext.LogicalSetData(Key, Scope._parent);
                    }
                }
            }
        }

        class PerformanceScopeGroupStopWatch : IPerformanceScopeWrapper
        {
            private readonly string _group;
            private Stopwatch _sw;
            private bool _disposing;

            public PerformanceScopeGroupStopWatch(PerformanceScope scope, string group)
            {
                _group = @group;
                Scope = scope;
                _sw = new Stopwatch();
                _sw.Start();
            }

            public PerformanceScope Scope { get; private set; }

            public void Dispose()
            {
                if (!_disposing)
                {
                    _disposing = true;
                    _sw.Stop();
                    Scope._groupedProfilers.AddOrUpdate(_group, _sw.Elapsed.Ticks, (key, current) => current + _sw.Elapsed.Ticks);
                }
            }
        }
        private const string Key = "_PerformanceScopes";

        public static IPerformanceScopeWrapper Create(string name, params object[] args)
        {
            if (!IsEnabled)
            {
                return new ProfilerDisabled();
            }
            var data = CallContext.LogicalGetData(Key);
            var parent = data as PerformanceScope;
            var profiler = new PerformanceScope(string.Format(name, args), parent);
            CallContext.LogicalSetData(Key, profiler);
            return new PerformanceScopeStopWatch(profiler);
        }

        public static IPerformanceScopeWrapper Append(string group)
        {
            if (!IsEnabled)
            {
                return new ProfilerDisabled();
            }
            var data = CallContext.LogicalGetData(Key);
            var parent = data as PerformanceScope;
            if (parent != null)
            {
                return new PerformanceScopeGroupStopWatch(parent, group);
            }
            else
            {
                return new ProfilerDisabled();
            }
        }
    }
}
