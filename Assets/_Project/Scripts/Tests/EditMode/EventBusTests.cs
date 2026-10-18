using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RivetSim.Core.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// EventBus 测试：发布、取消订阅、订阅者异常隔离。
    /// </summary>
    public sealed class EventBusTests
    {
        private readonly struct TestEvent : ISimEvent
        {
            public readonly int Value;
            public TestEvent(int value) => Value = value;
        }

        [Test]
        public void Publish_DeliversToSubscriber()
        {
            var bus = new EventBus();
            int received = 0;
            bus.Subscribe<TestEvent>(e => received = e.Value);
            bus.Publish(new TestEvent(7));
            Assert.That(received, Is.EqualTo(7));
        }

        [Test]
        public void DisposeSubscription_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            IDisposable subscription = bus.Subscribe<TestEvent>(_ => count++);
            bus.Publish(new TestEvent(1));
            subscription.Dispose();
            bus.Publish(new TestEvent(2));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(bus.SubscriberCount<TestEvent>(), Is.EqualTo(0));
        }

        [Test]
        public void ThrowingSubscriber_DoesNotBlockOthers()
        {
            var bus = new EventBus();
            bool secondCalled = false;
            bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("测试异常"));
            bus.Subscribe<TestEvent>(_ => secondCalled = true);

            LogAssert.Expect(LogType.Error, new Regex("测试异常"));
            bus.Publish(new TestEvent(0));
            Assert.That(secondCalled, Is.True);
        }
    }
}
