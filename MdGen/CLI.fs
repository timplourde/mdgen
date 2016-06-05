namespace MdGen

/// ===========================================
/// CLI
/// ===========================================

module CLI =

    open System
    open System.IO
    open Process

    // converts CLI args to a tuple of (full) source & target paths
    let getDirs sourcePath targetPath = 
        let currentDir = Environment.CurrentDirectory

        let toFullPath relativePath =
            sprintf "%s%s%s" currentDir (Path.DirectorySeparatorChar.ToString()) relativePath

        let toFullPathIfNecessary path =
            if(Path.IsPathRooted(path)) then
                path
            else
                toFullPath path 

        (toFullPathIfNecessary sourcePath, toFullPathIfNecessary targetPath)


    // executes the conversion process
    let runConverter dirs = 
        let fileCount = Process.run (fst dirs) (snd dirs)
        Console.WriteLine(sprintf "Converted %i markdown files" fileCount)

    // watches directories for changes and calls the onChanged callback
    let watch dirs onChanged =

        let mutable dirty = false;

        let watcher = new FileSystemWatcher(fst dirs, "*.*")

        let timer = new System.Timers.Timer(1000.0)
        timer.AutoReset <- true
        timer.Elapsed.Add(fun _ ->
            if dirty then 
                watcher.EnableRaisingEvents <- false
                timer.Enabled <- false
                onChanged dirs
                dirty <- false
                watcher.EnableRaisingEvents <- true
                timer.Enabled <- true
            )

        let handleFilesystemChange (status : FileSystemEventArgs) =
            //Console.WriteLine (status.ChangeType.ToString())
            dirty <- true

        watcher.IncludeSubdirectories <- true
        watcher.Changed.Add(handleFilesystemChange)
        watcher.Created.Add(handleFilesystemChange)
        watcher.Deleted.Add(handleFilesystemChange)
        watcher.Renamed.Add(handleFilesystemChange)

        timer.Start()
        watcher.EnableRaisingEvents <- true
    
        Console.WriteLine("Hit any key to stop watching")
        Console.ReadKey() |> ignore
        watcher.EnableRaisingEvents <- false
        watcher.Dispose()
        timer.Stop()
        timer.Dispose()
        Console.WriteLine("Stopped watching")
        0

    let execute (argv: string array) =
        let dirArgs = Array.filter (fun (a:string) -> a <> "--watch" && a <> "-h") argv
        if Array.contains "-h" argv || dirArgs.Length < 2 then
            Console.WriteLine("Usage: source target [--watch]")
            2
        else
            let dirs = getDirs dirArgs.[0] dirArgs.[1]

            if not (Directory.Exists (fst dirs)) then
                Console.WriteLine(sprintf "%s does not exist" (fst dirs))
                1
            else 
                if Seq.contains "--watch" argv then
                    watch dirs runConverter
                else
                    runConverter dirs
                    0