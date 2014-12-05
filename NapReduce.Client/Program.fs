module Program

    open System

    open Microsoft.AspNet.SignalR
    open Microsoft.AspNet.SignalR.Client

    let Connect hostUrl =
        let connection = new HubConnection(hostUrl)

        connection.TraceLevel <- TraceLevels.All
        connection.TraceWriter <- System.Console.Out

        let proxy = connection.CreateHubProxy("tasks")

        let jobs = proxy.On<Guid>("jobsAvailable", (fun id -> System.Console.WriteLine("Tasks Available in {0}", id)))

        connection.Start().Wait()

        jobs

    [<EntryPoint>]
    let main argv = 
        let hostUrl = "http://localhost:8085"
    
        use jobs = Connect hostUrl

        System.Console.ReadLine() |> ignore
        
        0
