using Bot.Tests.Unit.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Bot.Tests.Unit
{
    /// <summary>
    /// Dialog Handler Test Class
    /// </summary>
    [TestClass]
    public class DialogTests
    {
        /// <summary>
        /// Says the hi to bot.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task SayHiToBot()
        {
            await TestHelper.TestDialogFlow(new HelloWorldBot.RootDialog(), "Hi", "You sent **HI** which was 2 characters");
        }
    }
}
