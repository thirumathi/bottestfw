namespace Bot.Tests.Functional.Helpers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// General Test Class
    /// </summary>
    [TestClass]
    public class General
    {
        public static string AppId { get; set; }

        public static string DirectLineToken { get; set; }

        public static string BotId { get; set; }

        public static string TfsUrl { get; set; }

        public static string TfsProjectName { get; set; }

        public static string TfsUserId { get; set; }
        public static string TfsUserPwd { get; set; }

        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        /// <value>
        /// The test context.
        /// </value>
        public static TestContext testContext { get; set; }

        // Will run once before all of the tests in the project. We start assuming the user is already logged in to Azure,
        // which should  be done separately via the AzureBot.ConsoleConversation or some other means. 
        /// <summary>
        /// Sets up.
        /// </summary>
        /// <param name="context">The context.</param>
        [AssemblyInitialize]
        public static void SetUp(TestContext context)
        {
            testContext = context;
            DirectLineToken = Environment.GetEnvironmentVariable("DirectLineToken");
            if (string.IsNullOrEmpty(DirectLineToken))
                DirectLineToken = context.Properties["DirectLineToken"].ToString();
            AppId = Environment.GetEnvironmentVariable("MicrosoftAppId");
            if (string.IsNullOrEmpty(AppId))
                AppId = context.Properties["MicrosoftAppId"].ToString();
            BotId = Environment.GetEnvironmentVariable("BotId");
            if (string.IsNullOrEmpty(BotId))
                BotId = context.Properties["BotId"].ToString();

            if (string.IsNullOrEmpty(TfsUrl))
                TfsUrl = context.Properties["TfsUrl"].ToString();
            if (string.IsNullOrEmpty(TfsProjectName))
                TfsProjectName = context.Properties["TfsProjectName"].ToString();
            if (string.IsNullOrEmpty(TfsUserId))
                TfsUserId = context.Properties["TfsUserId"].ToString();
            if (string.IsNullOrEmpty(TfsUserPwd))
                TfsUserPwd = context.Properties["TfsUserPwd"].ToString();
        }
    }
}
