open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Client

open NapReduce.Common

open System

let Connect hostUrl =
    async {
        let connection = new HubConnection(hostUrl)
        let proxy = connection.CreateHubProxy("jobs")

        use jobs = proxy.On("feedback", (fun () -> ()))

        connection.Start().Wait()

        let! result = Async.AwaitTask (proxy.Invoke<Result>("SubmitJob", {Type = JobType.CodedUI; PackagePath = new Uri("http://www.test.com/"); Tests = Category("ci")}))

        return ()
    }

[<EntryPoint>]
let main argv = 
    let hostUrl = "http://localhost:8085"
    
    Console.WriteLine "Press Enter to submit job."
    Console.ReadLine() |> ignore

    Async.Start (Connect hostUrl)

    Console.ReadLine() |> ignore

    0
