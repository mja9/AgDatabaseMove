# Contributing

## Scope
The purpose of this project is to simplify the management of databases in an availability group with the initial
focus being on the ability to move SQL Server databases from one availability group to another. Any tools we
develop in this endeavor should be consumable by library or CLI consumers if reasonable.

This repository and FactSet's support of its development is based on being usable and maintainable for 
our internal projects and teams. If a change would be detrimental to that goal, we ask that it be 
developed in a separate fork.

## Bug Reports
If you find a bug, please file an issue on this repository. Since this project is based on SQL Server please
be descriptive of the environment that it's erroring in. Including as many relevant details as possible will
help us track it down successfully.

## Feature Requests
We understand that this is developed for our server architecture. If you have a different architecture you'd
like this tool to support we encourage you to file an issue. Hopefully you'll be interested in contributing
to help us get support added.

## Pull Requests
We look forward to accepting pull requests from the community. If you would like to contribute, we ask that
you open an issue before beginning any substantial work. Please work on a fork of the repo and submit the pull 
request from there.

## Getting Started

### Building
This project can be built in Visual Studio with support for dotnet core 2.0 or from the command line with:

`dotnet build`

### Unit Testing
ReSharper can run unit tests in the IDE or the tests can be run from the command line with:

`dotnet test AgDatabaseMove.Unit`

### Integration Testing
Since this project is predicated on having multiple SQL Server Enterprise edition instances and a windows server 
failover clustering for the availability groups we expect that most community members won't be able to run these 
tests yet. We hope to provide some automation in the future to allow for this to be done on your own AWS 
account, but for now we run this with a config.json within FactSet using our own infrastructure.

### Code Formatting
Our team at FactSet uses ReSharper to keep a consistent code style. This project doesn't qualify for a ReSharper
free open source license. If you don't have a license to use, we intend to set up the CI process to validate
the rules on PR.
