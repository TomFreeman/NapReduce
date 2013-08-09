open System

open Ionic.Zip
open NunitAssemblyParser
open CommandLine

type Argument() = 
    let makeOption v =
        if v = null then
            None
        else
            Some(v)

    [<Option('a', "assembly", Required=true, HelpText="Input assembly containing Nunit tests.")>]
    member val Assembly = "" with get, set
    [<Option('c', "category", DefaultValue=null, HelpText="Optionally provide a category")>]
    member val Cat = "" with get, set
    [<Option('a', "additional-files", DefaultValue=null, HelpText="Optionally provide extra files that can't be resolved as direct dependencies")>]
    member val Extras = "" with get, set
    [<Option('o', "ouptut-file", Required=true, HelpText="Provide the name for an output file.")>]
    member val Out = "" with get, set

    member this.AdditionalFiles 
        with get () = 
            if this.Extras = null then
                None
            else
                Some(this.Extras.Split([|','|], StringSplitOptions.RemoveEmptyEntries))

    member this.Category 
        with get () = makeOption this.Cat

    member this.OutputFile
        with get () = makeOption this.Out

let run argv =
    let args = new Argument()

    if not (Parser.Default.ParseArguments(argv, args)) then
        failwith "Couldn't parse arguments."

    let methods, assemblies = parseMethods args.Assembly args.Category

    let zipFileName = System.Guid.NewGuid().ToString() + ".zip"
    let zipFile = new ZipFile(zipFileName)

    // Grr... No methods to determine relative path on IO.Path
    let currentDir = IO.Directory.GetCurrentDirectory() + @"\"
    let additionalFiles = 
        match args.AdditionalFiles with
        | None -> assemblies
        | Some(files) -> files |> Array.append assemblies
        |> Array.map (fun path -> path.Replace(currentDir, "") )

    zipFile.AddFiles(additionalFiles, true, null)

    zipFile.Save()


[<EntryPoint>]
let main argv = 
    try
        do run argv
        0
    with
        | ex -> Console.WriteLine(ex.Message) 
                -1