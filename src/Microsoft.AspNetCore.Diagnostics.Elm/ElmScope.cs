using System;
#if NET451
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
#else
using System.Threading;
#endif

namespace Microsoft.AspNetCore.Diagnostics.Elm
{
    public class ElmScope
    {
        private readonly string _name;
        private readonly object _state;

        public ElmScope(string name, object state)
        {
            _name = name;
            _state = state;
        }

        public ActivityContext Context { get; set; }

        public ElmScope Parent { get; set; }

        public ScopeNode Node { get; set; }

#if NET451
        private static string FieldKey = typeof(ElmScope).FullName + ".Value";
        public static ElmScope Current
        {
            get
            {
                var handle = CallContext.LogicalGetData(FieldKey) as ObjectHandle;

                if (handle == null)
                {
                    return default(ElmScope);
                }

                return (ElmScope)handle.Unwrap();
            }
            set
            {
                CallContext.LogicalSetData(FieldKey, new ObjectHandle(value));
            }
        }
#else
        private static AsyncLocal<ElmScope> _value = new AsyncLocal<ElmScope>();
        public static ElmScope Current
        {
            set
            {
                _value.Value = value;
            }
            get
            {
                return _value.Value;
            }
        }
#endif

        public static IDisposable Push(ElmScope scope, ElmStore store)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            var temp = Current;
            Current = scope;
            Current.Parent = temp;

            Current.Node = new ScopeNode()
            {
                StartTime = DateTimeOffset.UtcNow,
                State = Current._state,
                Name = Current._name
            };

            if (Current.Parent != null)
            {
                Current.Node.Parent = Current.Parent.Node;
                Current.Parent.Node.Children.Add(Current.Node);
            }
            else
            {
                Current.Context.Root = Current.Node;
                store.AddActivity(Current.Context);
            }

            return new DisposableAction(() =>
            {
                Current.Node.EndTime = DateTimeOffset.UtcNow;
                Current = Current.Parent;
            });
        }

        private class DisposableAction : IDisposable
        {
            private Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                if (_action != null)
                {
                    _action.Invoke();
                    _action = null;
                }
            }
        }
    }
}
