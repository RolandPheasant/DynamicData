using System;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    
    public class BatchFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;
        private TestScheduler _scheduler;

        [SetUp]
        public void MyTestInitialize()
        {
            _scheduler = new TestScheduler();
            _source = new SourceCache<Person, string>(p => p.Key);
            _results = _source.Connect().Batch(TimeSpan.FromMinutes(1), _scheduler).AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void NoResultsWillBeReceivedBeforeClosingBuffer()
        {
            _source.AddOrUpdate(new Person("A", 1));
            _results.Messages.Count.Should().Be(0, "There should be no messages");
        }

        [Test]
        public void ResultsWillBeReceivedAfterClosingBuffer()
        {
            _source.AddOrUpdate(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
            _results.Messages.Count.Should().Be(1, "Should be 1 update");
        }
    }
}