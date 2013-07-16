module Nunit

    open System
    open System.Diagnostics
    open System.Reflection
    open System.IO
    open System.Diagnostics
    open System.Threading
    open System.Xml.Linq
    open System.Xml.XPath
    
    open NUnit.Framework

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

    /// <summary>
    /// Parses a given test assembly, looking for tests
    /// </summary>
    /// <remarks>
    /// The category filter is optional, without it all the tests will be returned.
    /// </remarks>
    let parseMethods assembly category =

        // When loading the assembly by reflection, dependencies are automatically resolved
        // so loader is attached to the event and resolves the assemblies for reflection
        let loader (sender:obj) (args:ResolveEventArgs) =
            printf "Attempting to load %s for dependency %s" args.Name args.RequestingAssembly.FullName

            let assemblyName = args.Name.Remove(args.Name.IndexOf(",")) + ".dll"
            let directory = IO.Path.GetDirectoryName(args.RequestingAssembly.Location)

            // Maybe just check for the assembly file, rather than throwing an exception?
            try
                let assemblyPath = IO.Path.Combine(directory, assemblyName)
                System.Reflection.Assembly.ReflectionOnlyLoadFrom(assemblyPath)
            with
            | _ -> System.Reflection.Assembly.ReflectionOnlyLoad(args.Name)

        // Attach loader to the assembly resolution method
        AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve(new ResolveEventHandler(loader))

        // Load the test assembly for reflection
        let assem = System.Reflection.Assembly.ReflectionOnlyLoadFrom(assembly)

        // Get the referenced assemblies (annoyingly, this is worked out in loader, but that's executed on another thread or something
        // so can't be persuaded to share what it knows
        let assemblyPath = IO.Path.GetDirectoryName(assem.Location)
        let assembliesNeeded = assem.GetReferencedAssemblies() 
                               |> Seq.map (fun a -> IO.Path.Combine(assemblyPath, a.Name + ".dll") )
                               |> Seq.filter IO.File.Exists
                               |> Seq.toArray

        // Find all the methods marked with a TestAttribute, can't match on the exact type
        // as we may be looking at an x86 assembly, or an x64 one
        let methods = assem.GetModules()
                      |> Seq.map (fun m -> m.GetTypes())
                      |> Seq.concat
                      |> Seq.map (fun t -> t.GetMethods(BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly))
                      |> Seq.concat
                      |> Seq.filter (fun m -> m.GetCustomAttributesData() 
                                              |> Seq.exists (fun data -> data.AttributeType.Name = "TestAttribute" ))

        // Indenting warnings, but the alternative is unreadable
        let filtered = match category with
            | Some(catFilter) -> methods 
                                 |> Seq.filter (fun m -> m.GetCustomAttributesData()
                                                         |> Seq.exists (fun cat -> cat.AttributeType.Name = "CategoryAttribute" && 
                                                                cat.ConstructorArguments 
                                                                |> Seq.exists (fun v -> v.ArgumentType = typeof<string> &&
                                                                                    (v.Value :?> string) = catFilter )) )
            | None -> methods

        // return the list of tests, and all required assemblies (excluding the test assembly)
        filtered 
        |> Seq.map (fun m -> m.ReflectedType.FullName + "." + m.Name )
        |> fun methods -> methods, assembliesNeeded

    let runNunit assembly test =
        async
            {
            let id = System.Guid.NewGuid().ToString()
            let proc = new Process()
            proc.StartInfo.FileName <- @"c:\Program Files (x86)\NUnit 2.6.2\bin\nunit-console-x86.exe"
            proc.StartInfo.Arguments <- assembly + @" /run=" + test + @" /result=" + id
            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.RedirectStandardError <- true
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.CreateNoWindow <- false

            proc.Start() |> ignore

            let! response = proc.AsyncWaitForExit()

            // If the results file exists, return that, otherwise return the output from the test to aid debugging
            // TODO: Somehow get the output from the test into the XML - so we can see what happened.
            return if File.Exists(id) then
                        Some(File.ReadAllText(id))
                   else
                        let text = proc.StandardOutput.ReadToEnd()
                        failwith text
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

    // Given a list of XDocuments representing a set of Nunit results, merge them all together
    let reduceNunit results =
        let mergeDocs (doc1:XDocument) (doc2:XDocument) =
            if doc1.Root = null || doc2.Root = null then
                doc2
            else
                doMergeResults doc1.Root doc2.Root
                correctAttributes doc1
                doc1
        results |> Seq.fold mergeDocs (new XDocument())