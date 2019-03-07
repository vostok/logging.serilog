using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Vostok.Logging.Abstractions;
using LogEvent = Vostok.Logging.Abstractions.LogEvent;
using SerilogEvent = Serilog.Events.LogEvent;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace Vostok.Logging.Serilog
{
    [TestFixture]
    internal class SerilogLog_Tests
    {
        private StringBuilder outputBuilder;
        private StringWriter outputWriter;

        private LoggingLevelSwitch serilogLevelSwitch;
        private ILogger serilogLogger;
        private ILog adapter;

        private SerilogEvent observedEvent;

        [SetUp]
        public void TestSetup()
        {
            outputBuilder = new StringBuilder();
            outputWriter = new StringWriter(outputBuilder);

            observedEvent = null;

            serilogLevelSwitch = new LoggingLevelSwitch(SerilogLevel.Verbose);

            serilogLogger = new LoggerConfiguration()
                .WriteTo.TextWriter(outputWriter, outputTemplate: "{Message}")
                .MinimumLevel.ControlledBy(serilogLevelSwitch)
                .Filter.ByExcluding(
                    evt =>
                    {
                        observedEvent = evt;
                        return false;
                    })
                .CreateLogger();

            adapter = new SerilogLog(serilogLogger);
        }

        [Test]
        public void IsEnabledFor_should_return_true_for_enabled_levels()
        {
            serilogLevelSwitch.MinimumLevel = SerilogLevel.Verbose;

            adapter.IsEnabledFor(LogLevel.Debug).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Info).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Warn).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Error).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Fatal).Should().BeTrue();

            serilogLevelSwitch.MinimumLevel = SerilogLevel.Warning;

            adapter.IsEnabledFor(LogLevel.Warn).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Error).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Fatal).Should().BeTrue();
        }

        [Test]
        public void IsEnabledFor_should_return_false_for_disabled_levels()
        {
            serilogLevelSwitch.MinimumLevel = SerilogLevel.Fatal;

            adapter.IsEnabledFor(LogLevel.Debug).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Info).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Warn).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Error).Should().BeFalse();

            serilogLevelSwitch.MinimumLevel = SerilogLevel.Warning;

            adapter.IsEnabledFor(LogLevel.Debug).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Info).Should().BeFalse();
        }

        [Test]
        public void ForContext_should_return_a_log_that_enriches_serilog_events_with_source_context_property()
        {
            adapter = adapter.ForContext("ctx");

            adapter.Info("Hello!");

            observedEvent.Properties[Constants.SourceContextPropertyName]
                .Should().BeOfType<SequenceValue>().Which.Elements.Cast<ScalarValue>().Select(element => element.Value)
                .Should().Equal("ctx");
        }

        [Test]
        public void ForContext_should_support_accumulating_source_context_with_multiple_calls()
        {
            adapter = adapter
                .ForContext("ctx1")
                .ForContext("ctx2")
                .ForContext("ctx3");

            adapter.Info("Hello!");

            observedEvent.Properties[Constants.SourceContextPropertyName]
                .Should().BeOfType<SequenceValue>().Which.Elements.Cast<ScalarValue>().Select(element => element.Value)
                .Should().Equal("ctx1", "ctx2", "ctx3");
        }

        [Test]
        public void Log_method_should_support_null_events()
        {
            adapter.Log(null);

            Output.Should().BeEmpty();
        }

        [Test]
        public void Log_method_should_support_syntax_with_index_based_parameters_in_template()
        {
            adapter.Info("P1 = {0}, P2 = {1}", 1, 2);

            Output.Should().Be("P1 = 1, P2 = 2");

            observedEvent.Properties.Should().HaveCount(2);

            observedEvent.Properties["0"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(1);
            observedEvent.Properties["1"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(2);
        }

        [Test]
        public void Log_method_should_support_syntax_with_named_properties_in_anonymous_object()
        {
            adapter.Info("P1 = {Param1}, P2 = {Param2}", new { Param1 = 1, Param2 = 2 });

            Output.Should().Be("P1 = 1, P2 = 2");

            observedEvent.Properties.Should().HaveCount(2);

            observedEvent.Properties["Param1"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(1);
            observedEvent.Properties["Param2"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(2);
        }

        [Test]
        public void Log_method_should_support_syntax_with_positional_properties_with_names_inferred_from_template()
        {
            adapter.Info("P1 = {Param1}, P2 = {Param2}", 1, 2);

            Output.Should().Be("P1 = 1, P2 = 2");

            observedEvent.Properties.Should().HaveCount(2);

            observedEvent.Properties["Param1"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(1);
            observedEvent.Properties["Param2"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(2);
        }

        [Test]
        public void Log_method_should_support_syntax_without_any_properties()
        {
            adapter.Info("Hello!");

            Output.Should().Be("Hello!");

            observedEvent.Properties.Should().BeEmpty();
        }

        [Test]
        public void Log_method_should_translate_all_properties_not_present_in_template()
        {
            var @event = new LogEvent(LogLevel.Info, DateTimeOffset.Now, null)
                .WithProperty("Param1", 1)
                .WithProperty("Param2", 2);

            adapter.Log(@event);

            observedEvent.Properties["Param1"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(1);
            observedEvent.Properties["Param2"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be(2);
        }

        [Test]
        public void Log_method_should_keep_original_timestamp()
        {
            var @event = new LogEvent(LogLevel.Info, DateTimeOffset.Now - 5.Hours(), null);

            adapter.Log(@event);

            observedEvent.Timestamp.Should().Be(@event.Timestamp);
        }

        [Test]
        public void Log_method_should_keep_original_exception()
        {
            var @event = new LogEvent(LogLevel.Info, DateTimeOffset.Now, null, new Exception("I failed."));

            adapter.Log(@event);

            observedEvent.Exception.Should().BeSameAs(@event.Exception);
        }

        [TestCase(LogLevel.Debug, SerilogLevel.Debug)]
        [TestCase(LogLevel.Info, SerilogLevel.Information)]
        [TestCase(LogLevel.Warn, SerilogLevel.Warning)]
        [TestCase(LogLevel.Error, SerilogLevel.Error)]
        [TestCase(LogLevel.Fatal, SerilogLevel.Fatal)]
        public void Log_method_should_correctly_convert_levels(LogLevel vostokLevel, SerilogLevel serilogLevel)
        {
            var @event = new LogEvent(vostokLevel, DateTimeOffset.Now, null);

            adapter.Log(@event);

            observedEvent.Level.Should().Be(serilogLevel);
        }

        [Test]
        public void Source_context_property_names_should_match_in_vostok_and_serilog()
        {
            WellKnownProperties.SourceContext.Should().Be(Constants.SourceContextPropertyName);
        }

        private string Output => outputBuilder.ToString();
    }
}