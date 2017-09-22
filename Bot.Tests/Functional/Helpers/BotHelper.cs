namespace Bot.Tests.Functional.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Connector.DirectLine;
    using System.Threading;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Bot Helper Class
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class BotHelper : IDisposable 
    {
        /// <summary>
        /// The watermark
        /// </summary>
        private string watermark;

        /// <summary>
        /// The microsoft application identifier
        /// </summary>
        private string microsoftAppId;

        /// <summary>
        /// The bot identifier
        /// </summary>
        private string botId;

        /// <summary>
        /// From user
        /// </summary>
        private string fromUser;

        /// <summary>
        /// The direct line token
        /// </summary>
        private string directLineToken;

        /// <summary>
        /// The direct line client
        /// </summary>
        private DirectLineClient directLineClient;

        /// <summary>
        /// The conversation
        /// </summary>
        private Conversation conversation;

        /// <summary>
        /// The disposed
        /// </summary>
        private bool disposed = false;

        static RegexOptions options = RegexOptions.None;
        Regex regex = new Regex("[ ]{2,}", options);

        /// <summary>
        /// Initializes a new instance of the <see cref="BotHelper"/> class.
        /// </summary>
        /// <param name="DirectLineToken">The direct line token.</param>
        /// <param name="MicrosoftAppId">The microsoft application identifier.</param>
        /// <param name="BotId">The bot identifier.</param>
        public BotHelper(string DirectLineToken, string MicrosoftAppId, string BotId)
        {
            this.microsoftAppId = MicrosoftAppId;
            this.botId = BotId;
            this.directLineToken = DirectLineToken;
        }

        /// <summary>
        /// Starts the new conversation.
        /// </summary>
        public void StartNewConversation()
        {
            watermark = null;
            fromUser = Guid.NewGuid().ToString();
            if (directLineClient != null)
                directLineClient.Dispose();
            directLineClient  = new DirectLineClient(directLineToken);
            conversation = directLineClient.Conversations.StartConversation();
        }

        /// <summary>
        /// Sends the message.
        /// </summary>
        /// <param name="msg">The MSG.</param>
        /// <returns></returns>
        public async Task<string> SendMessage(string msg)
        {
            await SendMessageNoReply(msg);
            return await LastMessageFromBot();
        }

        /// <summary>
        /// Sends the message no reply.
        /// </summary>
        /// <param name="msg">The MSG.</param>
        /// <returns></returns>
        public async Task SendMessageNoReply(string msg)
        {
            await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, MakeActivity(msg), CancellationToken.None);
        }

        /// <summary>
        /// Makes the activity.
        /// </summary>
        /// <param name="msg">The MSG.</param>
        /// <returns></returns>
        private Activity MakeActivity(string msg)
        {
            // Passing in a value in From makes the bot 'remember' that it's the same user
            // and loads the user context that will have been set up previously outside the tests
            return new Activity()
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = fromUser },
                Text = msg
            };
        }

        /// <summary>
        /// Lasts the message from bot.
        /// </summary>
        /// <returns></returns>
        public async Task<string> LastMessageFromBot()
        {
            var botMessages = await AllBotMessagesSinceWatermark();
            return botMessages.Last();
        }

        /// <summary>
        /// Waits for long running operations.
        /// </summary>
        /// <param name="resultHandler">The result handler.</param>
        /// <param name="operationsToWait">The operations to wait.</param>
        /// <param name="delayBetweenPoolingInSeconds">The delay between pooling in seconds.</param>
        /// <returns></returns>
        public async Task WaitForLongRunningOperations(Action<IList<string>> resultHandler, int operationsToWait, int delayBetweenPoolingInSeconds = 4)
        {
            var currentWatermark = watermark;
            var messages = await AllBotMessagesSinceWatermark(currentWatermark).ConfigureAwait(false);
            var iterations = 0;
            var maxIterations = (5 * 60) / delayBetweenPoolingInSeconds;

            while (iterations < maxIterations && messages.Count < operationsToWait)
            {
                await Task.Delay(TimeSpan.FromSeconds(delayBetweenPoolingInSeconds)).ConfigureAwait(false);
                messages = await AllBotMessagesSinceWatermark(currentWatermark);
                iterations++;
            }

            resultHandler(messages);
        }

        /// <summary>
        /// Alls the bot messages since watermark.
        /// </summary>
        /// <param name="specificWatermark">The specific watermark.</param>
        /// <returns></returns>
        private async Task<IList<string>> AllBotMessagesSinceWatermark(string specificWatermark = null)
        {
            var messages = await AllMessagesSinceWatermark(specificWatermark);
            var messagesText = from x in messages
                               where x.From.Id == botId
                               select Regex.Replace(regex.Replace(x.Text.Trim(), " "), "<[^>]*>", ""); ;
            return messagesText.ToList();
        }

        /// <summary>
        /// Alls the messages since watermark.
        /// </summary>
        /// <param name="specificWatermark">The specific watermark.</param>
        /// <returns></returns>
        private async Task<IList<Activity>> AllMessagesSinceWatermark(string specificWatermark = null)
        {
            specificWatermark = string.IsNullOrEmpty(specificWatermark) ? watermark : specificWatermark;
            ActivitySet messageSet = await directLineClient.Conversations.GetActivitiesAsync(conversation.ConversationId, specificWatermark);
            watermark = messageSet?.Watermark;
            return messageSet.Activities;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (directLineClient != null)
                {
                    directLineClient.Dispose();
                }
            }

            disposed = true;
        }
    }
}
