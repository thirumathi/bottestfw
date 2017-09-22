using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Bot.Builder.Base;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Tests;
using Microsoft.Bot.Connector;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bot.Tests.Unit.Helpers
{
    /// <summary>
    /// Test Helper Utility
    /// </summary>
    public class TestHelper
    {
        /// <summary>
        /// Tests the dialog flow.
        /// </summary>
        /// <param name="dialog">The dialog.</param>
        /// <param name="message">The message.</param>
        /// <param name="expectedResult">The expected result.</param>
        /// <returns></returns>
        public static async Task TestDialogFlow(IDialog<object> dialog, string message, string expectedResult)
        {
            var toBot = DialogTestBase.MakeTestMessage();

            toBot.From.Id = Guid.NewGuid().ToString();
            toBot.Text = message;

            Func<IDialog<object>> MakeRoot = () => dialog;

            using (new FiberTestBase.ResolveMoqAssembly(dialog))
            {
                using (var container = DialogTestBase.Build(DialogTestBase.Options.MockConnectorFactory, dialog))
                {
                    // act: sending the message
                    IMessageActivity toUser = await GetResponse(container, MakeRoot, toBot);

                    Assert.IsTrue(toUser != null && toUser.Text.Equals(expectedResult, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        /// <summary>
        /// Gets the response.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="makeRoot">The make root.</param>
        /// <param name="toBot">To bot.</param>
        /// <returns></returns>
        private static async Task<IMessageActivity> GetResponse(IContainer container, Func<IDialog<object>> makeRoot, IMessageActivity toBot)
        {
            IMessageActivity returnActivity = default(IMessageActivity);
            using (var scope = DialogModule.BeginLifetimeScope(container, toBot))
            {
                DialogModule_MakeRoot.Register(scope, makeRoot);

                // act: sending the message
                using (new LocalizedScope(toBot.Locale))
                {
                    var task = scope.Resolve<IPostToBot>();
                    await task.PostAsync(toBot, CancellationToken.None);
                }

                if (scope.Resolve<Queue<IMessageActivity>>().Count > 0)
                {
                    returnActivity = scope.Resolve<Queue<IMessageActivity>>().Dequeue();
                }
            }

            return returnActivity;
        }
    }
}
