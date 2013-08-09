module userManagement

open System.Security

open FSharp.Data

type userList = JsonProvider<"userList.json">

type UserManager() =
    member this.GetFree() =
        let users = userList.Parse("{}")
        let user = Seq.head users.Items
        let pass = user.Password.ToCharArray() |> Seq.fold (fun (sec:SecureString) char -> 
                                                                sec.AppendChar(char)
                                                                sec) (new SecureString())
        Some (user.Username, pass)

    member this.AssignUser(username) = async { return () }
    member this.FreeUser(username) = async { return () }