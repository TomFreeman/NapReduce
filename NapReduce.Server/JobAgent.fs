﻿module JobAgent

    open System
    open System.Threading

    open NapReduce.Common
    open NapReduce.Tasks

    type Agent<'T> = MailboxProcessor<'T>

    type State = {
        JobId : Guid
        Available : Test list
        Taken : Test list
        ForRetry : Test list
        Complete : Test list
    }

    let (|Incomplete|_|) state =
        match state.Available with
        | x :: xs -> Some(xs, x :: state.Taken, x)
        | [] -> None

    let (|Complete|_|) state =
        if (List.isEmpty state.Available) && List.isEmpty (state.ForRetry) && List.isEmpty (state.Taken) then
            Some state
        else
            None

    type JobProcessor(job) =

        let complete = new Event<State>()

        let feedback = new Event<State>()

        let error = new Event<State>()

        let finish = async {()}

        let task = new NapReduce.Tasks.TasksCommsProxy()

        let startJob job =
            Agent<TaskMessage>.Start(fun proc ->
                let rec loop (state:State) = 
                    async {
                        task.TasksAvailable(state.JobId, proc.Post)

                        feedback.Trigger state

                        let! msg = proc.Receive()

                        return!
                            match msg with
                            | ListAvailable -> task.TasksAvailable(state.JobId, proc.Post)
                                               loop state
                            | TemporaryFail test -> 
                                let newState = { 
                                    state with
                                        Taken = List.filter (fun t -> not (t = test)) state.Taken
                                        ForRetry = test :: state.ForRetry }
                                loop newState
                            | Take ->
                                match state with
                                | Incomplete (av, taken, task) ->
                                    let newState = { 
                                        state with
                                            Available = av;
                                            Taken = taken; }
                                    loop newState
                                | Complete state -> complete.Trigger state
                                                    finish 
                                | _ -> error.Trigger state
                                       finish
                            | Completed test ->
                                let newState = { 
                                    state with
                                        Taken = List.filter (fun t -> not (t = test)) state.Taken
                                        Complete = test :: state.Complete }
                                match newState with
                                | Complete st -> complete.Trigger state
                                                 finish
                                | _ -> loop newState
                            
                        
                    }
                loop job
            )

        member x.Start ()=
            let agent = startJob job
            agent.Error.Add(fun err -> System.Console.WriteLine("Horrible News!"))