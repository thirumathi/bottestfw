using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Bot.Connector;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Rest;
using System.Net.Http;
using System.Net;
using Autofac;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Builder.Internals.Fibers;
using Microsoft.Bot.Builder.Dialogs;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Web;

namespace Microsoft.Bot.Builder.Tests
{
    /// <summary>
    /// Mock Connector Factory Class
    /// </summary>
    /// <seealso cref="Microsoft.Bot.Builder.Dialogs.Internals.IConnectorClientFactory" />
    public class MockConnectorFactory : IConnectorClientFactory
    {
        /// <summary>
        /// The memory data store
        /// </summary>
        protected readonly IBotDataStore<BotData> memoryDataStore = new InMemoryDataStore();

        /// <summary>
        /// The bot identifier
        /// </summary>
        protected readonly string botId;

        /// <summary>
        /// The state client
        /// </summary>
        public StateClient StateClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockConnectorFactory"/> class.
        /// </summary>
        /// <param name="botId">The bot identifier.</param>
        public MockConnectorFactory(string botId)
        {
            SetField.NotNull(out this.botId, nameof(botId), botId);
        }

        /// <summary>
        /// Make the IConnectorClient implementation.
        /// </summary>
        /// <returns>
        /// The IConnectorClient implementation.
        /// </returns>
        public IConnectorClient MakeConnectorClient()
        {
            var client = new Mock<ConnectorClient>();
            client.CallBase = true;
            return client.Object;
        }

        /// <summary>
        /// Make the <see cref="T:Microsoft.Bot.Connector.IStateClient" /> implementation.
        /// </summary>
        /// <returns>
        /// The <see cref="T:Microsoft.Bot.Connector.IStateClient" /> implementation.
        /// </returns>
        public IStateClient MakeStateClient()
        {
            if (this.StateClient == null)
            {
                this.StateClient = MockIBots(this).Object;
            }
            return this.StateClient;
        }

        /// <summary>
        /// Addresses from.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="conversationId">The conversation identifier.</param>
        /// <returns></returns>
        protected IAddress AddressFrom(string channelId, string userId, string conversationId)
        {
            var address = new Address
            (
                this.botId,
                channelId,
                userId ?? "AllUsers",
                conversationId ?? "AllConversations",
                "InvalidServiceUrl"
            );
            return address;
        }

        /// <summary>
        /// Upserts the data.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="conversationId">The conversation identifier.</param>
        /// <param name="storeType">Type of the store.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        protected async Task<HttpOperationResponse<object>> UpsertData(string channelId, string userId, string conversationId, BotStoreType storeType, BotData data)
        {
            var _result = new HttpOperationResponse<object>();
            _result.Request = new HttpRequestMessage();
            try
            {
                var address = AddressFrom(channelId, userId, conversationId);
                await memoryDataStore.SaveAsync(address, storeType, data, CancellationToken.None);
            }
            catch (HttpException e)
            {
                _result.Body = e.Data;
                _result.Response = new HttpResponseMessage { StatusCode = HttpStatusCode.PreconditionFailed };
                return _result;
            }
            catch (Exception)
            {
                _result.Response = new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
                return _result;
            }

            _result.Body = data;
            _result.Response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK };
            return _result;
        }

        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="conversationId">The conversation identifier.</param>
        /// <param name="storeType">Type of the store.</param>
        /// <returns></returns>
        protected async Task<HttpOperationResponse<object>> GetData(string channelId, string userId, string conversationId, BotStoreType storeType)
        {
            var _result = new HttpOperationResponse<object>();
            _result.Request = new HttpRequestMessage();
            BotData data;
            var address = AddressFrom(channelId, userId, conversationId);
            data = await memoryDataStore.LoadAsync(address, storeType, CancellationToken.None);
            _result.Body = data;
            _result.Response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK };
            return _result;
        }

        /// <summary>
        /// Mocks the i bots.
        /// </summary>
        /// <param name="mockConnectorFactory">The mock connector factory.</param>
        /// <returns></returns>
        public Mock<StateClient> MockIBots(MockConnectorFactory mockConnectorFactory)
        {
            var botsClient = new Moq.Mock<StateClient>(MockBehavior.Loose);

            botsClient.Setup(d => d.BotState.SetConversationDataWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BotData>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, BotData, Dictionary<string, List<string>>, CancellationToken>(async (channelId, conversationId, data, headers, token) =>
                {
                    return await mockConnectorFactory.UpsertData(channelId, null, conversationId, BotStoreType.BotConversationData, data);
                });

            botsClient.Setup(d => d.BotState.GetConversationDataWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, Dictionary<string, List<string>>, CancellationToken>(async (channelId, conversationId, headers, token) =>
                {
                    return await mockConnectorFactory.GetData(channelId, null, conversationId, BotStoreType.BotConversationData);
                });


            botsClient.Setup(d => d.BotState.SetUserDataWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BotData>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
              .Returns<string, string, BotData, Dictionary<string, List<string>>, CancellationToken>(async (channelId, userId, data, headers, token) =>
              {
                  return await mockConnectorFactory.UpsertData(channelId, userId, null, BotStoreType.BotUserData, data);
              });

            botsClient.Setup(d => d.BotState.GetUserDataWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, Dictionary<string, List<string>>, CancellationToken>(async (channelId, userId, headers, token) =>
                {
                    return await mockConnectorFactory.GetData(channelId, userId, null, BotStoreType.BotUserData);
                });

            botsClient.Setup(d => d.BotState.SetPrivateConversationDataWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BotData>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
             .Returns<string, string, string, BotData, Dictionary<string, List<string>>, CancellationToken>(async (channelId, conversationId, userId, data, headers, token) =>
             {
                 return await mockConnectorFactory.UpsertData(channelId, userId, conversationId, BotStoreType.BotPrivateConversationData, data);
             });

            botsClient.Setup(d => d.BotState.GetPrivateConversationDataWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
             .Returns<string, string, string, Dictionary<string, List<string>>, CancellationToken>(async (channelId, conversationId, userId, headers, token) =>
             {
                 return await mockConnectorFactory.GetData(channelId, userId, conversationId, BotStoreType.BotPrivateConversationData);
             });

            return botsClient;
        }
    }
}
