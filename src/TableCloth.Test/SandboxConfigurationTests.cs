using TableCloth.Models.WindowsSandbox;

namespace TableCloth.Test
{
    [TestClass]
    public sealed class SandboxConfigurationTests
    {
        [TestMethod]
        public void VirtualGpu_DefaultValue_ShouldBeDisable()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.AreEqual("Disable", config.VirtualGpu);
        }

        [TestMethod]
        public void Networking_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.Networking);
        }

        [TestMethod]
        public void AudioInput_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.AudioInput);
        }

        [TestMethod]
        public void VideoInput_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.VideoInput);
        }

        [TestMethod]
        public void PrinterRedirection_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.PrinterRedirection);
        }

        [TestMethod]
        public void ClipboardRedirection_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.ClipboardRedirection);
        }

        [TestMethod]
        public void ProtectedClient_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.ProtectedClient);
        }

        [TestMethod]
        public void LogonCommand_DefaultValue_ShouldBeEmptyList()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNotNull(config.LogonCommand);
            Assert.IsEmpty(config.LogonCommand);
        }

        [TestMethod]
        public void MappedFolders_DefaultValue_ShouldBeEmptyList()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNotNull(config.MappedFolders);
            Assert.IsEmpty(config.MappedFolders);
        }

        [TestMethod]
        public void MemoryInMB_DefaultValue_ShouldBeNull()
        {
            // Arrange & Act
            var config = new SandboxConfiguration();

            // Assert
            Assert.IsNull(config.MemoryInMB);
        }

        [TestMethod]
        public void VirtualGpu_CanBeSetToEnable()
        {
            // Arrange
            var config = new SandboxConfiguration();

            // Act
            config.VirtualGpu = "Enable";

            // Assert
            Assert.AreEqual("Enable", config.VirtualGpu);
        }

        [TestMethod]
        public void MappedFolders_CanAddItems()
        {
            // Arrange
            var config = new SandboxConfiguration();
            var folder = new SandboxMappedFolder
            {
                HostFolder = @"C:\Test",
                ReadOnly = "True"
            };

            // Act
            config.MappedFolders.Add(folder);

            // Assert
            Assert.HasCount(1, config.MappedFolders);
            Assert.AreEqual(@"C:\Test", config.MappedFolders[0].HostFolder);
        }

        [TestMethod]
        public void LogonCommand_CanAddItems()
        {
            // Arrange
            var config = new SandboxConfiguration();

            // Act
            config.LogonCommand.Add("cmd.exe /c echo Hello");

            // Assert
            Assert.HasCount(1, config.LogonCommand);
            Assert.AreEqual("cmd.exe /c echo Hello", config.LogonCommand[0]);
        }
    }
}
