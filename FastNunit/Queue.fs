module Parallel

    open System
    open System.Threading

    // This queue is designed to handle the rotation of messages between
    // the worker queues
    type MasterQueueAgent<'a, 'b>(agentCount, handleResult:'b option -> 'b -> 'b option, doTest:'a -> Async<'b>) = 

        let waiter = new AutoResetEvent(false)

        // This is where the results come in.
        let results = MailboxProcessor.Start(fun results ->
            let rec loop acc = 
                async
                    {
                        let! result = results.Receive()
                        match result with
                        | Some(r) -> return! loop(handleResult acc r)
                        | None -> waiter.Set()
                                  return acc
                    }
            loop None)

        let createProcessor() =
                MailboxProcessor.Start(fun (tests:MailboxProcessor<'a option>) ->
                    let rec loop () =
                        async
                            {
                                let! test = tests.Receive()
                                match test with
                                | Some(t) -> let! result = doTest(t)
                                             results.Post(Some result)
                                             return! loop()
                                | None -> return ()
                            }
                    loop())

        let counter = MailboxProcessor.Start(fun inbox ->

                 // initalize a list of works limited by the number
                 // of process avaiable
                 let initialWorkers = 
                    [ for x in 1 .. agentCount -> createProcessor() ]

                 // loop which peels a work off the queue then places
                 // adds a the work item to its queue then place it at the back
                 // of the queue
                 let rec loop (workers:MailboxProcessor<'a option> list) = 
                    async { let! msg = inbox.Receive()
                            let woker = workers.Head
                            do woker.Post(msg) 
                            return! loop(workers.Tail @ [workers.Head])} 
                 loop(initialWorkers))

        member x.Post(n:'a) = counter.Post(Some n)

        member x.Stop() = [for i in 1 .. agentCount -> counter.Post(None)] |> ignore

        member x.Wait() = 
            x.Stop()
            waiter.WaitOne()

        interface IDisposable with
            member this.Dispose() = this.Stop()
            