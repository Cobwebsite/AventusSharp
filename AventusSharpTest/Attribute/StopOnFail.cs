using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System;

namespace AventusSharpTest.Attribute
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class StopOnFail : NUnitAttribute, ITestAction
    {
        public void BeforeTest(ITest test)
        {
        }

        public void AfterTest(ITest test)
        {
            TestResult testResult = TestExecutionContext.CurrentContext.CurrentResult;

            if (testResult.ResultState == ResultState.Failure || testResult.ResultState == ResultState.Error)
            {
                TestExecutionContext.CurrentContext.ExecutionStatus = TestExecutionStatus.StopRequested;
            }
        }

        public ActionTargets Targets => ActionTargets.Test;
    }
}