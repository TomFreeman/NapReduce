module Manager

open NapReduce.Common
open JobAgent

open System

let createJob tasks = 
    let id = Guid.NewGuid()
    {
    JobId = id;
    Available = tasks |> List.map (fun t -> {Name = t; JobId = id});
    Taken = [];
    Complete = [];
    ForRetry = []; }

let createCodedUITestList job =
    "Test One" :: "Test Two" :: []
    |> createJob

let StartNunitJob job =
    let task = createCodedUITestList job
    let agent = new JobProcessor(task)
    agent.Start()
    agent

let StartCodedUIJob job =
    let task = createCodedUITestList job
    let agent = new JobProcessor(task)
    agent.Start()
    agent

let Start (job:Job) =
    match job.Type with
    | Nunit -> StartNunitJob job
    | CodedUI -> StartCodedUIJob job