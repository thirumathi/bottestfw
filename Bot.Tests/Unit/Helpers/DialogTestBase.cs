﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Internals.Fibers;
using Microsoft.Bot.Connector;

using Autofac;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Bot.Builder.Tests
{
    public abstract class DialogTestBase
    {
        [Flags]
        public enum Options { None = 0, Reflection = 1, ScopedQueue = 2, MockConnectorFactory = 4, ResolveDialogFromContainer = 8, LastWriteWinsCachingBotDataStore = 16 };

        /// <summary>
        /// Builds the specified options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="singletons">The singletons.</param>
        /// <returns></returns>
        public static IContainer Build(Options options, params object[] singletons)
        {
            var builder = new ContainerBuilder();
            if (options.HasFlag(Options.ResolveDialogFromContainer))
            {
                builder.RegisterModule(new DialogModule());
            }
            else
            {
                builder.RegisterModule(new DialogModule_MakeRoot());
            }

            // make a "singleton" MockConnectorFactory per unit test execution
            IConnectorClientFactory factory = null;
            builder
                .Register((c, p) => factory ?? (factory = new MockConnectorFactory(c.Resolve<IAddress>().BotId)))
                .As<IConnectorClientFactory>()
                .InstancePerLifetimeScope();

            if (options.HasFlag(Options.Reflection))
            {
                builder.RegisterModule(new ReflectionSurrogateModule());
            }

            var r =
                builder
                .Register<Queue<IMessageActivity>>(c => new Queue<IMessageActivity>())
                .AsSelf();

            if (options.HasFlag(Options.ScopedQueue))
            {
                r.InstancePerLifetimeScope();
            }
            else
            {
                r.SingleInstance();
            }

            builder
                .RegisterType<BotToUserQueue>()
                .AsSelf()
                .InstancePerLifetimeScope();

            builder
                .Register(c => new MapToChannelData_BotToUser(
                    c.Resolve<BotToUserQueue>(),
                    new List<IMessageActivityMapper> { new KeyboardCardMapper() }))
                .As<IBotToUser>()
                .InstancePerLifetimeScope();

            if (options.HasFlag(Options.LastWriteWinsCachingBotDataStore))
            {
                builder.Register<CachingBotDataStore>(c => new CachingBotDataStore(c.ResolveKeyed<IBotDataStore<BotData>>(typeof(ConnectorStore)), CachingBotDataStoreConsistencyPolicy.LastWriteWins))
                    .As<IBotDataStore<BotData>>()
                    .AsSelf()
                    .InstancePerLifetimeScope();
            }

            foreach (var singleton in singletons)
            {
                builder
                    .Register(c => singleton)
                    .Keyed(FiberModule.Key_DoNotSerialize, singleton.GetType());
            }

            return builder.Build();
        }

        public static class ChannelID
        {
            public const string User = "testUser";
            public const string Bot = "YOUR_BOT_ID";
        }

        /// <summary>
        /// Makes the test message.
        /// </summary>
        /// <returns></returns>
        public static IMessageActivity MakeTestMessage()
        {
            return new Activity()
            {
                Id = Guid.NewGuid().ToString(),
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = ChannelID.User },
                Conversation = new ConversationAccount { Id = Guid.NewGuid().ToString() },
                Recipient = new ChannelAccount { Id = ChannelID.Bot },
                ServiceUrl = "InvalidServiceUrl",
                ChannelId = "Test",
                Attachments = Array.Empty<Attachment>(),
                Entities = Array.Empty<Entity>(),
            };
        }

        /// <summary>
        /// Posts the activity asynchronous.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="toBot">To bot.</param>
        /// <param name="token">The token.</param>
        /// <returns></returns>
        public static async Task PostActivityAsync(ILifetimeScope container, IMessageActivity toBot, CancellationToken token)
        {
            using (var scope = DialogModule.BeginLifetimeScope(container, toBot))
            {
                var task = scope.Resolve<IPostToBot>();
                await task.PostAsync(toBot, token);
            }
        }

        /// <summary>
        /// Asserts the script asynchronous.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="pairs">The pairs.</param>
        /// <returns></returns>
        public static async Task AssertScriptAsync(ILifetimeScope container, params string[] pairs)
        {
            Assert.AreNotEqual(0, pairs.Length);

            var toBot = MakeTestMessage();

            for (int index = 0; index < pairs.Length; ++index)
            {
                var toBotText = pairs[index];
                toBot.Text = toBotText;

                await PostActivityAsync(container, toBot, CancellationToken.None);

                var queue = container.Resolve<Queue<IMessageActivity>>();

                while (queue.Count > 0)
                {
                    ++index;

                    var toUser = queue.Dequeue();

                    var actual = toUser.Text;
                    var expected = pairs[index];

                    Assert.AreEqual(expected, actual);
                }
            }
        }

        /// <summary>
        /// Asserts the mentions.
        /// </summary>
        /// <param name="expectedText">The expected text.</param>
        /// <param name="actualToUser">The actual to user.</param>
        public static void AssertMentions(string expectedText, IEnumerable<IMessageActivity> actualToUser)
        {
            Assert.AreEqual(1, actualToUser.Count());
            var index = actualToUser.Single().Text.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(index >= 0);
        }

        /// <summary>
        /// Asserts the mentions.
        /// </summary>
        /// <param name="expectedText">The expected text.</param>
        /// <param name="scope">The scope.</param>
        public static void AssertMentions(string expectedText, ILifetimeScope scope)
        {
            var queue = scope.Resolve<Queue<IMessageActivity>>();
            AssertMentions(expectedText, queue);
        }

        /// <summary>
        /// Asserts the no messages.
        /// </summary>
        /// <param name="scope">The scope.</param>
        public static void AssertNoMessages(ILifetimeScope scope)
        {
            var queue = scope.Resolve<Queue<IMessageActivity>>();
            Assert.AreEqual(0, queue.Count);
        }

        /// <summary>
        /// News the identifier.
        /// </summary>
        /// <returns></returns>
        public static string NewID()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
