[![Build status](https://ci.appveyor.com/api/projects/status/s1cushw54324ngjm?svg=true)](https://ci.appveyor.com/project/dvoits/z3test)

This repository holds test infrastructure and benchmarks used to test Z3. 

*Performance test* is measurement of execution of a target command line executable file for specific input file
with certain command line parameters.
For example, a performance test for Z3 is execution of `z3.exe` for the given parameters and certain smt2 file.

An input file is called *benchmark*.

An *experiment* is a set of performance tests for a single target executable, same command line parameters and run for multiple benchmarks located in the predefined directory.

Regular experiments allow to track how changes in the source codes affect the target executable performance on same set of benchmarks. 

# Azure-based performance tests

## Experiments and results storage

## Running performance tests




# Structure of the repository

* `/PerformanceTests.sln` is a Visual Studio 2015 solution which contains following projects:
  * `PerformanceTests.Management` is a WPF application to view, manage and submit performance 
  experiments.
  * `AzurePerformanceTest` is a .NET class library holding the `AzureExperimentManager` class which exposes API to manage experiments based on Microsoft Azure.
  * `PerformanceTest` is a .NET class library containing abstract types for experiments management.
  * `Measurement` is a .NET class library allowing to measure process run time and memory usage.
  * `Z3Domain` is a .NET class library implementing Z3-specific analysis of the program execution.
  * `AzureWorker` is a command line application that is run on Azure Batch nodes to prepare and execute performance tests.
  * `AzurePerformanceTestCommons` is a .NET class library that contains functionality for Azure-based experiment management and is shared by `AzurePerformanceTest` and `AzureWorker`.

* `/ImportData.sln` is a Visual Studio 2015 solution which allows to import experiments results from old format to Azure storage.

* `/src/` contains Visual Studio projects included to the Visual Studio solutions.

* `/tests/` contain Visual Studio unit test projects included to the Visual Studio solutions.

# Build and test 

## Build requirements

## How to build

# Deploy

## Update Azure Batch worker

## Setup Nightly performance tests

The .NET application `/src/NightlyRunner` allows to submit performance tests for the latest nightly build of Z3. 
It does the following:

1. Finds most recent x86 binary package at https://github.com/Z3Prover/bin/tree/master/nightly. If there are multiple files found, takes commit sha from the file names and looks to the commit history of the Z3 repository to determine which is most recent.
2. Finds the last nightly performance experiment.
3. If the most recent build differs from the last experiment executable, does the following:
  
    1. Uploads new x86 z3 binary package to the blob container `bin` and sets its metadata attribute to the original file name of the package.
    2. Submits new performance experiment.


### How to schedule nightly runs using Azure Batch Schedule

1. Create Azure Batch Application for `NightlyRunner`. 

    1. Open Batch account page at the Azure portal.
    2. Click `Feature/Applications` and then click `Add`.
    3. Compress NightlyRunner.exe, NightlyRunner.exe.config and all its \*.dll files to a zip file and select it as Application package.
    4. Click `OK` to create the application.
  
2. Schedule execution of the application. Open PowerShell and use the following commands to create new schedule:

```powershell

Login-AzureRmAccount

$ManagerTask = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSJobManagerTask"
$ManagerTask.ApplicationPackageReferences = $AppRefs
$ManagerTask.Id = "NightlyRunTask"
$ManagerTask.CommandLine = "NightlyRunner.exe"

$JobSpecification = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSJobSpecification"
$JobSpecification.JobManagerTask = $ManagerTask
$JobSpecification.PoolInformation = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSPoolInformation"
$JobSpecification.PoolInformation.PoolId = ...pool id...

$Schedule = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSSchedule"
$Schedule.RecurrenceInterval = [TimeSpan]::FromDays(1)

$BatchContext = Get-AzureRmBatchAccountKeys 
New-AzureBatchJobSchedule -Id "NighlyRunSchedule" -Schedule $Schedule -JobSpecification $JobSpecification -BatchContext $BatchContext
```



