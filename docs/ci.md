# Continuous integration

The Flight core workflow runs on pull requests targeting `main` and pushes to
`main`. If `FlightCore.Tests/FlightCore.Tests.csproj` is absent, the job reports
that no flight tests ran and succeeds. Once the project is present, it installs
the .NET 9 SDK and runs `dotnet run --project FlightCore.Tests -c Release`.
The SDK matches the test project's `net9.0` target; update the workflow if that
target changes. A passing job while the project is absent is not test coverage.

The workflow does not build or test Unity and does not use Unity license secrets.
Run the same command locally from the repository root with the .NET 9 SDK after
the flight core and its test project are available.
