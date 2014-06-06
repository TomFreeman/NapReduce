namespace Program

    open Owin
    open Microsoft.AspNet.SignalR
    open Microsoft.AspNet.SignalR.Hubs
    open Microsoft.Owin.Hosting
    open Microsoft.Owin
    open System
    open System.Diagnostics
    open EkonBenefits.FSharp.Dynamic

    open Manager
    open NapReduce.Common
    open NapReduce.Tasks

    [<HubName("jobs")>]
    type JobsHub() =
       inherit Hub()
       member x.SubmitJob (job : Job) =
           Start job

    type MyWebStartUp() =
        member x.Configuration (app :IAppBuilder) =
           app.MapSignalR() |> ignore
           ()

    module Starter =

    [<EntryPoint>]
    let main argv = 
        let hostUrl = "http://localhost:8085"
        use app = WebApp.Start<MyWebStartUp>(hostUrl)
        Console.WriteLine("Server running on "+ hostUrl)
        Console.ReadLine() |> ignore

        0