Run the game's shared domain contracts without installing Unity:

```sh
dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release
```

This requires the .NET 8 SDK and no NuGet packages. It links the actual `ParkState.cs` rules and the same contract checks used by Unity's EditMode tests. For environments where the compiler server is unavailable, add `-p:UseSharedCompilation=false`.

The 15 contracts cover session expiry and recovery, exactly-once rewards, early finishing, input validation, purchase persistence, placement constraints, and practice statistics. The standalone runner uses System.Text.Json to exercise serialized roundtrips; the Unity adapter uses JsonUtility. Run Unity's EditMode tests to verify the Unity serializer and engine environment. These checks do not validate native device enforcement or game rendering.
