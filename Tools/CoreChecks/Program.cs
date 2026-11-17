using System;
using System.Text.Json;
using FocusPark.Core;
using FocusPark.Tests;

static class Program
{
    static int Main()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        int failures = 0;
        foreach (string name in CoreContractChecks.Names)
        {
            try
            {
                CoreContractChecks.Run(name, state => JsonSerializer.Deserialize<ParkState>(JsonSerializer.Serialize(state, options), options));
                Console.WriteLine("PASS " + name);
            }
            catch (Exception error)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name + ": " + error.Message);
            }
        }
        Console.WriteLine((CoreContractChecks.Names.Length - failures) + "/" + CoreContractChecks.Names.Length + " core contracts passed.");
        return failures == 0 ? 0 : 1;
    }
}
