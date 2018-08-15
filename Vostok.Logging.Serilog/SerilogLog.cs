using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Vostok.Logging.Abstractions;
using LogEvent = Vostok.Logging.Abstractions.LogEvent;
using SerilogEvent = Serilog.Events.LogEvent;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace Vostok.Logging.Serilog
{
    // TODO(iloktionov): xml-docs
    // TODO(iloktionov): unit tests

    public class SerilogLog : ILog
    {
        private readonly ILogger logger;

        public SerilogLog([NotNull] ILogger logger)
        {
            this.logger = logger;
        }

        public void Log(LogEvent @event)
        {
            if (@event == null)
                return;

            if (IsEnabledFor(@event.Level))
            {
                logger.Write(TranslateEvent(@event));
            }
        }

        public bool IsEnabledFor(LogLevel level)
        {
            return logger.IsEnabled(TranslateLevel(level));
        }

        public ILog ForContext(string context)
        {
            return new SerilogLog(logger.ForContext(Constants.SourceContextPropertyName, context));
        }

        private static SerilogLevel TranslateLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return SerilogLevel.Debug;

                case LogLevel.Info:
                    return SerilogLevel.Information;

                case LogLevel.Warn:
                    return SerilogLevel.Warning;

                case LogLevel.Error:
                    return SerilogLevel.Error;

                case LogLevel.Fatal:
                    return SerilogLevel.Fatal;

                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, $"Unexpected value of {nameof(LogLevel)}.");
            }
        }

        private SerilogEvent TranslateEvent(LogEvent @event)
        {
            return new SerilogEvent(
                @event.Timestamp,
                TranslateLevel(@event.Level),
                @event.Exception,
                CreateTemplate(@event),
                CreateProperties(@event));
        }

        private MessageTemplate CreateTemplate(LogEvent @event)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<LogEventProperty> CreateProperties(LogEvent @event)
        {
            throw new NotImplementedException();
        }
    }
}
