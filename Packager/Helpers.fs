module Helpers

    open System.Net

    let (==) opt1 opt2 =
        match opt1 with
        | Some(value1) -> match opt2 with
                          | Some(value2) -> value1 = value2
                          | None -> false
        | None -> opt2.IsNone

    let (!=) opt1 opt2 = not (opt1 == opt2)

    let readStreamAsString (stream:System.IO.Stream) =
        async {
            let reader = new System.IO.StreamReader(stream)
            return! Async.AwaitTask( reader.ReadToEndAsync()) }

    let writeStringToStream (stream:System.IO.Stream) (text:string) =
        let writer = new System.IO.StreamWriter(stream)
        writer.Write(text)
        writer.Flush()
        writer.Close()

    // Sends the text as the body of the given request
    let sendBody (request:HttpWebRequest) text = async{
        try
            use requestStream = request.GetRequestStream()

            writeStringToStream requestStream text

            let response = request.GetResponse()

            return response :?> HttpWebResponse

        with
            | ex -> raise ex
                    return null // this is silly, and may never get hit.

    }


    // redefine |> for async computation expressions
    let (|!>) asyncF followup = async{
            let! ans = asyncF
            return followup ans 
            }

    // define minus for sequences to be the subset of sequence 1 not in sequence 2
    let (-) seq1 seq2 =
        seq1 |> Seq.filter (fun item1 -> seq2 
                                         |> Seq.exists (fun item2 -> item1 = item2)
                                         |> not)
