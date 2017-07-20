#!/bin/bash
dotnet --info
echo STARTED dotnet restore
dotnet restore -v m
echo STARTED dotnet build
dotnet build -c Release ${path} -v m
echo STARTED dotnet test
dotnet test -c Release ./BreezeCommon.Tests/BreezeCommon.Tests.csproj -v m