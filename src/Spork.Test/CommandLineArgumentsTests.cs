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
        public void GetCurrent_ShouldReturnModel()
        {
            var args = new CommandLineArguments();
            var model = args.GetCurrent();
            Assert.IsNotNull(model);
        }
    }
}