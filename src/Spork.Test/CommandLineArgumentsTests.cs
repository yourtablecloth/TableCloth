using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spork.Components.Implementations;
using System.Threading.Tasks;

namespace Spork.Test
{
    [TestClass]
    public class CommandLineArgumentsTests
    {
        [TestMethod]
        public void Constructor_ShouldInitializeOptions()
        {
            var args = new CommandLineArguments();
            Assert.IsNotNull(args);
        }

        [TestMethod]
        public async Task GetHelpStringAsync_ShouldReturnNonEmptyString()
        {
            var args = new CommandLineArguments();
            var help = await args.GetHelpStringAsync();
            Assert.IsFalse(string.IsNullOrEmpty(help));
        }

        [TestMethod]
        public async Task GetHelpStringAsync_ShouldDocumentTargetUrlSwitch()
        {
            // 무설치 딥링크 체인(.wsb -> tablecloth-prepare.ps1 -> SporkBootstrap -> Spork)이
            // 이 스위치 이름에 의존한다. 이름이 바뀌면 체인이 조용히 끊어지므로 고정한다.
            var args = new CommandLineArguments();
            var help = await args.GetHelpStringAsync();

            Assert.Contains("--target-url", help, "The --target-url switch is missing from the help output.");
        }

        [TestMethod]
        public void GetCurrent_ShouldReturnModel()
        {
            var args = new CommandLineArguments();
            var model = args.GetCurrent();
            Assert.IsNotNull(model);
        }
    }
}