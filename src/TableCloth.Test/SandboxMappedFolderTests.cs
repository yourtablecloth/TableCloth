using TableCloth.Models.WindowsSandbox;

namespace TableCloth.Test
{
    [TestClass]
    public sealed class SandboxMappedFolderTests
    {
        [TestMethod]
        public void DefaultAssetPath_ShouldBeCorrect()
        {
            // Assert
            Assert.AreEqual(@"C:\assets", SandboxMappedFolder.DefaultAssetPath);
        }

        [TestMethod]
        public void HostFolder_DefaultValue_ShouldBeEmptyString()
        {
            // Arrange & Act
            var folder = new SandboxMappedFolder();

            // Assert
            Assert.IsEmpty(folder.HostFolder);
        }

        [TestMethod]
        public void SandboxFolder_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var folder = new SandboxMappedFolder();

            // Assert
            Assert.IsNull(folder.SandboxFolder);
        }

        [TestMethod]
        public void ReadOnly_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var folder = new SandboxMappedFolder();

            // Assert
            Assert.IsNull(folder.ReadOnly);
        }

        [TestMethod]
        public void AllProperties_CanBeSet()
        {
            // Arrange
            var folder = new SandboxMappedFolder();

            // Act
            folder.HostFolder = @"C:\MyFolder";
            folder.SandboxFolder = @"C:\Users\WDAGUtilityAccount\Desktop\MyFolder";
            folder.ReadOnly = "True";

            // Assert
            Assert.AreEqual(@"C:\MyFolder", folder.HostFolder);
            Assert.AreEqual(@"C:\Users\WDAGUtilityAccount\Desktop\MyFolder", folder.SandboxFolder);
            Assert.AreEqual("True", folder.ReadOnly);
        }
    }
}
