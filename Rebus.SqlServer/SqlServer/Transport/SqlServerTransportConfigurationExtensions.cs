﻿using System;
using System.Threading.Tasks;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Threading;
using Rebus.Timeouts;
using Rebus.Transport;

namespace Rebus.SqlServer.Transport
{
    /// <summary>
    /// Configuration extensions for the SQL transport
    /// </summary>
    public static class SqlServerTransportConfigurationExtensions
    {
        /// <summary>
        /// Configures Rebus to use SQL Server to transport messages as a one-way client (i.e. will not be able to receive any messages).
        /// The table specified by <paramref name="tableName"/> will be used to store messages.
        /// The message table will automatically be created if it does not exist.
        /// </summary>
        public static void UseSqlServerAsOneWayClient(this StandardConfigurer<ITransport> configurer, Func<Task<IDbConnection>> connectionFactory, string tableName)
        {
            Configure(configurer, loggerFactory => new DbConnectionFactoryProvider(connectionFactory, loggerFactory), tableName, null);

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }

        /// <summary>
        /// Configures Rebus to use SQL Server to transport messages as a one-way client (i.e. will not be able to receive any messages).
        /// The table specified by <paramref name="tableName"/> will be used to store messages.
        /// The message table will automatically be created if it does not exist.
        /// </summary>
        public static void UseSqlServerAsOneWayClient(this StandardConfigurer<ITransport> configurer, string connectionStringOrConnectionStringName, string tableName)
        {
            Configure(configurer, loggerFactory => new DbConnectionProvider(connectionStringOrConnectionStringName, loggerFactory), tableName, null);

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }

        /// <summary>
        /// Configures Rebus to use SQL Server as its transport. The table specified by <paramref name="tableName"/> will be used to
        /// store messages, and the "queue" specified by <paramref name="inputQueueName"/> will be used when querying for messages.
        /// The message table will automatically be created if it does not exist.
        /// </summary>
        public static void UseSqlServer(this StandardConfigurer<ITransport> configurer, Func<Task<IDbConnection>> connectionFactory, string tableName, string inputQueueName, bool forceSynchronousReceive = false)
        {
            Configure(configurer, loggerFactory => new DbConnectionFactoryProvider(connectionFactory, loggerFactory), tableName, inputQueueName, forceSynchronousReceive);
        }

        /// <summary>
        /// Configures Rebus to use SQL Server as its transport. The table specified by <paramref name="tableName"/> will be used to
        /// store messages, and the "queue" specified by <paramref name="inputQueueName"/> will be used when querying for messages.
        /// The message table will automatically be created if it does not exist.
        /// </summary>
        public static void UseSqlServer(this StandardConfigurer<ITransport> configurer, string connectionStringOrConnectionStringName, string tableName, string inputQueueName, bool forceSynchronousReceive = false)
        {
            Configure(configurer, loggerFactory => new DbConnectionProvider(connectionStringOrConnectionStringName, loggerFactory), tableName, inputQueueName, forceSynchronousReceive);
        }

        static void Configure(StandardConfigurer<ITransport> configurer, Func<IRebusLoggerFactory, IDbConnectionProvider> connectionProviderFactory, string tableName, string inputQueueName, bool forceSynchronousReceive = false)
        {
            configurer.Register(context =>
            {
                var rebusLoggerFactory = context.Get<IRebusLoggerFactory>();
                var asyncTaskFactory = context.Get<IAsyncTaskFactory>();
                var connectionProvider = connectionProviderFactory(rebusLoggerFactory);
                var transport = new SqlServerTransport(connectionProvider, tableName, inputQueueName, rebusLoggerFactory, asyncTaskFactory, forceSynchronousReceive);
                transport.EnsureTableIsCreated();
                return transport;
            });

            configurer.OtherService<ITimeoutManager>().Register(c => new DisabledTimeoutManager());

            configurer.OtherService<IPipeline>().Decorate(c =>
            {
                var pipeline = c.Get<IPipeline>();

                return new PipelineStepRemover(pipeline)
                    .RemoveIncomingStep(s => s.GetType() == typeof(HandleDeferredMessagesStep));
            });
        }
    }
}