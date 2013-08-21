module Nunit

    open System
    open System.Diagnostics
    open System.IO
    open System.Diagnostics
    open System.Xml.Linq
    open System.Xml.XPath
    
    /// <summary>
    /// An extension method to wrap waiting for an external process up in an async handler.
    /// </summary>
    type Process with
        member this.AsyncWaitForExit( ?millisecondsTimeout ) =
            async 
                {
                    use h = new System.Threading.EventWaitHandle( false, System.Threading.EventResetMode.ManualReset )
                    h.SafeWaitHandle <- new Microsoft.Win32.SafeHandles.SafeWaitHandle( this.Handle, true )
                    match millisecondsTimeout with
                    | Some(ms)  -> return! Async.AwaitWaitHandle( h, ms )
                    | None      -> return! Async.AwaitWaitHandle( h )
                }

    // Fix up the attributes in the merged xml document
    let correctAttributes (document:XDocument) =
        let countAttributes attributeName expectedValue =
            document.Descendants(XName.Get("test-case")) |> Seq.fold (fun count node -> let target = node.Attribute(XName.Get(attributeName))
                                                                                        if not (target = null) && target.Value = expectedValue then 
                                                                                          count + 1
                                                                                        else
                                                                                          count ) 0
        document.Root.SetAttributeValue(XName.Get("failures"), (countAttributes "result" "Failure"))
        document.Root.SetAttributeValue(XName.Get("not-run"), (countAttributes "executed" "False"))
        document.Root.SetAttributeValue(XName.Get("ignored"), (countAttributes "result" "Ignored"))
        document.Root.SetAttributeValue(XName.Get("inconclusive"), (countAttributes "result" "Inconclusive"))
        document.Root.SetAttributeValue(XName.Get("total"), Seq.length (document.Descendants(XName.Get("test-case"))))
        document

    let generateFakeFailure assembly test (stdout:string) (stderr:string) =

        let ``test-suite`` = XName.Get("test-suite")
        let ``results`` = XName.Get("results")
        let ``type`` = XName.Get("type")
        let ``name`` = XName.Get("name")
        let ``failure`` = XName.Get("failure")
        let ``message`` = XName.Get("message")
        let ``test-case`` = XName.Get("test-case")

        new XDocument(
            new XElement(XName.Get("test-results"),
                new XElement(``test-suite``,
                    new XAttribute(``type``, "Assembly"),
                    new XAttribute(``name``, assembly),
                    new XElement(``results``,
                        new XAttribute(``type``, "Namespace"),
                        new XAttribute(``name``, System.Guid.NewGuid()),
                        new XElement(``test-case``,
                            new XAttribute(``name``, test),
                            new XElement(``failure``,
                                new XComment(stdout),
                                new XComment(stderr)))))))
        |> correctAttributes
            
    /// <summary>
    /// Runs the nunit test
    /// </summary>
    let runNunit assembly folder test username password (logger : string -> unit) =
        async
            {

            logger (sprintf "Starting Nunit in %s\n" folder)

            let id = System.Guid.NewGuid().ToString()
            use proc = new Process()
            proc.StartInfo.FileName <- @"c:\Program Files (x86)\NUnit 2.6.2\bin\nunit-console-x86.exe"
            proc.StartInfo.Arguments <- assembly + @" /run=" + test + @" /result=" + id + @" /noshadow"

            logger (sprintf "Starting process: %s\n\twith arguments: %s" proc.StartInfo.FileName proc.StartInfo.Arguments )

            let log sender (args : DataReceivedEventArgs ) =
                logger(args.Data)
                
            proc.OutputDataReceived.AddHandler(new DataReceivedEventHandler(log))

            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.RedirectStandardError <- true
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.UserName <- username
            proc.StartInfo.Password <- password
            proc.StartInfo.WorkingDirectory <- folder
            //proc.StartInfo.CreateNoWindow <- true

            proc.Start() |> ignore
            proc.BeginOutputReadLine()

            let! response = proc.AsyncWaitForExit()

            // If the results file exists, return that, otherwise return the output from the test to aid debugging
            // TODO: Somehow get the output from the test into the XML - so we can see what happened.
            return if File.Exists(id) then
                        File.ReadAllText(id), None
                   else
////                        let text = proc.StandardOutput.ReadToEnd()
////                        failwith text
////                        None
                        let stdout = proc.StandardOutput.ReadToEnd()
                        let stderr =  proc.StandardError.ReadToEnd()
                        let errorDoc = generateFakeFailure assembly test stdout stderr
                        errorDoc.ToString(), Some(stderr)
            }

    // Mutates node1 recursively! - beware!
    let rec doMergeResults (node1:XElement) (node2:XElement) =

        if not (node1 = null) && not (node2 = null) then
            let suiteName = XName.Get("test-suite")
            for suite in node2.Elements(suiteName) do
                match node1.Elements(suiteName) |>
                      Seq.tryFind (fun node -> suite.Attribute(XName.Get("type")).Value = node.Attribute(XName.Get("type")).Value &&
                                               suite.Attribute(XName.Get("name")).Value = node.Attribute(XName.Get("name")).Value ) with
                      | None -> // No matching element, so add it as a new one
                                node1.Add(suite)
                      | Some(n) -> doMergeResults (n.Element(XName.Get("results"))) (suite.Element(XName.Get("results")))

            // Assuming no duplicate results ATM, potentially unsafe assumption
            // Also, could potentially lose data if results2 isn't null, but results1 is...
            for case in node2.Elements(XName.Get("test-case")) do node1.Add(case)

    // Given a list of XDocuments representing a set of Nunit results, merge them all together
    let reduceNunit results =
        let mergeDocs (doc1:XDocument) (doc2:XDocument) =
            if doc1.Root = null then
                doc2
            elif doc2.Root = null then
                doc1
            else
                doMergeResults doc1.Root doc2.Root
                correctAttributes doc1
        results |> Seq.fold mergeDocs (new XDocument())