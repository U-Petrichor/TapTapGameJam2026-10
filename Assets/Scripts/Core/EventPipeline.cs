using System;
using System.Collections.Generic;
namespace TapTapGameJam.Events
{
    /// <summary>
    /// 全局事件总线
    /// </summary>
    public static class EventPipeline
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();
        public static void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            Type type = typeof(T);

            if (!_handlers.ContainsKey(type))
            {
                // 如果不存在
                _handlers[type] = null;
            }

            _handlers[type] = Delegate.Combine(_handlers[type], handler);
        }

        public static void UnSubscribe<T>(Action<T> handler) where T : IEvent
        {
            Type type = typeof(T);

            if (_handlers.ContainsKey(type))
            {
                // 如果存在，就把这个委托取消订阅
                _handlers[type] = Delegate.Remove(_handlers[type], handler);
            }
        }

        public static void Publish<T>(T eventData) where T : IEvent
        {
            Type type = typeof(T);
            // 尝试获取，如果存在则获取委托列表
            // 如果委托列表非空，就执行
            if (_handlers.TryGetValue(type, out var del) && del != null)
            {
                ((Action<T>)del)?.Invoke(eventData);
            }
        }

        public static void ClearAll()
        {
            _handlers.Clear();
        }

    }
}
