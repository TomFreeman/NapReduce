module NunitAssemblyParser

    open System
    open System.Reflection
    open System.IO
    open System.Diagnostics
    open System.Threading
    
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
            printf "Attempting to load %s for dependency %s\n" args.Name args.RequestingAssembly.FullName

            let assemblyName = args.Name.Remove(args.Name.IndexOf(",")) + ".dll"
            let directory = IO.Path.GetDirectoryName(args.RequestingAssembly.Location)

            // Maybe just check for the assembly file, rather than throwing an exception?
            try
                let assemblyPath = IO.Path.Combine(directory, assemblyName)
                if IO.File.Exists(assemblyPath) then
                    System.Reflection.Assembly.ReflectionOnlyLoadFrom(assemblyPath)
                else
                    System.Reflection.Assembly.ReflectionOnlyLoad(args.Name)
            with
            | _ -> System.Reflection.Assembly.ReflectionOnlyLoad(args.Name)

        // Attach loader to the assembly resolution method
        AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve(new ResolveEventHandler(loader))

        printfn "Loading test assembly by reflection\n"
        let assem = System.Reflection.Assembly.ReflectionOnlyLoadFrom(assembly)

        // Get the referenced assemblies (annoyingly, this is worked out in loader, but that's executed on another thread or something
        // so can't be persuaded to share what it knows
        printfn "Determining assembly references to be packaged\n"
        let assemblyPath = IO.Path.GetDirectoryName(assem.Location)

        // resolve all the dependencies for the test dll...                
        let rec getDependencies (ass : Assembly) =
            ass.GetReferencedAssemblies()
            |> Seq.map (fun a -> a, IO.Path.Combine(assemblyPath, a.Name + ".dll") )
            |> Seq.filter (fun (a, p) -> IO.File.Exists(p))
            |> Seq.map (fun (a, p) -> System.Reflection.Assembly.ReflectionOnlyLoadFrom(p)
                                      |> getDependencies
                                      |> (fun ass -> a :: ass))
            |> List.concat
            |> fun list -> (ass.GetName()) :: list

        // Get the paths for the required assemblies.               
        let assembliesNeeded = assem 
                               |> getDependencies 
                               |> Seq.map (fun a -> IO.Path.Combine(assemblyPath, a.Name + ".dll") )
                               |> Seq.filter (fun path -> IO.File.Exists(path))
                               |> Seq.distinct
                               |> Seq.toArray

        printfn "Looking for tests\n"
        // Find all the methods marked with a TestAttribute, can't match on the exact type
        // as we may be looking at an x86 assembly, or an x64 one
        let methods = assem.GetModules()
                      |> Seq.map (fun m -> m.GetTypes())
                      |> Seq.concat
                      |> Seq.map (fun t -> t.GetMethods(BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly))
                      |> Seq.concat
                      |> Seq.filter (fun m -> m.GetCustomAttributesData() 
                                              |> Seq.exists (fun data -> data.AttributeType.Name = "TestAttribute" ))

        printfn "Filtering tests list\n"
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

    
