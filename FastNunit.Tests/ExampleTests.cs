using NUnit.Framework;

namespace FastNunit.Tests
{
    /// <summary>
    /// The example tests.
    /// </summary>
    [TestFixture]
    public class ExampleTests
    {
        #region Public Methods and Operators

        [Test]
        public void WillFail()
        {
            Assert.Fail("I just fail");
        }

        [Test]
        public void WillAlsoFail()
        {
            Assert.Fail("I just fail");
        }

        [Test]
        public void Inconclusive()
        {
            Assert.Inconclusive("I just don't know");
        }

        [Test, Ignore]
        public void Ignored()
        {
            Assert.Fail("I should be ignored");
        }

        #endregion
    }
}