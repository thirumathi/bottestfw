namespace Bot.Tests.Functional.Helpers
{
    using Microsoft.TeamFoundation.TestManagement.Client;
    using System;
    using System.Collections.Generic;

    public class BotTestCase
    {
        public string Suite { get; set; }

        public string Title { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public TimeSpan Duration
        {
            get
            {
                if (StartTime != DateTime.MinValue && EndTime != DateTime.MinValue)
                {
                    return EndTime.Subtract(StartTime);
                }

                return TimeSpan.Zero;
            }
        }
        public int Id { get; set; }

        private List<BotTestCaseStep> _steps = new List<BotTestCaseStep>();

        public List<BotTestCaseStep> Steps
        {
            get
            {
                return _steps;
            }
            internal set
            {
                _steps = value;
            }
        }

        public TestOutcome Outcome
        {
            get
            {
                TestOutcome result = TestOutcome.NotExecuted;

                foreach (var item in Steps)
                {
                    if (item.Outcome == TestOutcome.Failed)
                    {
                        result = item.Outcome;
                        break;
                    }
                    else
                    {
                        result = item.Outcome;
                    }
                }

                return result;
            }
        }

    }

        /// <summary>
        /// Bot Test Case Class
        /// </summary>
        public class BotTestCaseStep
    {
        public int Id { get; set; }

        public string ResultType { get; set; }

        public string Actual { get; set; }

        public string ErrorMessage { get; set; }

        public TestOutcome Outcome
        { get; set; }

        /// <summary>
        /// The action
        /// </summary>
        private string _action;

        /// <summary>
        /// The expected reply
        /// </summary>
        private List<string> _expectedReplies = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="BotTestCaseStep"/> class.
        /// </summary>
        public BotTestCaseStep()
        {
            this.ErrorMessageHandler = DefaultErrorMessageHandler;
        }

        /// <summary>
        /// Gets or sets the action.
        /// </summary>
        /// <value>
        /// The action.
        /// </value>
        public string Action
        {
            get
            {
                return _action;
            }
            internal set
            {
                _action = value.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Gets or sets the expected reply.
        /// </summary>
        /// <value>
        /// The expected reply.
        /// </value>
        public List<string> ExpectedReplies {
            get
            {
                return _expectedReplies;
            }
            internal set
            {
                _expectedReplies = value;
            }
        }

        /// <summary>
        /// Gets or sets the error message handler.
        /// </summary>
        /// <value>
        /// The error message handler.
        /// </value>
        public Func<string, string, string, string> ErrorMessageHandler { get; internal set; }

        public Action<string> Verified { get; internal set; }

        /// <summary>
        /// Defaults the error message handler.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="expectedReply">The expected reply.</param>
        /// <param name="receivedReply">The received reply.</param>
        /// <returns></returns>
        private static string DefaultErrorMessageHandler(string action, string expectedReply, string receivedReply)
        {
            return $"[{action}] received reply [{receivedReply}] that doesn't contain the expected message [{expectedReply}]";
        }
    }
}
