using TableCloth.Models.Answers;

namespace TableCloth.Test
{
    [TestClass]
    public sealed class SporkAnswersTests
    {
        [TestMethod]
        public void EnableSandboxPublicDnsFallback_DefaultValue_ShouldBeTrue()
        {
            var answers = new SporkAnswers();
            Assert.IsTrue(answers.EnableSandboxPublicDnsFallback);
        }

        [TestMethod]
        public void EnableZScalerRootCertPropagation_DefaultValue_ShouldBeFalse()
        {
            var answers = new SporkAnswers();
            Assert.IsFalse(answers.EnableZScalerRootCertPropagation);
        }

        [TestMethod]
        public void EnableZScalerRootCertPropagation_CanBeSet()
        {
            var answers = new SporkAnswers { EnableZScalerRootCertPropagation = true };
            Assert.IsTrue(answers.EnableZScalerRootCertPropagation);
        }
    }
}
