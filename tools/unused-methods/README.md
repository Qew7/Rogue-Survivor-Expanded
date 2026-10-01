# Unused private methods

Run the audit against the portable build's exact source list and Mono reference
assemblies:

```sh
docker build --target build -t rogue-unused-audit .
docker run --rm -v "$PWD/tools/unused-methods:/audit" -w /audit rogue-unused-audit \
  dotnet run --project UnusedMethods.csproj -- \
  /src/WRogue/RogueSurvivor.Portable.csproj /usr/lib/mono/4.5-api
```

The tool requires a compilation without errors, then resolves references to
private methods with Roslyn symbols. It excludes explicit interface methods and
attributed callbacks. Its output is a candidate list, not an automatic delete
list: check reflection calls (including tests), serialization, and inactive
preprocessor branches before removing a method. Public and protected APIs are
outside the audit because mods and subclass implementations may call them.
