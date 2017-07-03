using System;
using System.Linq;
using DynamicData.Controllers;
using DynamicData.Tests.Domain;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.Cache
{
    
    public class GroupControllerForFilteredItemsFixture
    {
        private enum AgeBracket
        {
            Under20,
            Adult,
            Pensioner
        }

        private readonly Func<Person, AgeBracket> _grouper = p =>
        {
            if (p.Age <= 19) return AgeBracket.Under20;
            return p.Age <= 60 ? AgeBracket.Adult : AgeBracket.Pensioner;
        };

        private ISourceCache<Person, string> _source;
        private GroupController _controller;
        private IObservableCache<IGroup<Person, string, AgeBracket>, AgeBracket> _grouped;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _controller = new GroupController();
            _grouped = _source.Connect(p => _grouper(p) != AgeBracket.Pensioner)
                              .Group(_grouper, _controller).AsObservableCache();
        }

        [Test]
        public void RegroupRecaluatesGroupings()
        {
            var p1 = new Person("P1", 10);
            var p2 = new Person("P2", 15);
            var p3 = new Person("P3", 30);
            var p4 = new Person("P4", 70);
            var people = new[] { p1, p2, p3, p4 };

            _source.AddOrUpdate(people);

            IsContainedIn("P1", AgeBracket.Under20).Should().BeTrue();
            IsContainedIn("P2", AgeBracket.Under20).Should().BeTrue();
            IsContainedIn("P3", AgeBracket.Adult).Should().BeTrue();

            p1.Age = 60;
            p2.Age = 80;
            p3.Age = 15;
            p4.Age = 30;

            _controller.RefreshGroup();

            IsContainedIn("P1", AgeBracket.Adult).Should().BeTrue();
            IsContainedIn("P3", AgeBracket.Under20).Should().BeTrue();

            IsContainedOnlyInOneGroup("P1").Should().BeTrue();
            IsContainedOnlyInOneGroup("P2").Should().BeTrue();
        }

        [Test]
        public void RegroupRecaluatesGroupings2()
        {
            var p1 = new Person("P1", 10);
            var p2 = new Person("P2", 15);
            var p3 = new Person("P3", 30);
            var p4 = new Person("P4", 70);
            var people = new[] {p1, p2, p3, p4};

            _source.AddOrUpdate(people);

            IsContainedIn("P1", AgeBracket.Under20).Should().BeTrue();
            IsContainedIn("P2", AgeBracket.Under20).Should().BeTrue();
            IsContainedIn("P3", AgeBracket.Adult).Should().BeTrue();
            IsContainedIn("P4", AgeBracket.Pensioner).Should().BeFalse();

            p1.Age = 60;
            p2.Age = 80;
            p3.Age = 15;
            p4.Age = 30;

            // _controller.RefreshGroup();

            _source.Refresh(new[] {p1, p2, p3, p4});

            IsContainedIn("P1", AgeBracket.Adult).Should().BeTrue();
            IsContainedIn("P2", AgeBracket.Pensioner).Should().BeFalse();
            IsContainedIn("P3", AgeBracket.Under20).Should().BeTrue();
            IsContainedIn("P4", AgeBracket.Adult).Should().BeTrue();

            IsContainedOnlyInOneGroup("P1").Should().BeTrue();
            IsNotContainedAnyWhere("P2").Should().BeTrue();
            IsContainedOnlyInOneGroup("P3").Should().BeTrue();
            IsContainedOnlyInOneGroup("P4").Should().BeTrue();
        }

        private bool IsContainedIn(string name, AgeBracket bracket)
        {
            var group = _grouped.Lookup(bracket);
            if (!group.HasValue) return false;

            return group.Value.Cache.Lookup(name).HasValue;
        }

        private bool IsContainedOnlyInOneGroup(string name)
        {
            var person = _grouped.Items.SelectMany(g => g.Cache.Items.Where(s => s.Name == name)).ToList();

            return person.Count == 1;
        }

        private bool IsNotContainedAnyWhere(string name)
        {
            var person = _grouped.Items.SelectMany(g => g.Cache.Items.Where(s => s.Name == name)).ToList();

            return person.Count == 0;
        }
    }
}