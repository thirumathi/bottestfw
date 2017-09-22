using Bot.Tests.Functional.Helpers;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bot.Tests.TFS
{
    public class TFSHelper : IDisposable
    {
        private TfsTeamProjectCollection _tfs;

        private ITestManagementTeamProject _testTeamProject;

        private TeamFoundationIdentity identity;

        List<ITestSuiteBase> testSuites;

        public TFSHelper(Uri tfsProjectUri, NetworkCredential credential, string projectName)
        {
            try
            {
                BasicAuthCredential basicCred = new BasicAuthCredential(credential);

                TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(tfsProjectUri, basicCred);
                tpc.Authenticate();

                identity = tpc.AuthorizedIdentity;

                ITestManagementService testManagementService = (ITestManagementService)tpc.GetService(typeof(ITestManagementService));
                this._testTeamProject = testManagementService.GetTeamProject(projectName);

                testSuites = new List<ITestSuiteBase>();
            }
            catch(Exception ex)
            {
                string s = ex.Message;
            }
        }

        public void Dispose()
        {
            if (this._tfs != null)
            {
                this._tfs.Dispose();
                this._tfs = null;
            }
            this._testTeamProject = null;
        }

        public ITestSuiteBase GetSuite(int suiteID)
        {
            return this._testTeamProject.TestSuites.Find(suiteID);
        }

        public ITestSuiteBase GetSuite(string suiteName)
        {
            string query = string.Concat("SELECT * FROM TestSuite where Title = '", suiteName, "'");

            var firstMatchingSuite = this._testTeamProject.TestSuites.Query(query);

            return firstMatchingSuite.FirstOrDefault();
        }

        public ITestSuiteBase CreateSuite(ITestPlan testPlan, string suiteName)
        {
            var firstMatchingSuite = GetSuite(suiteName);

            if (firstMatchingSuite == null)
            {
                IStaticTestSuite newSuite = this._testTeamProject.TestSuites.CreateStatic();
                newSuite.Title = suiteName;

                ITestSuiteEntry suiteEntry = testPlan.RootSuite.Entries.Add(newSuite);
                testPlan.Save();

                return suiteEntry.TestSuite;
            }

            return firstMatchingSuite;
        }

        public ITestPlan GetTestPlan(int planId)
        {
            return this._testTeamProject.TestPlans.Find(planId);
        }

        public int CreateNewTestCase(BotTestCase botTestCase, ITestPlan testPlan)
        {
            ITestCase testCase = this._testTeamProject.TestCases.Create();
            testCase.Title = botTestCase.Title;

            foreach (var step in botTestCase.Steps)
            {
                ITestStep newStep = testCase.CreateTestStep();
                newStep.Title = step.Action;
                newStep.ExpectedResult = string.Join(";", step.ExpectedReplies);
                testCase.Actions.Add(newStep);
            }

            testCase.Save();

            ITestSuiteBase testSuite = testSuites.FirstOrDefault(ts => ts.Title.Equals(botTestCase.Suite, StringComparison.OrdinalIgnoreCase));
            if (testSuite == null)
            {
                testSuite = this.CreateSuite(testPlan, botTestCase.Suite);
                testSuites.Add(testSuite);
            }

            try
            {
                (testSuite as IStaticTestSuite).Entries.Add(testCase);
                testSuite.Plan.Save();
            }
            catch(Exception ex)
            {
                string s = ex.Message;
            }

            return testCase.Id;
        }

        public List<TestCaseEntry> GetTestCases(ITestSuiteBase testSuite)
        {
            List<TestCaseEntry> TestCases = new List<TestCaseEntry>();

            if (TestSuiteType.StaticTestSuite == testSuite.TestSuiteType)
            {
                IStaticTestSuite staticTestSuite = (IStaticTestSuite)testSuite;
                foreach (ITestSuiteBase current in staticTestSuite.SubSuites)
                {
                    this.GetTestCases(current);
                }
                using (IEnumerator<ITestSuiteEntry> enumerator2 = staticTestSuite.TestCases.Reverse<ITestSuiteEntry>().Reverse<ITestSuiteEntry>().GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        ITestSuiteEntry current2 = enumerator2.Current;
                        if (current2.EntryType == TestSuiteEntryType.TestCase && current2.TestCase != null)
                        {
                            TestCaseEntry customTestcaseEntry = new TestCaseEntry();
                            customTestcaseEntry.Testcase = current2.TestCase;
                            customTestcaseEntry.TestId = current2.TestCase.Id;
                            customTestcaseEntry.SuiteId = staticTestSuite.Id;
                            customTestcaseEntry.SuiteName = staticTestSuite.Title;
                            TestCases.Add(customTestcaseEntry);
                        }
                    }
                    return TestCases;
                }
            }
            if (TestSuiteType.DynamicTestSuite == testSuite.TestSuiteType)
            {
                IDynamicTestSuite dynamicTestSuite = (IDynamicTestSuite)testSuite;
                using (IEnumerator<ITestSuiteEntry> enumerator3 = dynamicTestSuite.TestCases.Reverse<ITestSuiteEntry>().Reverse<ITestSuiteEntry>().GetEnumerator())
                {
                    while (enumerator3.MoveNext())
                    {
                        ITestSuiteEntry current3 = enumerator3.Current;
                        if (current3.EntryType == TestSuiteEntryType.TestCase && current3.TestCase != null)
                        {
                            TestCaseEntry customTestcaseEntry2 = new TestCaseEntry();
                            customTestcaseEntry2.Testcase = current3.TestCase;
                            customTestcaseEntry2.TestId = current3.TestCase.Id;
                            customTestcaseEntry2.SuiteId = dynamicTestSuite.Id;
                            customTestcaseEntry2.SuiteName = dynamicTestSuite.Title;
                            TestCases.Add(customTestcaseEntry2);
                        }
                    }
                    return TestCases;
                }
            }
            if (TestSuiteType.RequirementTestSuite == testSuite.TestSuiteType)
            {
                IRequirementTestSuite requirementTestSuite = (IRequirementTestSuite)testSuite;
                foreach (ITestSuiteEntry current4 in requirementTestSuite.TestCases)
                {
                    if (current4.EntryType == TestSuiteEntryType.TestCase && current4.TestCase != null)
                    {
                        TestCaseEntry customTestcaseEntry3 = new TestCaseEntry();
                        customTestcaseEntry3.Testcase = current4.TestCase;
                        customTestcaseEntry3.TestId = current4.TestCase.Id;
                        customTestcaseEntry3.SuiteId = requirementTestSuite.Id;
                        customTestcaseEntry3.SuiteName = requirementTestSuite.Title;
                        TestCases.Add(customTestcaseEntry3);
                    }
                }
                return TestCases;
            }

            return TestCases;
        }

        public List<TestCaseEntry> GetTestCases(string query)
        {
            List<TestCaseEntry> TestCases = new List<TestCaseEntry>();

            var testCases = this._testTeamProject.TestCases.Query(query);

            foreach (var item in testCases)
            {
                TestCases.Add(new TestCaseEntry() { SuiteId =  item.TestSuiteEntry.Id, SuiteName = item.TestSuiteEntry.Title, Testcase = item, TestId = item.Id });
            }

            return TestCases;
        }

        public async Task<int> RunTests(ITestPlan testPlan, List<TestCaseEntry> testCases)
        {
            int passedCount = 0;
            try
            {
                ITestRun run = testPlan.CreateTestRun(false);
                run.Title = "Test run from Automation Tool on " + DateTime.Now.ToString();

                using (List<TestCaseEntry>.Enumerator enumerator = testCases.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        TestCaseEntry entry = enumerator.Current;
                        ITestPointCollection testPointCollection = testPlan.QueryTestPoints(string.Format("SELECT * FROM TestPoint WHERE SuiteId = {0}", entry.SuiteId.ToString()));
                        IQueryable<ITestPoint> source = testPointCollection.AsQueryable<ITestPoint>();
                        IQueryable<ITestPoint> queryable = from point in source
                                                           where point.TestCaseWorkItem.ToString().Contains(entry.TestId.ToString())
                                                           select point;
                        foreach (ITestPoint current in queryable)
                        {
                            run.AddTestPoint(current, null);
                        }
                    }
                }
                run.Save();

                ITestCaseResultCollection testCaseResultCollection = run.QueryResults();

                foreach (var testCaseResult in testCaseResultCollection)
                {
                    ITestCase testcase = testCaseResult.GetTestCase();
                    await PerformTestRun(testCaseResult, testcase, testCases);
                    if (testCaseResult.Outcome == TestOutcome.Passed)
                    {
                        passedCount++;
                    }

                }

            }
            catch
            {

            }

            return passedCount;
        }

        private async Task PerformTestRun(ITestCaseResult testCaseResult, ITestCase testCase, List<TestCaseEntry> testCases)
        {
            BotTestCase botTestCase = new BotTestCase();
            botTestCase.Id = testCase.Id;

            int count = testCase.Actions.Count;
            int testIDa = testCase.Id;
            TestCaseEntry testcaseEntry = (from o in testCases
                                           where o.TestId == testIDa
                                           select o).SingleOrDefault<TestCaseEntry>();

            ITestIterationResult testIterationResult = testCaseResult.Iterations.FirstOrDefault<ITestIterationResult>();
            if (testIterationResult == null)
            {
                testIterationResult = testCaseResult.CreateIteration(1);
            }

            for (int i = 0; i < testCase.Actions.Count; i++)
            {
                ITestStep testStep = testCase.Actions[i] as ITestStep;
                if (testStep != null)
                {
                    string action = Regex.Replace(Regex.Replace(testStep.Title, "<[^>]*>", " "), " {2,}", " ").Trim();
                    string expectedResult = Regex.Replace(Regex.Replace(testStep.ExpectedResult.ToString(), "<[^>]*>", " "), " {2,}", " ").Trim();

                    botTestCase.Steps.Add(new BotTestCaseStep() { Id = i, Action = action, ExpectedReplies = expectedResult.Split(';').ToList(), ResultType = "Text" });
                }
            }

            botTestCase.StartTime = DateTime.Now;

            TestRunner testRunner = new TestRunner();

            await testRunner.RunTestCase(botTestCase);

            botTestCase.EndTime = DateTime.Now;

            this.SetTestCaseResult(testCaseResult, testIterationResult, testCase, botTestCase);

            if (testCaseResult.Iterations.Count == 0)
            {
                testCaseResult.Iterations.Add(testIterationResult);
            }
            testCaseResult.State = TestResultState.Completed;
            testCaseResult.Save();
        }

        public async Task<int> RunTests(string testRunId, List<TestCaseEntry> testCases)
        {
            int passedCount = 0;
            try
            {
                string query = $"SELECT * FROM TestRun WHERE Id = {testRunId}";
                ITestRun run = this._testTeamProject.TestRuns.Query(query).FirstOrDefault();

                ITestCaseResultCollection testCaseResultCollection = run.QueryResults();
                BotTestCase botTestCase = default(BotTestCase);
                foreach (var testCase in testCases)
                {
                    ITestCaseResult testCaseResult = testCaseResultCollection.FirstOrDefault(tcr => tcr.TestCaseId == testCase.Testcase.Id);
                    if (testCaseResult != null)
                    {
                        botTestCase = new BotTestCase();
                        botTestCase.Id = testCase.Testcase.Id;

                        int count = testCase.Testcase.Actions.Count;
                        TestCaseEntry testcaseEntry = (from o in testCases
                                                       where o.TestId == testCase.TestId
                                                       select o).SingleOrDefault<TestCaseEntry>();

                        ITestIterationResult testIterationResult = testCaseResult.Iterations.FirstOrDefault<ITestIterationResult>();
                        if (testIterationResult == null)
                        {
                            testIterationResult = testCaseResult.CreateIteration(1);
                        }

                        for (int i = 0; i < testCase.Testcase.Actions.Count; i++)
                        {
                            ITestStep testStep = testCase.Testcase.Actions[i] as ITestStep;
                            if (testStep != null)
                            {
                                string action = Regex.Replace(Regex.Replace(testStep.Title, "<[^>]*>", " "), " {2,}", " ").Trim();
                                string expectedResult = Regex.Replace(Regex.Replace(testStep.ExpectedResult.ToString(), "<[^>]*>", " "), " {2,}", " ").Trim();

                                botTestCase.Steps.Add(new BotTestCaseStep() { Id = i, Action = action, ExpectedReplies = expectedResult.Split(';').ToList(), ResultType = "Text" });
                            }
                        }

                        botTestCase.StartTime = DateTime.Now;

                        TestRunner testRunner = new TestRunner();

                        await testRunner.RunTestCase(botTestCase);

                        botTestCase.EndTime = DateTime.Now;

                        this.SetTestCaseResult(testCaseResult, testIterationResult, testCase.Testcase, botTestCase);

                        if (testCaseResult.Iterations.Count == 0)
                        {
                            testCaseResult.Iterations.Add(testIterationResult);
                        }

                        testCaseResult.State = TestResultState.Completed;
                        testCaseResult.Save();

                        if (botTestCase.Outcome == TestOutcome.Passed)
                        {
                            passedCount++;
                        }
                    }
                }
            }
            catch
            {

            }

            return passedCount;
        }

        private void SetTestCaseResult(ITestCaseResult result, ITestIterationResult iteration, ITestCase testcase, BotTestCase botTestCase)
        {
            iteration.DateStarted = botTestCase.StartTime;
            iteration.DateCompleted = botTestCase.EndTime;
            iteration.Duration = botTestCase.Duration;
            result.RunBy = this.identity;

            iteration.Outcome = botTestCase.Outcome;
            result.Outcome = botTestCase.Outcome;

            if (testcase.Actions.Count != botTestCase.Steps.Count)
            {
                iteration.Outcome = TestOutcome.Failed;
                result.Outcome = TestOutcome.Failed;
            }

            for (int i = 0; i < testcase.Actions.Count; i++)
            {
                ITestAction testAction = testcase.Actions[i];
                if (testAction is ISharedStepReference)
                {
                    ISharedStepReference sharedStepReference = testAction as ISharedStepReference;
                    if (sharedStepReference != null)
                    {
                        ISharedStep sharedStep = sharedStepReference.FindSharedStep();
                        ISharedStepResult sharedStepResult = iteration.CreateSharedStepResult(testAction.Id, sharedStep.Id);
                        if (botTestCase.Steps[i] == null)
                        {
                            sharedStepResult.Outcome = TestOutcome.Failed;
                            sharedStepResult.Comment = "Step result is not generated by automation tool";
                            sharedStepResult.ErrorMessage = "Step result is not generated by automation tool";
                        }
                        sharedStepResult.Outcome = botTestCase.Steps[i].Outcome;

                        if (sharedStepResult.Outcome == TestOutcome.Failed)
                        {
                            sharedStepResult.ErrorMessage = botTestCase.Steps[i].ErrorMessage;
                        }
                        else
                        {
                            sharedStepResult.Comment = botTestCase.Steps[i].ErrorMessage;
                        }

                        iteration.Actions.Add(sharedStepResult);
                    }
                }
                else
                {
                    ITestStepResult testStepResult = iteration.CreateStepResult(testAction.Id);
                    testStepResult.Outcome = botTestCase.Steps[i].Outcome;
                    
                    if (testStepResult.Outcome == TestOutcome.Failed)
                    {
                        testStepResult.ErrorMessage = botTestCase.Steps[i].ErrorMessage;
                    }
                    else
                    {
                        testStepResult.ErrorMessage = $"Actual: {botTestCase.Steps[i].Actual}";
                    }

                    iteration.Actions.Add(testStepResult);
                }
            }
        }
    }
}

