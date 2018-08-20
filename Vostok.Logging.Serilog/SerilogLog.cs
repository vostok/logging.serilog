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
    /// <summary>
    /// <para>Represents an adapter between Vostok logging interfaces and Serilog.</para>
    /// <para>It implements Vostok <see cref="ILog"/> interface using an externally provided instance of Serilog <see cref="ILogger"/>.</para>
    /// <para>It does this by following these rules:</para>
    /// <list type="number">
    ///     <item><description>Vostok <see cref="LogLevel"/>s are directly translated to Serilog <see cref="SerilogLevel"/>s.<para/></description></item>
    ///     <item><description>Messages are not prerendered into text as Vostok <see cref="ILog"/>'s formatting syntax capabilities are a subset of those supported by Serilog.<para/></description></item>
    ///     <item><description>Message templates are parsed using <see cref="ILogger.BindMessageTemplate"/>.<para/></description></item>
    ///     <item><description>Event properties are converted using <see cref="ILogger.BindProperty"/>.<para/></description></item>
    ///     <item><description><see cref="ForContext"/> invokes inner <see cref="ILogger"/>'s <see cref="ILogger.ForContext(string,object,bool)"/> with name set to <see cref="Constants.SourceContextPropertyName"/> and wraps resulting <see cref="ILogger"/> into another <see cref="SerilogLog"/>.<para/></description></item>
    /// </list>
    /// </summary>
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
            if (logger.BindMessageTemplate(@event.MessageTemplate, Array.Empty<object>(), out var serilogTemplate, out _))
                return serilogTemplate;

            return MessageTemplate.Empty;
        }

        private IEnumerable<LogEventProperty> CreateProperties(LogEvent @event)
        {
            if (@event.Properties == null)
                yield break;

            foreach (var pair in @event.Properties)
            {
                if (logger.BindProperty(pair.Key, pair.Value, false, out var serilogProperty))
                    yield return serilogProperty;
            }
        }
    }
}
