namespace MapReduce

open MapReduceHelpers
open Nunit
open System.Xml.Linq
open Microsoft.Hadoop.MapReduce
open Microsoft.Hadoop.MapReduce.Json
open Ionic.Zip

open Microsoft.Hadoop.MapReduce.HdfsExtras.Hdfs

/// <summary>
/// Represents a single test, used so that assembly and test can be sent through as JSON.
/// </summary>
type Test() =
    member val Assembly = "" with get, set
    member val Test = "" with get, set

/// <summary>
/// A type to run tests and produce results
/// </summary>
/// <remarks>
/// Because Hadoop operates on a line basis, and our Nunit results have whitespace in them
/// the entire Nunit result is base 64 encoded to fit on one line without losing whitespace
/// </remarks>
type NunitMapper() =
    inherit JsonInMapperBase<Test>()

    override this.Initialize(context) =
        // First job is to unzip the annoying zip file we had to create...
        System.IO.Directory.EnumerateFiles(System.IO.Directory.GetCurrentDirectory(), "*.zip")
        |> Seq.iter (fun path -> let file = ZipFile.Read(path)
                                 file.ExtractAll(".", ExtractExistingFileAction.OverwriteSilently) )

    override this.Map(input : Test, context : MapperContext) =

        context.Log("Test file :" + context.InputFilename)
        context.Log(sprintf "Started running test %s for %s" input.Test input.Assembly)
        
        let task = async
                        {
                            context.Log(sprintf "Attempting to run test %s from assembly %s" input.Test input.Assembly)
                            
                            try
                                let! result, stderr = runNunit input.Assembly input.Test context.Log

                                match stderr with
                                | Some(text) -> context.Log("-------------------------\n\rError Output: " + text + "\n\r-------------------------")
                                | None -> ()

                                match result with
                                | Some(res) -> context.EmitKeyValue(input.Assembly, toB64Encoded res)
                                | None -> context.Log("test failed abnormally")
                                          // TODO: Create phoney failure? Or trigger a re-run somehow?
                             with
                             | ex -> context.Log("test failed unexpectedly :" + ex.Message)
                        } 
        Async.RunSynchronously task

/// <summary>
/// A type to reduce test results, combines results from the mapper together to give a single result
/// </summary>
/// <remarks>
/// Because Hadoop operates on a line basis, and our Nunit results have whitespace in them
/// the entire Nunit result is base 64 encoded to fit on one line without losing whitespace.
/// Further, because we can run through the reducer more than once, the end result of the reducer
/// is still a key / value pair - although the final result should only have one value. And it is
/// still base 64 encoded.
/// </remarks>
type NunitReducer() =
    inherit ReducerCombinerBase()

    override this.Reduce(key, values, context) =
        context.Log("Performing reduce on key : " + key)

        let docs = values 
                    |> Seq.map fromB64Encoded
                    |> Seq.map (fun text -> XDocument.Parse(text))
        let reduction = docs |> reduceNunit

        context.EmitKeyValue(key, toB64Encoded reduction)


/// <summary>
/// Represents an Nunit map / reduce job.
/// </summary>
type NunitJob(assemblyPath, tasks, [<System.ParamArrayAttribute>] additionalFiles : string []) =
    inherit HadoopJob<NunitMapper, NunitReducer, NunitReducer>()

    new() = NunitJob("", "")

    override this.Configure(context) = 
        let config = new HadoopJobConfiguration()
        config.DeleteOutputFolder <- true
        config.InputPath <- "input/nunit"
        config.OutputFolder <- "output/nunit"

        config.MaximumAttemptsMapper <- 2
        config.Verbose <- true

        // Force the number of tasks, if we can.
        if not (System.String.IsNullOrEmpty(tasks)) then 
            config.AdditionalGenericArguments.Add("-D \"mapred.tasktracker.map.tasks.maximum=" +  tasks + "\"")

        config.AdditionalGenericArguments.Add("-D \"mapred.task.timeout=1200000\"")

        // Include the assembly, if must be added to HDFS outside of here... (ATM)
        if not (System.String.IsNullOrEmpty(assemblyPath)) then config.FilesToInclude.Add(assemblyPath)

        // Include the extra assemblies, which must be added to HDFS outside of here... (ATM)
        additionalFiles |> Seq.iter config.FilesToInclude.Add

        config