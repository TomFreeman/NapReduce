﻿open System
open Nunit
open NunitAssemblyParser
open Ionic.Zip
open System.Xml.Linq
open CommandLine
open userManagement

type MessageForActor = 
   | ProcessTestResult of XDocument
   | GetResults of (XDocument) AsyncReplyChannel

let mailboxLoop =
   MailboxProcessor.Start(fun inbox ->
      let rec loop acc =
         async {
            let! message = inbox.Receive()

            match message with
            | ProcessTestResult (result) ->
                return! loop (reduceNunit (result :: [acc]))
            | GetResults replyChannel ->
               replyChannel.Reply(acc)
         }
      loop (new XDocument())
      )

type arg() = 
    let makeOption v =
        if v = null then
            None
        else
            Some(v)

    [<Option('a', "assembly", Required=true, HelpText="Input assembly containing Nunit tests.")>]
    member val Assembly = "" with get, set

    [<Option('c', "category", DefaultValue=null, HelpText="Optionally provide a category")>]
    member val Cat = "" with get, set

    [<Option('o', "ouptut-file", Required=true, HelpText="Provide the name for an output file.")>]
    member val OutputFile = "" with get, set

    [<Option('i', "session-id", Required=true, HelpText="Need a sessionId for these tests")>]
    member val SessionId = "" with get, set

    [<Option('w', "working-directory", Required=true, HelpText="Provide a working directory for the process.")>]
    member val WorkingDirectory = "" with get, set

    member this.Category 
        with get () = makeOption this.Cat

let run (args : arg) =
    
    IO.Directory.SetCurrentDirectory(args.WorkingDirectory)

    let listener = mailboxLoop

    let userManager = new UserManager()

    let methods, assemblies = parseMethods args.Assembly args.Category

    let testsToRun = methods
                     |> Seq.map (fun t -> t, args.WorkingDirectory, args.Assembly)
                     |> Seq.toList

    testsToRun
    |> Seq.iter (fun (test, _, _) -> printf "Queueing %s to be run\n" test )

    // Do the tests
    let doTests testList =
        testList
        |> Seq.map (fun (test, folder, assembly) ->
                            let rec doTest() =
                                // Try to get a user - potentially could be more than an Option is the service is down / the client is misbehaving
                                match userManager.GetFree(args.SessionId) with
                                | Success(username, password) ->
                                                            async { 
                                    try
                                        Console.WriteLine("Acquired user: {0} - starting test run.", username)
                                        do! userManager.AssignUser(username)
                                        let! result, errLog = runNunit assembly folder test username password (Console.WriteLine)
                                        Console.WriteLine("Results Come through: {0}, posting to queue to merge.")
                                        Console.WriteLine("Finished test run, freeing user: {0}.", username)
                                        do! userManager.FreeUser(username)

                                        result
                                        |> XDocument.Parse
                                        |> ProcessTestResult
                                        |> mailboxLoop.Post
                                    with
                                    | ex -> do! userManager.FreeUser(username)
                                            raise ex
                                    }
                                | NotFound -> async {
                                                System.Threading.Thread.Sleep(10000)
                                                do! doTest() }
                                | Error(ex) -> raise ex 
                            doTest() )

    doTests testsToRun
    |> Seq.toArray
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

    let result = listener.PostAndReply GetResults
    Console.WriteLine("Saving merged results.")
    result.Save(args.OutputFile)

// Generic entry point, just to wrap args parsing and exception logging
[<EntryPoint>]
let main argv = 
    
    let args = new arg()
    let showHelp () =
        let help = CommandLine.Text.HelpText.AutoBuild(args)
        Console.Write (help.ToString())
        let read = Console.ReadLine();
        -1
    
    if not (CommandLine.Parser.Default.ParseArguments(argv, args)) then
        showHelp()
    else
        try
            do run args
            0
        with
            | ex -> Console.WriteLine(ex.Message)
                    Console.WriteLine(ex.StackTrace)
                    let read = Console.ReadLine();
                    -1