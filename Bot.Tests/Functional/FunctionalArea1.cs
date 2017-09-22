using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bot.Tests.Functional.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Bot.Tests.TFS;
using Microsoft.TeamFoundation.TestManagement.Client;
using System.Reflection;
using System.Globalization;

namespace Bot.Tests.Functional
{
    /// <summary>
    /// Functional Area1 test Class
    /// </summary>
    [TestClass]
    public class FunctionalArea1
    {
        public TestContext TestContext { get; set; }

        NetworkCredential cred;
        TFSHelper tfsHelper;

        public FunctionalArea1()
        {
            cred = new NetworkCredential(General.TfsUserId, General.TfsUserPwd);
            tfsHelper = new TFSHelper(new Uri(General.TfsUrl), this.cred, General.TfsProjectName);
        }

        /// <summary>
        /// Initiates this instance.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Salutation()
        {
            ITestPlan testplan = tfsHelper.GetTestPlan(1);
            ITestSuiteBase testSuite = tfsHelper.GetSuite(3);
            List<TestCaseEntry> testCases = tfsHelper.GetTestCases(testSuite);
            int passedTestCases = await tfsHelper.RunTests(testplan, testCases);

            Assert.IsTrue(passedTestCases == testCases.Count, $"{testCases.Count - passedTestCases} out of {testCases.Count} found to be FAILED");
        }

        [TestMethod]
        public async Task ManualTest()
        {
            BotTestCase botTestCase = new BotTestCase();
            botTestCase.Title = "ManualTest";
            botTestCase.Steps.Add(new BotTestCaseStep() { Action = "Hi", ExpectedReplies = new List<string>() { "You sent **HI** which was 2 characters" } });
            botTestCase.Steps.Add(new BotTestCaseStep() { Action = "Hello", ExpectedReplies = new List<string>() { "You sent **HELLO** which was 5 characters" } });
            botTestCase.Steps.Add(new BotTestCaseStep() { Action = "Test", ExpectedReplies = new List<string>() { "You sent **Test** which was 4 characters" } });

            TestRunner testRunner = new TestRunner();
            await testRunner.RunTestCase(botTestCase);

            Assert.IsTrue(botTestCase.Outcome == TestOutcome.Passed, $"Test failed");
        }

        [TestMethod]
        public async Task RunTfsTestCase()
        {
            if (TestContext.Properties.Contains("__Tfs_TestCaseId__"))
            {
                string testCaseId = TestContext.Properties["__Tfs_TestCaseId__"] as string;
                List<TestCaseEntry> testCases = tfsHelper.GetTestCases($"SELECT * FROM WorkItems WHERE Id = {testCaseId}");

                string testRunId = TestContext.Properties["__Tfs_TestRunId__"] as string;

                int passedTestCases = await tfsHelper.RunTests(testRunId, testCases);

                Assert.IsTrue(passedTestCases == testCases.Count, $"{testCases.Count - passedTestCases} out of {testCases.Count} found to be FAILED");
            }
        }
    }
}
