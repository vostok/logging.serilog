using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Serilog.Core;
using Serilog.Events;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Values;
using LogEvent = Vostok.Logging.Abstractions.LogEvent;
using SerilogEvent = Serilog.Events.LogEvent;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace Vostok.Logging.Serilog
{
    [PublicAPI]
    public class VostokSink : ILogEventSink
    {
        private readonly ILog log;

        public VostokSink([NotNull] ILog log)
            => this.log = log ?? throw new ArgumentNullException(nameof(log));

        public void Emit(SerilogEvent @event)
        {
            if (@event == null || !log.IsEnabledFor(TranslateLevel(@event.Level)))
                return;

            log.Log(TranslateEvent(@event));
        }

        private static LogEvent TranslateEvent(SerilogEvent @event)
        {
            return new LogEvent(
                TranslateLevel(@event.Level),
                @event.Timestamp,
                @event.MessageTemplate.Text,
                @event.Properties?.ToDictionary(
                    pair => AdjustPropertyName(pair.Key), 
                    pair => UnwrapPropertyValue(pair.Key, pair.Value)),
                @event.Exception);
        }

        private static LogLevel TranslateLevel(SerilogLevel level)
        {
            switch (level)
            {
                case SerilogLevel.Debug:
                case SerilogLevel.Verbose:
                    return LogLevel.Debug;

                case SerilogLevel.Information:
                    return LogLevel.Info;

                case SerilogLevel.Warning:
                    return LogLevel.Warn;

                case SerilogLevel.Error:
                    return LogLevel.Error;

                case SerilogLevel.Fatal:
                    return LogLevel.Fatal;

                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, $"Unexpected value of {nameof(SerilogLevel)}.");
            }
        }

        private static string AdjustPropertyName(string name)
        {
            if (name == Constants.SourceContextPropertyName)
                return WellKnownProperties.SourceContext;

            return name;
        }

        private static object UnwrapPropertyValue(string name, LogEventPropertyValue value)
        {
            var unwrappedValue = UnwrapPropertyValue(value);

            if (name == Constants.SourceContextPropertyName)
            {
                if (unwrappedValue is IEnumerable<object> unwrappedSequence)
                    unwrappedValue = new SourceContextValue(unwrappedSequence.Select(element => element.ToString()).ToArray());
                else
                    unwrappedValue = new SourceContextValue(unwrappedValue.ToString());
            }

            return unwrappedValue;
        }

        private static object UnwrapPropertyValue(LogEventPropertyValue value)
        {
            switch (value)
            {
                case ScalarValue scalar:
                    return scalar.Value;

                case SequenceValue sequence:
                    return sequence.Elements.Select(UnwrapPropertyValue).ToArray();

                case DictionaryValue dictionary:
                    return dictionary.Elements.ToDictionary(
                        pair => UnwrapPropertyValue(pair.Key),
                        pair => UnwrapPropertyValue(pair.Value));

                case StructureValue structure:
                    return structure.Properties.ToDictionary(prop => prop.Name, prop => UnwrapPropertyValue(prop.Value));

                default:
                    return value?.ToString();
            }
        }
    }
}
