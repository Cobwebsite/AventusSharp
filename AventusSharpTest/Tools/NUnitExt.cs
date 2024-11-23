using AventusSharp.Data;
using AventusSharp.Tools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharpTest.Tools
{
    class NUnitExt
    {
        public static void AssertNoError(IWithError result)
        {
            Assert.IsTrue(result.Errors.Count == 0, string.Join(", ", result.Errors.Select(e => e.Message)));
        }
        public static void AssertNoError(List<GenericError> errors)
        {
            bool isTrue = errors == null || errors.Count == 0;
            Assert.IsTrue(isTrue, string.Join(", ", errors.Select(e => e.Message)));
        }

        public static void AssertError(IWithError result)
        {
            Assert.IsTrue(result.Errors.Count != 0);
        }
        public static void AssertError(List<GenericError> errors)
        {
            bool isTrue = errors != null && errors.Count > 0;
            Assert.IsTrue(isTrue);
        }
    }
}
