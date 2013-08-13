module userManagement

open System.Security

open System.Net
open Helpers

open FSharp.Data

type userList = JsonProvider<"userList.json">

type UserManager() =

   // Creates a basic agilezen rest request with the correct header
    let createRequest path = 
        let root =  "https://localhost:9999/"

        let request = HttpWebRequest.Create(root + path)
        request :?> HttpWebRequest

    // Makes the get request given a url fragment and a lambda to parse the output
    let tryMakeGet urlFragment parse = async {
            let request = createRequest urlFragment
            
            use! response = request.AsyncGetResponse()
            let httpResponse = response :?> HttpWebResponse

            return! match httpResponse.StatusCode with
                    | HttpStatusCode.OK -> httpResponse.GetResponseStream() 
                                           |> readStreamAsString 
                                           |!> parse
                                           |!> Some
                    | HttpStatusCode.NotFound -> async { return None }
                    | _ -> failwith "Bad response"
    }

    


    member this.GetFree() =

        let parse text =
            let users = userList.Parse(text)
            let user = Seq.head users.Items
            let pass = user.Password.ToCharArray() |> Seq.fold (fun (sec:SecureString) char -> 
                                                                    sec.AppendChar(char)
                                                                    sec) (new SecureString())
            user.Name, pass

        tryMakeGet "free/users" parse
        |> Async.RunSynchronously

    member this.AssignUser(username) = async {             
            let request = createRequest (sprintf "/users/%s" username)
            request.Method <- "PUT"
            
            use! response = request.AsyncGetResponse()
            let httpResponse = response :?> HttpWebResponse

            return match httpResponse.StatusCode with
                   | HttpStatusCode.OK -> ()
                   | _ -> failwith "Bad response"
    }

    member this.FreeUser(username) =  async {
            let request = createRequest (sprintf "/users/%s" username)
            request.Method <- "DELETE"
            
            use! response = request.AsyncGetResponse()
            let httpResponse = response :?> HttpWebResponse

            return match httpResponse.StatusCode with
                   | HttpStatusCode.OK -> ()
                   | _ -> failwith "Bad response"
    }