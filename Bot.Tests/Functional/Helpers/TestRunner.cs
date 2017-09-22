namespace Bot.Tests.Functional.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.TeamFoundation.TestManagement.Client;

    /// <summary>
    /// Test Runner Class
    /// </summary>
    internal class TestRunner
    {
        /// <summary>
        /// Runs the test case.
        /// </summary>
        /// <param name="testCase">The test case.</param>
        /// <returns></returns>
        internal async Task RunTestCase(BotTestCase testCase)
        {
            await RunTestCase(testCase, new List<BotTestCaseStep>());
        }

        /// <summary>
        /// Runs the test cases.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="completionTestCase">The completion test case.</param>
        /// <param name="completionChecks">The completion checks.</param>
        /// <returns></returns>
        internal async Task RunTestCase(BotTestCase testCase, BotTestCaseStep completionTestCase = null, int completionChecks = 1)
        {
            await RunTestCase(testCase, new List<BotTestCaseStep> { completionTestCase }, completionChecks);
        }

        /// <summary>
        /// Runs the test cases.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="completionTestCases">The completion test cases.</param>
        /// <param name="completionChecks">The completion checks.</param>
        /// <param name="strictCheck">if set to <c>true</c> [strict check].</param>
        /// <returns></returns>
        internal async Task RunTestCase(BotTestCase testCase, IList<BotTestCaseStep> completionTestCases = null, int completionChecks = 1, bool strictCheck = true)
        {
            BotHelper botHelper = new BotHelper(General.DirectLineToken, General.AppId, General.BotId);
            botHelper.StartNewConversation();
            if (completionTestCases != null && completionTestCases.Count > 1 && completionTestCases.Count < completionChecks)
            {
                Assert.Fail($"There are completion test cases missing. Completion Test Cases: {completionTestCases.Count} for {completionChecks} completionChecks");
            }

            foreach (var step in testCase.Steps)
            {
                await botHelper.SendMessageNoReply(step.Action);

                Action<IList<string>> action = (replies) =>
                {
                    bool resultMatch = false;
                    string actualResult = default(string);
                    foreach (string expectedReply in step.ExpectedReplies)
                    {
                        var match = replies.FirstOrDefault(stringToCheck => stringToCheck.ToLowerInvariant().Contains(expectedReply.ToLowerInvariant()));
                        if (!string.IsNullOrEmpty(match))
                        {
                            actualResult = match;
                            resultMatch = true;
                            break;
                        }
                    }

                    //Assert.IsTrue(resultMatch, step.ErrorMessageHandler(step.Action, "None of the expected replies matched with the actual reply", string.Join(", ", replies)));
                    step.Outcome = resultMatch ? TestOutcome.Passed : TestOutcome.Failed;
                    step.Actual = step.Outcome == TestOutcome.Passed ? actualResult : string.Join(" ", replies);
                    step.ErrorMessage = resultMatch ? string.Empty: step.ErrorMessageHandler(step.Action, string.Join(" (or) ", step.ExpectedReplies), string.Join(" ", replies));
                    step.Verified?.Invoke(replies.LastOrDefault());

                };
                await botHelper.WaitForLongRunningOperations(action, 1);
            }

            if (completionTestCases != null && completionTestCases.Any())
            {
                Action<IList<string>> action = (replies) =>
                {
                    var singleCompletionTestCase = completionTestCases.Count == 1;

                    for (int i = 0; i < replies.Count(); i++)
                    {
                        if (!strictCheck && completionChecks > replies.Count())
                        {
                            var skip = completionChecks - replies.Count();

                            completionTestCases = completionTestCases.Skip(skip).ToList();
                        }

                        var completionIndex = singleCompletionTestCase ? 0 : i;
                        var completionTestCase = completionTestCases[completionIndex];

                        bool resultMatch = false;
                        foreach (string expectedReply in completionTestCase.ExpectedReplies)
                        {
                            if (replies[i].ToLowerInvariant().Contains(expectedReply.ToLowerInvariant()))
                            {
                                resultMatch = true;
                                break;
                            }
                        }

                        Assert.IsTrue(
                            resultMatch,
                            completionTestCase.ErrorMessageHandler(completionTestCase.Action, "None of the completion testcase expected replies matched with the actual reply", replies[i]));

                        completionTestCase.Verified?.Invoke(replies[i]);
                    }
                };

                await botHelper.WaitForLongRunningOperations(action, completionChecks);
            }
        }
    }
}
