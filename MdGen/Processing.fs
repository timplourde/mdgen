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
    let homePageMarkdown (sourceDir: string) (files: File list) = 
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
            let srcDirInfo = DirectoryInfo sourceDir
            srcDirInfo.GetDirectories()
                |> Seq.map (fun d-> directoryMarkdownLink d.Name)
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
            MarkdownContent = homePageMarkdown srcDir files; 
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

    // creates a File for a directory
    let dirInfoToAutoIndexFile (rootSourceDir: string) (files: File list) (dirInfo : DirectoryInfo)  =
        let rootDirInfo = DirectoryInfo rootSourceDir

        // this .md file doesn't really exist
        let indexFilePath = Path.Combine(dirInfo.FullName, "index.md")

        let childrenFiles = files
                            |> List.filter (fun (f:File) -> 
                                let fileDirInfo = DirectoryInfo f.SourcePath
                                fileDirInfo.Parent.FullName = dirInfo.FullName)

        let childrenDirs = dirInfo.EnumerateDirectories()
                            |> Seq.map (fun cd -> cd.Name)
                            |> Seq.toList

        let parentDirs = dirInfo.FullName
                            .Replace(rootDirInfo.FullName, String.Empty)
                            .Split(Path.DirectorySeparatorChar)
                            |> Seq.filter (fun d-> not (String.IsNullOrWhiteSpace(d)) )
                            |> Seq.toList

        {
            SourcePath = indexFilePath;
            Href= srcFilePathToHref rootSourceDir indexFilePath;
            Title = dirInfo.Name;
            LastModified = DateTime.Now;
            MarkdownContent = indexPageContent childrenFiles childrenDirs; 
            ParentDirectories = parentDirs
            DisplayParentDirectoryLinks = true
        }

    // generates File records for all dynamically generated index pages
    let createIndexPages (sourceDir: string) (files: File list) =
        let sourceDirInfo = DirectoryInfo sourceDir
        let allSubDirs = sourceDirInfo.GetDirectories("*", SearchOption.AllDirectories)
        allSubDirs
        |> Seq.filter (fun di -> not (File.Exists(Path.Combine(di.FullName,"index.md"))) )
        |> Seq.map (dirInfoToAutoIndexFile sourceDir files)
        |> Seq.toList

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