using System;
using System.Linq;
using DynamicData.Tests;
using ReactiveUI.Legacy;
using Xunit;
using FluentAssertions;

#pragma warning disable CS0618 // Using legacy code.

namespace DynamicData.ReactiveUI.Tests.Fixtures
{
	
    public class ToObservableChangeSetFixture : IDisposable
    {
        private readonly ReactiveList<int> _collection;
        private readonly ChangeSetAggregator<int> _results;

        public ToObservableChangeSetFixture()
        {
            _collection = new ReactiveList<int>();
            _results = _collection.ToObservableChangeSet().AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
        }

        [Fact]
        public void Move()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);
            _collection.Move(5, 8);
            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);

            _collection.Move(7, 1);
            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);
        }

        [Fact]
        public void Add()
        {
            _collection.Add(1);

            _results.Messages.Count.Should().Be(1);
            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Fact]
        public void Remove()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _collection.Remove(3);

            _results.Data.Count.Should().Be(9);
            _results.Data.Items.Contains(3).Should().BeFalse();
            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);
        }

        [Fact]
        public void Duplicates()
        {
            _collection.Add(1);
            _collection.Add(1);

            _results.Data.Count.Should().Be(2);
        }

        [Fact]
        public void Replace()
        {
            _collection.AddRange(Enumerable.Range(1, 10));
            _collection[8] = 20;

            _results.Data.Items.ShouldBeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 20, 10 });

        }

        [Fact]
        public void ResetFiresClearsAndAdds()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _collection.Reset();
            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);

            var resetNotification = _results.Messages.Last();
            resetNotification.Removes.Should().Be(10);
            resetNotification.Adds.Should().Be(10);
        }


    }
}