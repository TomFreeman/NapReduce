module Nunit

    open System.Reflection
    open System.IO
    open System.Diagnostics
    open System.Threading
    open System.Xml.Linq
    open System.Xml.XPath
    
    open NUnit.Framework

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

    open System
    open System.Diagnostics

    let parseMethods assembly category =
        let assem = System.Reflection.Assembly.LoadFrom(assembly)

        let methods = assem.GetModules()
                      |> Seq.map (fun m -> m.GetTypes())
                      |> Seq.concat
                      |> Seq.map (fun t -> t.GetMethods(BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly))
                      |> Seq.concat
                      |> Seq.filter (fun m -> not (m.GetCustomAttribute<TestAttribute>(true) = null))

        let filtered = match category with
                        | Some(catFilter) -> methods |> Seq.filter (fun m -> let cats = m.GetCustomAttributes<CategoryAttribute>(true)
                                                                             if cats = null then
                                                                                 false
                                                                             else
                                                                                 cats |> Seq.exists (fun cat -> cat.Name = catFilter ))
                        | None -> methods

        filtered |> Seq.map (fun m -> m.ReflectedType.FullName + "." + m.Name )

    let runNunit assembly test =
        async
            {
            let id = System.Guid.NewGuid().ToString()
            let proc = new Process()
            proc.StartInfo.FileName <- @"c:\Program Files (x86)\NUnit 2.6.2\bin\nunit-console-x86.exe"
            proc.StartInfo.Arguments <- assembly + @" /run=" + test + @" /result=" + id
//            proc.StartInfo.RedirectStandardOutput <- true
//            proc.StartInfo.RedirectStandardError <- true
//            proc.StartInfo.UseShellExecute <- false
//            proc.StartInfo.CreateNoWindow <- false

            let reused = proc.Start()            

            let! response = proc.AsyncWaitForExit()

            return if File.Exists(id) then
                        Some(File.ReadAllText(id))
                   else
                        None
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

    let reduceNunit results =
        let mergeDocs (doc1:XDocument) (doc2:XDocument) =
            if doc1.Root = null || doc2.Root = null then
                doc2
            else
                doMergeResults doc1.Root doc2.Root
                correctAttributes doc1
                doc1
        results |> Seq.fold mergeDocs (new XDocument())