using Microsoft.TeamFoundation.TestManagement.Client;
using System;
using System.Collections.Generic;

namespace Bot.Tests.TFS
{
    public class TestCaseEntry
    {
        public int SuiteId
        {
            get;
            set;
        }

        public string SuiteName
        {
            get;
            set;
        }

        public int TestId
        {
            get;
            set;
        }

        public ITestCase Testcase
        {
            get;
            set;
        }
    }
}