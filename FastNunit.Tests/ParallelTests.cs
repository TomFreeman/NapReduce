using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace FastNunit.Tests
{
    /// <summary>
    /// The parallel tests.
    /// </summary>
    [TestFixture]
    [Category("Parallel")]
    public class ParallelTests
    {
        #region Static Fields

        /// <summary>
        /// The static value.
        /// </summary>
        private static string StaticValue;

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// The async tests are isolated.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task AsyncTestsAreIsolated()
        {
            const string constant = "AsyncOne";

            await Task.Run(
                () =>
                    {
                        StaticValue = constant;

                        Thread.Sleep(1000);

                        Assert.That(StaticValue, Is.EqualTo(constant));
                    });
        }

        /// <summary>
        /// The async tests are isolated three.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task AsyncTestsAreIsolatedThree()
        {
            const string constant = "AsyncThree";

            await Task.Run(
                () =>
                    {
                        StaticValue = constant;

                        Thread.Sleep(1000);

                        Assert.That(StaticValue, Is.EqualTo(constant));
                    });
        }

        /// <summary>
        /// The async tests are isolated two.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        [Test]
        public async Task AsyncTestsAreIsolatedTwo()
        {
            const string constant = "AsyncTwo";

            await Task.Run(
                () =>
                    {
                        StaticValue = constant;

                        Thread.Sleep(1000);

                        Assert.That(StaticValue, Is.EqualTo(constant));
                    });
        }

        /// <summary>
        /// The non parameterised tests keep statics isolated one.
        /// </summary>
        [Test]
        public void NonParameterisedTestsKeepStaticsIsolatedOne()
        {
            const string constant = "One";

            StaticValue = constant;

            Thread.Sleep(1000);

            Assert.That(StaticValue, Is.EqualTo(constant));
        }

        /// <summary>
        /// The non parameterised tests keep statics isolated three.
        /// </summary>
        [Test]
        public void NonParameterisedTestsKeepStaticsIsolatedThree()
        {
            const string constant = "Three";

            StaticValue = constant;

            Thread.Sleep(1000);

            Assert.That(StaticValue, Is.EqualTo(constant));
        }

        /// <summary>
        /// The non parameterised tests keep statics isolated two.
        /// </summary>
        [Test]
        public void NonParameterisedTestsKeepStaticsIsolatedTwo()
        {
            const string constant = "Two";

            StaticValue = constant;

            Thread.Sleep(1000);

            Assert.That(StaticValue, Is.EqualTo(constant));
        }

        /// <summary>
        /// The parameterised tests keep statics isolated.
        /// </summary>
        /// <param name="param">
        /// The param.
        /// </param>
        [Test]
        [TestCase("1")]
        [TestCase("2")]
        [TestCase("3")]
        [TestCase("4")]
        [TestCase("5")]
        [TestCase("6")]
        [TestCase("7")]
        [TestCase("8")]
        [TestCase("9")]
        [TestCase("a")]
        [TestCase("b")]
        [TestCase("c")]
        [TestCase("d")]
        [TestCase("e")]
        [TestCase("g")]
        [TestCase("h")]
        [TestCase("i")]
        [TestCase("j")]
        [TestCase("k")]
        [TestCase("l")]
        [TestCase("m")]
        public void ParameterisedTestsKeepStaticsIsolated(string param)
        {
            StaticValue = param;

            Thread.Sleep(1000);

            Assert.That(StaticValue, Is.EqualTo(param));
        }

        #endregion
    }
}