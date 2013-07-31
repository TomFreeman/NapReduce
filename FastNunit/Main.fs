open System
open MapReduce
open MapReduceHelpers
open Nunit
open NunitAssemblyParser
open Microsoft.Hadoop.MapReduce
open Ionic.Zip

type arg = 
    {
        Assembly : string
        Category : string option
        AdditionalFiles : string [] option
    }

let parseArgs (arguments:string[]) =
    if arguments.Length = 0 then
        failwith "Command must be run with test assembly as a parameter."
    else
        { 
            Assembly = arguments.[0];
            Category = if arguments.Length > 1 then
                            Some arguments.[1]
                       else
                            None
            AdditionalFiles = if arguments.Length > 2 then
                                Some (Array.sub arguments 2 (arguments.Length - 2)
                                      |> Array.map (fun str -> str.Trim() ))
                              else
                                None
        }

// DO EVERYTHING!
let testCluster = new Uri("http://localhost:8084/")

let run (args : arg) =

    let hd = Hadoop.Connect()

    // Clean up the input files, in case something terrible has happened and we have invalid input that's not getting deleted
    hd.StorageSystem.Delete("input/nunit")

    // Store only the assembly name in the json representation
    let assemblyName = args.Assembly.Substring(args.Assembly.LastIndexOf(@"\") + 1)

    // Build out the list of tests to run
    let methods, assemblies = parseMethods args.Assembly args.Category
    let tests = methods
                |> Seq.map (fun name -> let test = new Test() 
                                        test.Assembly <- assemblyName
                                        test.Test <- name
                                        test |> Newtonsoft.Json.JsonConvert.SerializeObject )

    // Abuse map/reduce by creating hundreds of little files to bump up the mappers.
    tests 
    |> Seq.iteri (fun index test -> hd.StorageSystem.WriteAllText(sprintf "input/nunit/%d" index, test))

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
                       
    // Execute the job, including the dll
    let result = hd.MapReduceJob.ExecuteJob<NunitJob>(args.Assembly :: "4" :: [zipFileName] |> List.toArray)

    // Pull the results, and parse them back into an nunit xml file
    hd.StorageSystem.LsFiles("output/nunit")
    |> Seq.filter (fun path -> not (path.EndsWith("Success", System.StringComparison.OrdinalIgnoreCase)))
    |> (fun filteredList -> if (Seq.length filteredList) = 1 then                                
                                let out = hd.StorageSystem.ReadAllText(Seq.head filteredList) |> decodeKeyValueResult
                                IO.File.WriteAllText("result.xml", out)
                            else
                                failwith "Too many results files, reduction failed")    

// Generic entry point, just to wrap args parsing and exception logging
[<EntryPoint>]
let main argv = 
    
    let args = parseArgs argv

    try
        do run args
        0
    with
        | ex -> Console.WriteLine(ex.Message) 
                -1