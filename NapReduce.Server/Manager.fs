module Manager

open NapReduce.Common
open JobAgent

open System

let createJob tasks = {
    Available = tasks;
    Taken = [];
    Complete = [];
    ForRetry = []; }

let createCodedUITestList job =
    Name("Test One") :: Name("Test Two") :: []
    |> createJob

let StartNunitJob job =
    ()

let StartCodedUIJob job =
    let task = createCodedUITestList job
    let agent = new JobProcessor(task)
    agent.Start()

let Start (job:Job) =
    match job.Type with
    | Nunit -> StartNunitJob job
    | CodedUI -> StartCodedUIJob job