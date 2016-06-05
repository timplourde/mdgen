namespace MdGen

/// ===========================================
/// File IO Operations
/// ===========================================
module FileIO =

    open System.IO
    open Regex
    open Types
    open Support

    // creates a directory if necessary
    let createDirectoryIfNecessary filePath =
        let dir = Path.GetDirectoryName filePath
        if not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore

    // writes a file
    let write path content =
        createDirectoryIfNecessary path
        File.WriteAllText(path, content)

    // write a html file for a File record
    let writeHtmlFile sourceDir targetDir templateHtml file =
        let destFilePath = srcPathToTargetPath sourceDir targetDir file.SourcePath
        file
        |> convertFileToHtml templateHtml
        |> write destFilePath

    // reads a markdown file and returns a File record
    let read srcDir srcDirectoryParts (filePath:string) =
        let fileText = File.ReadAllText filePath
        let titlePattern = "# (.*)";
        let title = findOrDefault titlePattern "Untitled Document" fileText |> trim
        
        { 
            SourcePath = filePath;
            Href = srcFilePathToHref srcDir filePath
            Title = title;
            LastModified = File.GetLastWriteTime filePath;
            MarkdownContent = fileText; 
            ParentDirectories = getParentDirectories srcDirectoryParts filePath
            DisplayParentDirectoryLinks= true;
        }

    // gets all .md files in a directory recursively
    let getAllMarkdownFiles dir = 
        Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories)

    // gets the HTML layout template for all pages
    let getLayoutTemplate srcDir =
        let templatePath = Path.Combine(srcDir, "layout.html")
        if (File.Exists(templatePath)) then
            File.ReadAllText(templatePath)
        else 
            Templates.DefaultLayoutTemplate

    // deletes all files and directories in the target directory
    let cleanTarget dir =
        if not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) 
            |> ignore
        else 
            let di = new DirectoryInfo(dir);
            Seq.iter (fun (fileInfo : FileInfo) -> fileInfo.Delete()) (di.GetFiles())
            Seq.iter (fun (dirInfo : DirectoryInfo) -> dirInfo.Delete(true)) (di.GetDirectories())

    // writes the search js file in the target directory
    let writeJsSearch targetDir jsContent = 
        let jsFileTargetPath = 
            String.concat (Path.DirectorySeparatorChar.ToString()) [targetDir; "search.js"]
        write jsFileTargetPath jsContent

    // copies all non-md files, such as images, to the target directory
    let copyAllOtherFiles (sourceDir: string) (targetDir: string) = 
        let allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
        allFiles
            |> Seq.filter (fun f-> not (f.EndsWith(".md")))
            |> Seq.iter (fun (f:string) -> File.Copy(f, f.Replace(sourceDir, targetDir)))

    let getNonEmptySiblingDirectoryNamesFor (filePath: string) =
        let info = new FileInfo(filePath)
        info.Directory.GetDirectories() 
            |> Seq.filter (fun d-> Seq.length (d.EnumerateFiles()) > 0)
            |> Seq.map (fun d-> d.Name) 
            |> Seq.toList