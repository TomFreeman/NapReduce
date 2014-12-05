module NapReduce.Tasks

    open System

    open Microsoft.AspNet.SignalR
    open Microsoft.AspNet.SignalR.Hubs

    open EkonBenefits.FSharp.Dynamic

    open NapReduce.Common
    
    type TaskMessage =
    | ListAvailable
    | Take 
    | Completed of Test
    | TemporaryFail of Test

    type poster = Action<TaskMessage>

    let groups = new System.Collections.Concurrent.ConcurrentDictionary<Guid, poster>()

    [<HubName("tasks")>]
    type TasksHub() =
        inherit Hub()

        do System.Console.WriteLine("Created tasks hub")

        member x.TakeJob () =
            ()

        member x.FinishJob(result : Test) =
            let success, post = groups.TryGetValue(result.JobId)

            post.Invoke(Completed(result))
            
    type TasksCommsProxy() =

        member this.TasksAvailable(jobId:Guid, post:TaskMessage -> unit) =
            let context = GlobalHost.ConnectionManager.GetHubContext<TasksHub>()    

            let post = new Action<TaskMessage>(post)
            let updater id poster = post

            groups.AddOrUpdate(jobId, (fun id -> post), updater) |> ignore

            context.Clients.All?jobsAvailable(jobId)
