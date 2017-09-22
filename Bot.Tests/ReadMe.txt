--------------------------------------------
About Unit Testing Approach 
--------------------------------------------
Unit tests cases can be created at the Dialog level only, due to the dependency of context and other parameters at the inner level components from dialogs.
Hence its mandatory to pass the Dialog parameter to the unit test template which handles it further. 
The helpers on this project has abstracted the internals of BOT Dialog triggerring and provides the following features.

- Accepts the Dialog to be testd, input message and expected message as parameters
- Creates a Mock Connection to Bot using factory to call the Bot
- Generates the context and other relevant parameters for the the request processing
- Calls the BOT with the input message, using mock connection
- Retrieves the BOT response and validates the same with expected response

--------------------------------------------
Creating a new Unit Test case
--------------------------------------------
. Indetify the dialog to be tested
. Create a new unit test method, attributed with [TestMethod], the class where the methos is locatd must be attributed with [TestClass]
. Call the following method inside the test method logic
	await TestHelper.TestDialogFlow(new [Name of the dialog class to be tested], [input mesage], [expected result]);

--------------------------------------------
Triggering Unit tests
--------------------------------------------
Triggering unit test requires a .runsettings file to be associated.
To associate a runsetings file

Goto Menu

Test --> Test Settings --> Select Test Settings File --> ..\Bot\Bot.Tests\UnitTest.runsettings

--------------------------------------------
Important Notes
--------------------------------------------
- The unit testing DOES NOT require the bot to be hosted and running.
- Can be testing directly from Visual Studio
- The logic across the bot code that depends upon HttpContext.Current would fail on tests, as there is no HttpContext getting set here. Hence there has to be an alternate code implemented inside the 
	logic to handle such scenarios. Try using AppDomain.CurrentDomain or similar functions to acheive desired functionality.