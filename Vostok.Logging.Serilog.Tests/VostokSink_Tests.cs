using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Values;
using Vostok.Logging.Console;
using LogEvent = Vostok.Logging.Abstractions.LogEvent;
using SerilogEvent = Serilog.Events.LogEvent;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace Vostok.Logging.Serilog
{
    [TestFixture]
    internal class VostokSink_Tests
    {
        private ILog vostokLog;
        private ILogger serilogLogger;
        private List<LogEvent> observedEvents;

        [SetUp]
        public void TestSetup()
        {
            observedEvents = new List<LogEvent>();

            vostokLog = Substitute.For<ILog>();
            vostokLog
                .When(l => l.Log(Arg.Any<LogEvent>()))
                .Do(info => observedEvents.Add(info.Arg<LogEvent>()));

            vostokLog = new CompositeLog(vostokLog, new SynchronousConsoleLog());

            serilogLogger = new LoggerConfiguration()
                .WriteTo.Sink(new VostokSink(vostokLog), LogEventLevel.Verbose)
                .MinimumLevel.Verbose()
                .CreateLogger();
        }

        // TODO(iloktionov): source context

        [TestCase(SerilogLevel.Verbose, LogLevel.Debug)]
        [TestCase(SerilogLevel.Debug, LogLevel.Debug)]
        [TestCase(SerilogLevel.Information, LogLevel.Info)]
        [TestCase(SerilogLevel.Warning, LogLevel.Warn)]
        [TestCase(SerilogLevel.Error, LogLevel.Error)]
        [TestCase(SerilogLevel.Fatal, LogLevel.Fatal)]
        public void Should_copy_primary_simple_components_of_log_events(SerilogLevel serilogLevel, LogLevel vostokLevel)
        {
            var serilogEvent = new SerilogEvent(
                DateTimeOffset.Now,
                serilogLevel,
                new Exception("I have failed.."),
                new MessageTemplate("Hello!", Enumerable.Empty<MessageTemplateToken>()),
                Enumerable.Empty<LogEventProperty>());

            serilogLogger.Write(serilogEvent);

            var vostokEvent = observedEvents.Single();

            vostokEvent.Level.Should().Be(vostokLevel);
            vostokEvent.Timestamp.Should().Be(serilogEvent.Timestamp);
            vostokEvent.MessageTemplate.Should().Be("Hello!");
            vostokEvent.Exception.Should().BeSameAs(serilogEvent.Exception);
            vostokEvent.Properties.Should().BeEmpty();
        }

        [Test]
        public void Should_provide_properties_for_message_template_placeholders()
        {
            serilogLogger.Information("Hello, {who}!", "world");

            observedEvents.Single().MessageTemplate.Should().Be("Hello, {who}!");
            observedEvents.Single().Properties?["who"].Should().Be("world");
        }

        [Test]
        public void Should_copy_properties_not_present_in_message_template()
        {
            var serilogEvent = new SerilogEvent(DateTimeOffset.Now, SerilogLevel.Information, null,
                new MessageTemplate("Hello!", Enumerable.Empty<MessageTemplateToken>()),
                new [] {new LogEventProperty("A", new ScalarValue(1)), new LogEventProperty("B", new ScalarValue(2)) });

            serilogLogger.Write(serilogEvent);

            var vostokEvent = observedEvents.Single();

            vostokEvent.Properties?["A"].Should().Be(1);
            vostokEvent.Properties?["B"].Should().Be(2);
        }

        [Test]
        public void Should_unwrap_scalar_properties()
        {
            serilogLogger.Information("Props = {str} {int} {guid}", "something", 123, Guid.Empty);

            var vostokEvent = observedEvents.Single();

            vostokEvent.Properties?["str"].Should().Be("something");
            vostokEvent.Properties?["int"].Should().Be(123);
            vostokEvent.Properties?["guid"].Should().Be(Guid.Empty);
        }

        [Test]
        public void Should_unwrap_sequence_properties()
        {
            serilogLogger.Information("Sequence = {seq}", new [] {1, 2, 3});

            var vostokEvent = observedEvents.Single();

            vostokEvent.Properties?["seq"].Should().BeOfType<object[]>().Which.Should().Equal(1, 2, 3);
        }

        [Test]
        public void Should_unwrap_dictionary_properties()
        {
            serilogLogger.Information("Dictionary = {dic}", new Dictionary<string, int>
            {
                ["A"] = 1,
                ["B"] = 2
            });

            var vostokEvent = observedEvents.Single();

            var dictionary = vostokEvent.Properties?["dic"].Should().BeOfType<Dictionary<object, object>>().Which;

            dictionary?["A"].Should().Be(1);
            dictionary?["B"].Should().Be(2);
        }

        [Test]
        public void Should_wrap_scalar_source_context_property_into_SourceContextValue()
        {
            serilogLogger.ForContext<VostokSink>().Information("Hello!");

            var vostokEvent = observedEvents.Single();

            vostokEvent.Properties?[WellKnownProperties.SourceContext].Should().BeOfType<SourceContextValue>().Which.Should().Equal(typeof(VostokSink).FullName);
        }

        [Test]
        public void Should_wrap_sequence_source_context_property_into_SourceContextValue()
        {
            serilogLogger.ForContext(Constants.SourceContextPropertyName, new [] {"ctx1", "ctx2"}).Information("Hello!");

            var vostokEvent = observedEvents.Single();

            vostokEvent.Properties?[WellKnownProperties.SourceContext].Should().BeOfType<SourceContextValue>().Which.Should().Equal("ctx1", "ctx2");
        }
    }
}
