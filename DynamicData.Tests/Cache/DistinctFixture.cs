﻿
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    
    public class DistinctFixture
    {
        private ISourceCache<Person, string> _source;
        private DistinctChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void FiresAddWhenaNewItemIsAdded()
        {
            _source.AddOrUpdate(new Person("Person1", 20));

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().Should().Be(20, "Should 20");
        }

        [Test]
        public void FiresBatchResultOnce()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
            });

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(3, "Should be 3 items in the cache");

            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {20, 21, 22});
            _results.Data.Items.First().Should().Be(20, "Should 20");
        }

        [Test]
        public void DuplicatedResultsResultInNoAdditionalMessage()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person1", 20));
            });

            _results.Messages.Count.Should().Be(1, "Should be 1 update message");
            _results.Data.Count.Should().Be(1, "Should be 1 items in the cache");
            _results.Data.Items.First().Should().Be(20, "Should 20");
        }

        [Test]
        public void RemovingAnItemRemovesTheDistinct()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.Remove(new Person("Person1", 20));
            _results.Messages.Count.Should().Be(2, "Should be 1 update message");
            _results.Data.Count.Should().Be(0, "Should be 1 items in the cache");

            _results.Messages.First().Adds.Should().Be(1, "First message should be an add");
            _results.Messages.Skip(1).First().Removes.Should().Be(1, "Second messsage should be a remove");
        }

        [Test]
        public void BreakWithLoadsOfUpdates()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person2", 12));
                updater.AddOrUpdate(new Person("Person1", 1));
                updater.AddOrUpdate(new Person("Person1", 1));
                updater.AddOrUpdate(new Person("Person2", 12));


                updater.AddOrUpdate(new Person("Person3", 13));
                updater.AddOrUpdate(new Person("Person4", 14));
            });

            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {1, 12, 13, 14});

            //This previously threw
            _source.Remove(new Person("Person3", 13));

            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {1, 12, 14});
        }
    }
}