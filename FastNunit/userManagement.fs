module userManagement

open System.Security

open System.Net
open Helpers

open FSharp.Data

type userList = JsonProvider<"userList.json">
type user = JsonProvider<"user.json">

type WebResponses<'a> =
    | Success of 'a
    | NotFound
    | Error of System.Exception

type UserManager() =

   // Creates a basic agilezen rest request with the correct header
    let createRequest path = 
        let root =  "http://localhost:8080/"

        let request = HttpWebRequest.Create(root + path)
        request :?> HttpWebRequest

    // Makes the get request given a url fragment and a lambda to parse the output
    let tryMakeGet urlFragment parse = async {
            try
                let request = createRequest urlFragment
                request.Method <- "GET"
                request.Accept <- "application/json"
            
                use! response = request.AsyncGetResponse()
                let httpResponse = response :?> HttpWebResponse
            
                return! match httpResponse.StatusCode with
                        | HttpStatusCode.OK -> httpResponse.GetResponseStream() 
                                                |> readStreamAsString 
                                                |!> parse
                                                |!> Success
                        | HttpStatusCode.NotFound -> async { return NotFound }
                        | _ -> failwith "Bad response"
            with
            | :? WebException as ex ->  if (ex.Response :?> HttpWebResponse).StatusCode = HttpStatusCode.NotFound then
                                            return NotFound
                                        else
                                            return Error(ex)
            | ex -> return Error(ex)

    }

    
    member this.GetAll() =
        let parse text =
            userList.Parse(text)
        tryMakeGet "users" parse        

    member this.GetFree() =

        let parse text =
            let user = user.Parse(text)
            let pass = user.Password.ToCharArray() |> Seq.fold (fun (sec:SecureString) char -> 
                                                                    sec.AppendChar(char)
                                                                    sec) (new SecureString())
            user.Name, pass

        tryMakeGet "free/user" parse
        |> Async.RunSynchronously

    member this.AssignUser(username) = async {             
            let request = createRequest (sprintf "user/%s" username)
            request.Method <- "PUT"
            
            use! response = request.AsyncGetResponse()
            let httpResponse = response :?> HttpWebResponse

            return match httpResponse.StatusCode with
                   | HttpStatusCode.OK -> ()
                   | _ -> failwith "Bad response"
    }

    member this.FreeUser(username) =  async {
            let request = createRequest (sprintf "user/%s" username)
            request.Method <- "DELETE"
            
            use! response = request.AsyncGetResponse()
            let httpResponse = response :?> HttpWebResponse

            return match httpResponse.StatusCode with
                   | HttpStatusCode.OK -> ()
                   | _ -> failwith "Bad response"
    }