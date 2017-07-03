using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    
    public class FilterControllerFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;
        private FilterController<Person> _filter;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Key);
            _filter = new FilterController<Person>(p => p.Age > 20);
            _results = new ChangeSetAggregator<Person, string>(_source.Connect(_filter));
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void ChangeFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

            _source.AddOrUpdate(people);
            _results.Data.Count.Should().Be(80, "Should be 80 people in the cache");

            _filter.Change(p => p.Age <= 50);
            _results.Data.Count.Should().Be(50, "Should be 50 people in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 update messages");
            _results.Messages[1].Removes.Should().Be(50, "Should be 50 removes in the second message");
            _results.Messages[1].Adds.Should().Be(20, "Should be 20 adds in the second message");

            _results.Data.Items.All(p => p.Age <= 50).Should().BeTrue();
        }

        [Test]
        public void RepeatedApply()
        {
            using (var source = new SourceCache<Person, string>(p => p.Key))
            {
                source.AddOrUpdate(Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray());

                var subject = new ReplaySubject<Func<Person, bool>>();
                subject.OnNext(x => true);

                IChangeSet<Person, string> latestChanges = null;
                using (source.Connect().Filter(subject).Do(changes => latestChanges = changes).AsObservableCache())
                {
                    subject.OnNext(p => false);
                    latestChanges.Removes.Should().Be(100);
                    latestChanges.Adds.Should().Be(0);

                    subject.OnNext(p => true);
                    latestChanges.Adds.Should().Be(100);
                    latestChanges.Removes.Should().Be(0);

                    subject.OnNext(p => false);
                    latestChanges.Removes.Should().Be(100);
                    latestChanges.Adds.Should().Be(0);

                    subject.OnNext(p => true);
                    latestChanges.Adds.Should().Be(100);
                    latestChanges.Removes.Should().Be(0);

                    subject.OnNext(p => false);
                    latestChanges.Removes.Should().Be(100);
                    latestChanges.Adds.Should().Be(0);
                }
            }
        }

        [Test]
        public void ReevaluateFilter()
        {
            //re-evaluate for inline changes
            var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

            _source.AddOrUpdate(people);
            _results.Data.Count.Should().Be(80, "Should be 80 people in the cache");

            foreach (var person in people)
            {
                person.Age = person.Age + 10;
            }
            _filter.Reevaluate();

            _results.Data.Count.Should().Be(90, "Should be 90 people in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 update messages");
            _results.Messages[1].Adds.Should().Be(10, "Should be 10 adds in the second message");

            foreach (var person in people)
            {
                person.Age = person.Age - 10;
            }
            _filter.Reevaluate();

            _results.Data.Count.Should().Be(80, "Should be 80 people in the cache");
            _results.Messages.Count.Should().Be(3, "Should be 3 update messages");
            _results.Messages[2].Removes.Should().Be(10, "Should be 10 removes in the third message");
        }


        #region Static filter tests

        /* Should be the same as standard lambda filter */

        [Test]
        public void AddMatched()
        {
            var person = new Person("Adult1", 50);
            _source.AddOrUpdate(person);

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().Should().Be(person, "Should be same person");
        }

        [Test]
        public void AddNotMatched()
        {
            var person = new Person("Adult1", 10);
            _source.AddOrUpdate(person);

            _results.Messages.Count.Should().Be(0, "Should have no item updates");
            _results.Data.Count.Should().Be(0, "Cache should have no items");
        }

        [Test]
        public void AddNotMatchedAndUpdateMatched()
        {
            const string key = "Adult1";
            var notmatched = new Person(key, 19);
            var matched = new Person(key, 21);

            _source.Edit(innerCache =>
            {
                innerCache.AddOrUpdate(notmatched);
                innerCache.AddOrUpdate(matched);
            });

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Messages[0].First().Current.Should().Be(matched, "Should be same person");
            _results.Data.Items.First().Should().Be(matched, "Should be same person");
        }

        [Test]
        public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
        {
            const string key = "Adult1";
            _source.Remove(key);
            _results.Messages.Count.Should().Be(0, "Should be 0 updates");
        }

        [Test]
        public void BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            _source.AddOrUpdate(people);
            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Messages[0].Adds.Should().Be(80, "Should return 80 adds");

            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            _results.Data.Items.OrderBy(p => p.Age).ShouldAllBeEquivalentTo(_results.Data.Items.OrderBy(p => p.Age), "Incorrect Filter result");
        }

        [Test]
        public void BatchRemoves()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

            _source.AddOrUpdate(people);
            _source.Remove(people);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(80, "Should be 80 addes");
            _results.Messages[1].Removes.Should().Be(80, "Should be 80 removes");
            _results.Data.Count.Should().Be(0, "Should be nothing cached");
        }

        [Test]
        public void BatchSuccessiveUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
            foreach (var person in people)
            {
                Person person1 = person;
                _source.AddOrUpdate(person1);
            }

            _results.Messages.Count.Should().Be(80, "Should be 80 messages");
            _results.Data.Count.Should().Be(80, "Should be 80 in the cache");
            var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
            _results.Data.Items.OrderBy(p => p.Age).ShouldAllBeEquivalentTo(_results.Data.Items.OrderBy(p => p.Age), "Incorrect Filter result");
        }

        [Test]
        public void Clear()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
            _source.AddOrUpdate(people);
            _source.Clear();

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(80, "Should be 80 addes");
            _results.Messages[1].Removes.Should().Be(80, "Should be 80 removes");
            _results.Data.Count.Should().Be(0, "Should be nothing cached");
        }

        [Test]
        public void Remove()
        {
            const string key = "Adult1";
            var person = new Person(key, 50);

            _source.AddOrUpdate(person);
            _source.Remove(key);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
            _results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
            _results.Data.Count.Should().Be(0, "Should be nothing cached");
        }

        [Test]
        public void UpdateMatched()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);

            _source.AddOrUpdate(newperson);
            _source.AddOrUpdate(updated);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
            _results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
        }

        [Test]
        public void SameKeyChanges()
        {
            const string key = "Adult1";

            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person(key, 50));
                updater.AddOrUpdate(new Person(key, 52));
                updater.AddOrUpdate(new Person(key, 53));
                updater.Remove(key);
            });

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
            _results.Messages[0].Updates.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Removes.Should().Be(1, "Should be 1 remove");
        }

        [Test]
        public void UpdateNotMatched()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 10);
            var updated = new Person(key, 11);

            _source.AddOrUpdate(newperson);
            _source.AddOrUpdate(updated);

            _results.Messages.Count.Should().Be(0, "Should be no updates");
            _results.Data.Count.Should().Be(0, "Should nothing cached");
        }

        #endregion
    }
}