using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Accenture.CIO.Bot.Tests.TFS
{
    public class ResultUploader
    {
        public bool isRunningInConsoleMode;

        public bool UpdateStatus;

        public bool UpdateComments;

        public bool UpdateAttachments;

        public bool ExecutionStopFlag;

        public int currentCount;

        private ITestPlan currentTestPlan;

        private ITestRun run;

        public string ConsoleReportsLocation;

        public List<SelectedTestcaseEntry> selectedTestCaseIDs;

        public int automatedTestCases;

        public int automatableTestCases;

        public int totalTestCases;

        private string ReportsBaseLocation = ConfigurationManager.AppSettings["OutputDirName"].ToString();

        private string outputFileName = ConfigurationManager.AppSettings["OutputFileName"].ToString();

        public List<TestCaseEntry> TestCases;

        public TeamFoundationIdentity Identity;

        public ResultUploader(ITestPlan testPlan, List<SelectedTestcaseEntry> testCaseIDs, bool isConsoleMode)
        {
            this.ExecutionStopFlag = false;
            this.isRunningInConsoleMode = isConsoleMode;
            this.currentTestPlan = testPlan;
            this.selectedTestCaseIDs = testCaseIDs;
            this.run = this.currentTestPlan.CreateTestRun(false);
            this.currentCount = 0;
            this.run.Title = "Test run from SOAP UI Utility on " + DateTime.Now.ToString();
        }

        public ResultUploader(ITestPlan testPlan, List<TestCaseEntry> testCaseIDs, bool isConsoleMode)
        {
            this.ExecutionStopFlag = false;
            this.isRunningInConsoleMode = isConsoleMode;
            this.currentTestPlan = testPlan;
            this.selectedTestCaseIDs = (from testCaseID in testCaseIDs
                                        select new SelectedTestcaseEntry
                                        {
                                            SuiteId = testCaseID.SuiteId,
                                            Status = 0,
                                            TestId = testCaseID.Testcase.Id
                                        }).ToList<SelectedTestcaseEntry>();
            this.run = this.currentTestPlan.CreateTestRun(false);
            this.run.Title = "Test run from SOAP UI Utility on " + DateTime.Now.ToString();
            this.currentCount = 0;
        }

        public bool ExecuteCommandSync(string programName, string programArgs)
        {
            bool result;
            try
            {
                System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo();
                processStartInfo.FileName = programName;
                processStartInfo.Arguments = programArgs;
                processStartInfo.RedirectStandardOutput = false;
                processStartInfo.UseShellExecute = true;
                processStartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                processStartInfo.CreateNoWindow = false;
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo = processStartInfo;
                process.Start();
                process.WaitForExit();
                result = true;
            }
            catch (Exception ex)
            {
                Logger.Write(new LogEntry
                {
                    Message = ex.Message + ex.Source + ex.InnerException,
                    Priority = 1,
                    Severity = System.Diagnostics.TraceEventType.Critical
                });
                result = false;
            }
            return result;
        }

        private bool CallSoapUI(ITestCase testcase, TestCaseData tData, string projectFileFullPath, string individualTestReportPath)
        {
            if (this.ExecutionStopFlag)
            {
                return false;
            }
            testcase.Id.ToString();
            string text = "\"" + projectFileFullPath + "\"";
            string text2 = "\"" + tData.Name + "\"";
            string text3 = "\"" + individualTestReportPath + "\"";
            string programName = ConfigurationManager.AppSettings["AppName"].ToString();
            string programArgs = string.Format("{0} -I -M -c {1} -a -f {2} > \"{3}\\Soapui.log\"", new object[]
            {
                text,
                text2,
                text3,
                individualTestReportPath
            });
            if (!Directory.Exists(individualTestReportPath))
            {
                Directory.CreateDirectory(individualTestReportPath);
            }
            bool result = this.ExecuteCommandSync(programName, programArgs);
            this.currentCount++;
            return result;
        }

        private void SetTestCasetoFailedState(ITestCaseResult result, ITestIterationResult iteration, ITestCase testcase)
        {
            iteration.DateStarted = DateTime.Now;
            iteration.DateCompleted = DateTime.Now;
            iteration.Duration = new TimeSpan(0L);
            if (this.UpdateComments)
            {
                iteration.Comment = "Test case run has been FAILED, due to mismatch in no of Test steps or Test case execution failed or incorrect SoapUI project file.";
            }
            iteration.Outcome = TestOutcome.Failed;
            result.RunBy = this.Identity;
            result.Outcome = TestOutcome.Failed;
            if (this.UpdateComments)
            {
                result.Comment = "Test Step has been FAILED, due to mismatch in no of Test steps or Test case execution failed or incorrect SoapUI project file";
            }
            for (int i = 0; i < testcase.Actions.Count; i++)
            {
                ITestAction testAction = testcase.Actions[i];
                if (!(testAction is ISharedStepReference))
                {
                    ITestStepResult testStepResult = iteration.CreateStepResult(testAction.Id);
                    if (this.UpdateComments)
                    {
                        testStepResult.Comment = "SOAP UI Test case Execution is FAILED";
                        testStepResult.ErrorMessage = "Test Step has been FAILED, due to mismatch in no of Test steps or Test case execution failed or incorrect SoapUI project file.";
                    }
                    testStepResult.Outcome = TestOutcome.Failed;
                    iteration.Actions.Add(testStepResult);
                }
            }
        }

        private void SetTestCasetoResult(ITestCaseResult result, ITestIterationResult iteration, ITestCase testcase, TestCaseRunResult runResult)
        {
            double num = (double)runResult.TimeTaken / 1000.0 * 10000000.0;
            iteration.DateStarted = runResult.TimeStamp;
            iteration.DateCompleted = iteration.DateStarted;
            iteration.DateCompleted = iteration.DateCompleted.Add(new TimeSpan((long)num));
            iteration.Duration = new TimeSpan((long)num);
            iteration.Comment = runResult.Info;
            result.Comment = runResult.Info;
            result.RunBy = this.Identity;
            if (runResult == null)
            {
                iteration.Outcome = TestOutcome.Failed;
                result.Outcome = TestOutcome.Failed;
            }
            if (runResult.Status == "FINISHED")
            {
                iteration.Outcome = TestOutcome.Passed;
                result.Outcome = TestOutcome.Passed;
            }
            else
            {
                iteration.Outcome = TestOutcome.Failed;
                result.Outcome = TestOutcome.Failed;
            }
            if (testcase.Actions.Count != runResult.TestSteps.Count)
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
                        if (runResult.TestSteps[i] == null)
                        {
                            sharedStepResult.Outcome = TestOutcome.Failed;
                            sharedStepResult.Comment = "Step result is not generated by SOAP UI";
                            sharedStepResult.ErrorMessage = "Step result is not generated by SOAP UI";
                        }
                        if (runResult.TestSteps[i].Status == "OK")
                        {
                            sharedStepResult.Outcome = TestOutcome.Passed;
                            this.GetComments(runResult.TestSteps[i].StepData, sharedStepResult);
                        }
                        else
                        {
                            sharedStepResult.Outcome = TestOutcome.Failed;
                            this.GetComments(runResult.TestSteps[i].StepData, sharedStepResult);
                        }
                        iteration.Actions.Add(sharedStepResult);
                    }
                }
                else
                {
                    ITestStepResult testStepResult = iteration.CreateStepResult(testAction.Id);
                    if (runResult.TestSteps[i].Status == "OK")
                    {
                        testStepResult.Outcome = TestOutcome.Passed;
                        this.GetComments(runResult.TestSteps[i].StepData, testStepResult);
                    }
                    else
                    {
                        testStepResult.Outcome = TestOutcome.Failed;
                        iteration.Outcome = TestOutcome.Failed;
                        result.Outcome = TestOutcome.Failed;
                        this.GetComments(runResult.TestSteps[i].StepData, testStepResult);
                    }
                    iteration.Actions.Add(testStepResult);
                }
            }
        }

        private void GetComments(List<StepData> stepData, ISharedStepResult stepResult)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (StepData current in stepData)
            {
                stringBuilder.Append(current.Comments);
                stringBuilder.Append(Environment.NewLine);
                if (this.UpdateAttachments && current.FileAttachment != null)
                {
                    string[] fileAttachment = current.FileAttachment;
                    for (int i = 0; i < fileAttachment.Length; i++)
                    {
                        string text = fileAttachment[i];
                        if (!string.IsNullOrEmpty(text))
                        {
                            ITestAttachment testAttachment = stepResult.CreateAttachment(text);
                            testAttachment.Comment = "Automated SOAP UI Step files attachment";
                            testAttachment.AttachmentType = "text file";
                            stepResult.Attachments.Add(testAttachment);
                        }
                    }
                }
            }
            if (this.UpdateComments)
            {
                stepResult.Comment = stringBuilder.ToString();
                stepResult.ErrorMessage = stringBuilder.ToString();
            }
        }

        private void GetComments(List<StepData> stepData, ITestStepResult stepResult)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (StepData current in stepData)
            {
                stringBuilder.Append(current.Comments);
                stringBuilder.Append(Environment.NewLine);
                if (this.UpdateAttachments && current.FileAttachment != null)
                {
                    string[] fileAttachment = current.FileAttachment;
                    for (int i = 0; i < fileAttachment.Length; i++)
                    {
                        string text = fileAttachment[i];
                        if (!string.IsNullOrEmpty(text))
                        {
                            ITestAttachment testAttachment = stepResult.CreateAttachment(text);
                            testAttachment.Comment = "Automated SOAP UI Step files attachment";
                            testAttachment.AttachmentType = "text file";
                            stepResult.Attachments.Add(testAttachment);
                        }
                    }
                }
            }
            if (this.UpdateComments)
            {
                stepResult.Comment = stringBuilder.ToString();
                stepResult.ErrorMessage = stringBuilder.ToString();
            }
        }

        private bool PrepareTestPoints()
        {
            using (List<SelectedTestcaseEntry>.Enumerator enumerator = this.selectedTestCaseIDs.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    SelectedTestcaseEntry entry = enumerator.Current;
                    if (this.ExecutionStopFlag)
                    {
                        return true;
                    }
                    ITestPointCollection testPointCollection = this.currentTestPlan.QueryTestPoints(string.Format("SELECT * FROM TestPoint WHERE SuiteId = {0}", entry.SuiteId.ToString()));
                    int arg_68_0 = testPointCollection.Count;
                    IQueryable<ITestPoint> source = testPointCollection.AsQueryable<ITestPoint>();
                    IQueryable<ITestPoint> queryable = from point in source
                                                       where point.TestCaseWorkItem.ToString().Contains(entry.TestId.ToString())
                                                       select point;
                    foreach (ITestPoint current in queryable)
                    {
                        this.run.AddTestPoint(current, null);
                    }
                }
            }
            this.run.Save();
            return true;
        }

        private string GetProjectFileFullPath(string location, string fileName)
        {
            string text = "";
            if (File.Exists(location + "\\" + fileName))
            {
                return location + "\\" + fileName;
            }
            if (Directory.Exists(location))
            {
                string[] directories = Directory.GetDirectories(location);
                string[] array = directories;
                for (int i = 0; i < array.Length; i++)
                {
                    string location2 = array[i];
                    text = this.GetProjectFileFullPath(location2, fileName);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
                return text;
            }
            return "";
        }

        public bool RunTests(string soapUIProjectLocation)
        {
            string text = this.isRunningInConsoleMode ? this.ConsoleReportsLocation : this.ReportsBaseLocation;
            if (!Directory.Exists(text))
            {
                Directory.CreateDirectory(text);
            }
            string path = string.Format("{0}\\Status.log", text);
            bool result;
            try
            {
                if (this.ExecutionStopFlag)
                {
                    result = false;
                }
                else if (!this.UpdateAttachments && !this.UpdateComments && !this.UpdateStatus)
                {
                    using (List<SelectedTestcaseEntry>.Enumerator enumerator = this.selectedTestCaseIDs.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            SelectedTestcaseEntry SelectedEntry = enumerator.Current;
                            if (this.ExecutionStopFlag)
                            {
                                result = false;
                                return result;
                            }
                            TestCaseEntry customTestcaseEntry = (from o in this.TestCases
                                                                       where o.Testcase.Id == SelectedEntry.TestId
                                                                       select o).FirstOrDefault<TestCaseEntry>();
                            if (customTestcaseEntry != null)
                            {
                                ITestCase testcase = customTestcaseEntry.Testcase;
                                string text2 = string.Format("{0}\\{1}", text, testcase.Id);
                                string fileName = testcase.CustomFields["SoapUI project file Path"].Value.ToString();
                                string projectFileFullPath = this.GetProjectFileFullPath(soapUIProjectLocation, fileName);
                                ResultUploader.DeleteDirectory(text2);
                                if (!string.IsNullOrEmpty(projectFileFullPath))
                                {
                                    TestCaseData testCaseData = this.PrepareTestCaseData(testcase, projectFileFullPath);
                                    if (testCaseData != null)
                                    {
                                        this.CallSoapUI(testcase, testCaseData, projectFileFullPath, text2);
                                    }
                                }
                            }
                        }
                    }
                    result = true;
                }
                else
                {
                    string arg_169_0 = WindowsIdentity.GetCurrent().Name;
                    this.PrepareTestPoints();
                    if (this.isRunningInConsoleMode)
                    {
                        string fullfilename = string.Format("{0}\\TestRunExecutionStatusReport.xml", text);
                        XMLExecutionStatusReport.LoadDocument(fullfilename, this.totalTestCases, this.automatedTestCases, this.automatableTestCases);
                    }
                    ITestCaseResultCollection testCaseResultCollection = this.run.QueryResults();
                    foreach (ITestCaseResult current in testCaseResultCollection)
                    {
                        ITestCase testCase = current.GetTestCase();
                        try
                        {
                            if (this.ExecutionStopFlag)
                            {
                                result = false;
                                return result;
                            }
                            int count = testCase.Actions.Count;
                            if (count == 0)
                            {
                                XDocument xDocument = XDocument.Parse(testCase.WorkItem.Fields["Steps"].Value.ToString());
                                XAttribute xAttribute = xDocument.Document.Root.Attributes("last").FirstOrDefault<XAttribute>();
                                if (xAttribute != null)
                                {
                                    int.TryParse(xAttribute.Value, out count);
                                }
                            }
                            int testIDa = testCase.Id;
                            SelectedTestcaseEntry testcaseEntry = (from o in this.selectedTestCaseIDs
                                                                   where o.TestId == testIDa
                                                                   select o).SingleOrDefault<SelectedTestcaseEntry>();
                            TestCaseEntry customTestcaseEntry2 = (from o in this.TestCases
                                                                        where o.Testcase.Id == testcaseEntry.TestId
                                                                        select o).FirstOrDefault<TestCaseEntry>();
                            string text3 = string.Format("{0}\\{1}", text, testCase.Id).Replace("\\\\", "\\");
                            string fileName2 = testCase.CustomFields["SoapUI project file Path"].Value.ToString();
                            string projectFileFullPath2 = this.GetProjectFileFullPath(soapUIProjectLocation, fileName2);
                            TestCaseData testCaseData2 = this.PrepareTestCaseData(testCase, projectFileFullPath2);
                            string text4 = "Failed";
                            ITestIterationResult testIterationResult = current.Iterations.FirstOrDefault<ITestIterationResult>();
                            bool flag = false;
                            ResultUploader.DeleteDirectory(text3);
                            if (testIterationResult == null)
                            {
                                testIterationResult = current.CreateIteration(1);
                                testcaseEntry.Status = 3;
                            }
                            if (testCaseData2 != null)
                            {
                                if (!string.IsNullOrEmpty(projectFileFullPath2))
                                {
                                    this.CallSoapUI(testCase, testCaseData2, projectFileFullPath2, text3);
                                }
                                if (this.UpdateAttachments || this.UpdateComments || this.UpdateStatus)
                                {
                                    string text5 = string.Format("{0}\\{1}", text3, this.outputFileName);
                                    if (!Directory.Exists(text3))
                                    {
                                        this.IntroduceDelayForOutputfile(text3);
                                    }
                                    if (Directory.Exists(text3))
                                    {
                                        if (this.UpdateAttachments)
                                        {
                                            if (File.Exists(text5) && text5.Length <= 248)
                                            {
                                                ITestAttachment testAttachment = testIterationResult.CreateAttachment(text5, SourceFileAction.None);
                                                testAttachment.AttachmentType = "xml file";
                                                testAttachment.Comment = "Automated SOAP UI Result";
                                                testIterationResult.Attachments.Add(testAttachment);
                                            }
                                            if (ConfigurationManager.AppSettings["AttachSoapUILog"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                                            {
                                                string text6 = string.Format("{0}\\soapui.log", text3);
                                                if (text6.Length <= 248 && File.Exists(text6))
                                                {
                                                    ITestAttachment testAttachment2 = testIterationResult.CreateAttachment(text6, SourceFileAction.None);
                                                    testAttachment2.AttachmentType = "Log file";
                                                    testIterationResult.Attachments.Add(testAttachment2);
                                                    testAttachment2.Comment = "SOAP UI log file";
                                                }
                                            }
                                        }
                                        if (testCaseData2.steps.Count == count)
                                        {
                                            TestCaseRunResult testCaseRunResult = ReportFilesReader.ReadOutputFiles(text3, testCaseData2, this.outputFileName);
                                            if (testcaseEntry != null && (testCaseRunResult.Status == "OK" || testCaseRunResult.Status == "FINISHED"))
                                            {
                                                testcaseEntry.Status = 2;
                                                text4 = "Passed";
                                            }
                                            this.SetTestCasetoResult(current, testIterationResult, testCase, testCaseRunResult);
                                            flag = true;
                                        }
                                    }
                                }
                            }
                            if (testcaseEntry.Status == 3 && !flag)
                            {
                                this.SetTestCasetoFailedState(current, testIterationResult, testCase);
                            }
                            if (this.UpdateAttachments || this.UpdateComments || this.UpdateStatus)
                            {
                                if (current.Iterations.Count == 0)
                                {
                                    current.Iterations.Add(testIterationResult);
                                }
                                current.State = TestResultState.Completed;
                                current.Save();
                            }
                            if (this.isRunningInConsoleMode)
                            {
                                string uniqueid = "";
                                string assignedTo = string.Empty;
                                if (testCase.CustomFields["UniqueID"].Value != null)
                                {
                                    uniqueid = testCase.CustomFields["UniqueID"].Value.ToString();
                                }
                                assignedTo = testCase.WorkItem["System.AssignedTo"].ToString();
                                XMLExecutionStatusReport.CreateTestCaseNode(customTestcaseEntry2.SuiteId.ToString(), customTestcaseEntry2.SuiteName, customTestcaseEntry2.Testcase.Id.ToString(), customTestcaseEntry2.Testcase.Title, uniqueid, text4, assignedTo);
                                XMLExecutionStatusReport.Save();
                                if (ConfigurationManager.AppSettings["ContinueExecutiononFailureInConsoleMode"].ToString() != "true" && text4 != "Passed")
                                {
                                    Program.ExitApplication();
                                    result = false;
                                    return result;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            using (StreamWriter streamWriter = new StreamWriter(path, true))
                            {
                                streamWriter.WriteLine("{0}--------------------------------------", Environment.NewLine);
                                streamWriter.WriteLine("{0}Error on executing Suite: {1} Testcase: [{2}] {3}", new object[]
                                {
                                    Environment.NewLine,
                                    testCase.TestSuiteEntry.Id,
                                    testCase.Id,
                                    testCase.Title
                                });
                                streamWriter.WriteLine("{0}--------------------------------------{0}{1}", Environment.NewLine, ex.Message);
                                streamWriter.WriteLine("{0}Inner Exception:{0}{1}", Environment.NewLine, (ex.InnerException != null) ? ex.InnerException.Message : string.Empty);
                                streamWriter.WriteLine("{0}Stack Trace:{0}{1}", Environment.NewLine, ex.StackTrace);
                            }
                        }
                    }
                    result = true;
                }
            }
            catch (Exception ex2)
            {
                using (StreamWriter streamWriter2 = new StreamWriter(path, true))
                {
                    streamWriter2.WriteLine("{0}--------------------------------------{0}{1}", Environment.NewLine, ex2.Message);
                    streamWriter2.WriteLine("{0}Inner Exception:{0}{1}", Environment.NewLine, (ex2.InnerException != null) ? ex2.InnerException.Message : string.Empty);
                    streamWriter2.WriteLine("{0}Stack Trace:{0}{1}", Environment.NewLine, ex2.StackTrace);
                }
                result = false;
            }
            return result;
        }
    }
}
