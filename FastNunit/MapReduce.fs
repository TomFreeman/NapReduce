namespace MapReduce

open MapReduceHelpers
open Nunit
open System.Xml.Linq
open Microsoft.Hadoop.MapReduce
open Microsoft.Hadoop.MapReduce.Json

open Microsoft.Hadoop.MapReduce.HdfsExtras.Hdfs

type Test() =
    member val Assembly = "" with get, set
    member val Test = "" with get, set

type NunitMapper() =
    inherit JsonInMapperBase<Test>()

    override this.Map(input : Test, context : MapperContext) =

        context.Log("Test file :" + context.InputFilename)
        context.Log(sprintf "Started running test %s for %s" input.Test input.Assembly)
        
        let task = async
                        {
                            context.Log(sprintf "Attempting to run test %s from assembly %s" input.Test input.Assembly)
                            
                            let! result = runNunit input.Assembly input.Test

                            match result with
                            | Some(res) -> context.EmitKeyValue(input.Assembly, toB64Encoded res)
                            | None -> context.Log("test failed abnormally")
                                      // TODO: Create phoney failure? Or trigger a re-run somehow?
                        } 
        Async.RunSynchronously task

type NunitReducer() =
    inherit ReducerCombinerBase()

    override this.Reduce(key, values, context) =
        context.Log("Performing reduce on key : " + key)

        let docs = values 
                    |> Seq.map fromB64Encoded
                    |> Seq.map (fun text -> XDocument.Parse(text))
        let reduction = docs |> reduceNunit

        context.EmitKeyValue(key, toB64Encoded reduction)

type NunitJob(assemblyPath : string) =
    inherit HadoopJob<NunitMapper, NunitReducer, NunitReducer>()

    new() = NunitJob("")

    override this.Configure(context) = 
        let config = new HadoopJobConfiguration()
        config.DeleteOutputFolder <- true
        config.MaximumAttemptsMapper <- 2
        config.InputPath <- "input/nunit"
        config.OutputFolder <- "output/nunit"
        if not (System.String.IsNullOrEmpty(assemblyPath)) then config.FilesToInclude.Add(assemblyPath)
        config