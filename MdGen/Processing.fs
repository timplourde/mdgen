namespace MdGen

/// ===========================================
/// Main processing logic
/// ===========================================
module Process = 

    open FileIO
    open System
    open System.IO
    open Support
    open Types

    // generates markdown for the home page
    let homePageMarkdown (files: File list) = 
        let template =  Templates.HomePageTemplate

        let mostRecentFiles = 
            files 
            |> List.sortByDescending (fun f->f.LastModified)
            |> (fun (sortedFiles: File list) -> 
                match sortedFiles.Length with
                    | i when i > 10 -> Seq.take 10 sortedFiles
                    | _ -> List.toSeq sortedFiles)
            |> Seq.map fileToMarkdownListLink
            |> String.concat Environment.NewLine

        let topLevelDirectories = 
            query {
                for f in List.toArray files do
                where (f.ParentDirectories.Length = 1)
                select f.ParentDirectories.[0]
                distinct 
            } 
            |> Seq.sort 
            |> Seq.map directoryMarkdownLink 
            |> String.concat Environment.NewLine

        template.Replace("{{MOST_RECENT}}",mostRecentFiles)
            .Replace("{{TOP_DIRECTORIES}}", topLevelDirectories)
            .Replace("{{CACHE_BUST}}", DateTime.Now.Ticks.ToString())

    // creates a File record for the home page
    let createHomePageFile (srcDir: string) (files: File list) =
        {
            SourcePath = sprintf "%s/index.md" srcDir;
            Href= "/";
            Title = "Home";
            LastModified = DateTime.Now;
            MarkdownContent = homePageMarkdown files; 
            ParentDirectories = []
            DisplayParentDirectoryLinks = false;
        }

    // generates markdown for the dynamic index pages
    let indexPageContent (files: File list) (dirs: string list) =
        let filesMarkdown = files
                            |> List.map fileToMarkdownListLink
                            |> String.concat Environment.NewLine

        let dirsMarkdown = dirs
                            |> List.map directoryMarkdownLink
                            |> String.concat Environment.NewLine
                            
        Templates.IndexPageTemplate.Replace("{{INDEX_DIRS}}", dirsMarkdown)
            .Replace("{{INDEX_FILES}}", filesMarkdown)

    // generates File records for all dynamically generated index pages
    let createIndexPages (sourceDir: string) (files: File list) =
        files
        |> List.distinctBy (fun f-> f.ParentDirectories)
        |> List.map (fun f -> 
            let indexFilePath = 
                String.concat (Path.DirectorySeparatorChar.ToString()) [Path.GetDirectoryName(f.SourcePath);"index.md"]
            (f, indexFilePath)
        )
        |> List.filter (fun (file,indexFilePath) -> 
            file.ParentDirectories.Length > 0 && not (File.Exists(indexFilePath)))
        |> List.map (fun (file, indexFilePath) ->
                
            let childrenFiles = 
                List.filter (fun f-> f.ParentDirectories = file.ParentDirectories) files

            let childrenDirs = getNonEmptySiblingDirectoryNamesFor file.SourcePath

            {
                SourcePath = indexFilePath;
                Href= srcFilePathToHref sourceDir indexFilePath;
                Title = List.last file.ParentDirectories;
                LastModified = DateTime.Now;
                MarkdownContent = indexPageContent childrenFiles childrenDirs; 
                ParentDirectories = file.ParentDirectories
                DisplayParentDirectoryLinks = true
            })

    // primary function: processes all markdown files and publishes html
    let run sourceDir targetDir =
        let templateHtml = getLayoutTemplate sourceDir
        let srcDirectoryParts = sourceDir.Split(Path.DirectorySeparatorChar) |> Set.ofArray
        let files = getAllMarkdownFiles sourceDir 
                    |> Seq.map (read sourceDir srcDirectoryParts)
                    |> Seq.toList

        cleanTarget targetDir

        generateSearchJs files |> writeJsSearch targetDir
         
        let homePage = createHomePageFile sourceDir files
        let indexPages = createIndexPages sourceDir files

        List.append files (homePage :: indexPages)
            |> List.iter (writeHtmlFile sourceDir targetDir templateHtml)

        copyAllOtherFiles sourceDir targetDir

        files.Length