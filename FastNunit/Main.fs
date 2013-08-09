open System
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
    member val Out = "" with get, set

    member this.Category 
        with get () = makeOption this.Cat

    member this.OutputFile
        with get () = makeOption this.Out

let run (args : arg) =
    
    let listener = mailboxLoop

    let userManager = new UserManager()

    let methods, assemblies = parseMethods args.Assembly args.Category

    // Do the tests
    let rec doTests testList currentJob =
        match testList with
        | (assembly, test) :: remaining -> 
            match userManager.GetFree() with
            | Some(username, password) -> 
                async {
                    do! userManager.AssignUser(username)
                    let! result, errLog = runNunit assembly test username password ignore
                    do! userManager.FreeUser(username)
                    result
                    |> XDocument.Parse
                    |> ProcessTestResult
                    |> mailboxLoop.Post }
                |> fun job -> job :: [currentJob]
                |> fun task -> doTests remaining (Async.Parallel(task) |> Async.Ignore)
            | None -> System.Threading.Thread.Sleep(10000)
                      doTests ((assembly, test) :: remaining) currentJob
        | [] -> currentJob

    let testsToRun = methods
                     |> Seq.map (fun t -> t, args.Assembly)
                     |> Seq.toList

    doTests testsToRun (async { return () })
    |> Async.RunSynchronously

    let result = listener.PostAndReply GetResults
    result.Save("results.xml")

// Generic entry point, just to wrap args parsing and exception logging
[<EntryPoint>]
let main argv = 
    
    let args = new arg()
    if not (CommandLine.Parser.Default.ParseArguments(argv, args)) then
        failwith "couldn't parse the command line"

    try
        do run args
        0
    with
        | ex -> Console.WriteLine(ex.Message) 
                -1