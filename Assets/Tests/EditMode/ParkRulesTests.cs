using FocusPark.Core;
using NUnit.Framework;
using UnityEngine;

namespace FocusPark.Tests
{
    public sealed class ParkRulesTests
    {
        [TestCaseSource(typeof(CoreContractChecks), nameof(CoreContractChecks.Names))]
        public void CoreContract(string name)
        {
            CoreContractChecks.Run(name, state => JsonUtility.FromJson<ParkState>(JsonUtility.ToJson(state)));
        }
    }
}
