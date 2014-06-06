namespace NapReduce.Tasks

    open Microsoft.AspNet.SignalR
    open Microsoft.AspNet.SignalR.Hubs

    open EkonBenefits.FSharp.Dynamic

    open NapReduce.Common

    [<HubName("tasks")>]
    type TasksHub() =
        inherit Hub()

        do System.Console.WriteLine("Created tasks hub")

        member x.TakeJob (task : Job) =
            ()

        member x.FinishJob(result : Result) =
            ()

        member x.TasksAvailable() =
            x.Clients.All?jobsAvailable()
            
    type TasksCommsProxy() =
        member this.TasksAvailable() =
            let context = GlobalHost.ConnectionManager.GetHubContext<TasksHub>()    
            context.Clients.All?jobsAvailable(1)
