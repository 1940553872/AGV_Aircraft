using System;
using System.Collections.Generic;
using RivetSim.Core.Logging;

namespace RivetSim.Core.Events
{
    /// <summary>
    /// 强类型事件总线：按事件类型发布 / 订阅。单个订阅者抛出异常时记录错误日志，不影响其他订阅者。
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        /// <summary>订阅事件，返回的对象 Dispose 时取消订阅。</summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, ISimEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.TryGetValue(typeof(T), out Delegate existing);
            _handlers[typeof(T)] = Delegate.Combine(existing, handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        /// <summary>取消订阅；未订阅时无操作。</summary>
        public void Unsubscribe<T>(Action<T> handler) where T : struct, ISimEvent
        {
            if (handler == null || !_handlers.TryGetValue(typeof(T), out Delegate existing)) return;
            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = remaining;
        }

        /// <summary>发布事件，同步调用全部订阅者。</summary>
        public void Publish<T>(in T evt) where T : struct, ISimEvent
        {
            if (!_handlers.TryGetValue(typeof(T), out Delegate existing)) return;

            foreach (Delegate d in existing.GetInvocationList())
            {
                try
                {
                    ((Action<T>)d).Invoke(evt);
                }
                catch (Exception ex)
                {
                    SimLog.Error(LogCategory.System, $"事件 {typeof(T).Name} 的订阅者抛出异常：{ex}");
                }
            }
        }

        /// <summary>某类事件当前的订阅者数量。</summary>
        public int SubscriberCount<T>() where T : struct, ISimEvent
        {
            return _handlers.TryGetValue(typeof(T), out Delegate existing) ? existing.GetInvocationList().Length : 0;
        }

        /// <summary>清除全部订阅。</summary>
        public void Clear() => _handlers.Clear();

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
