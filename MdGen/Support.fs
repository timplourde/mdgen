namespace MdGen

/// ===========================================
/// Suporting functions
/// ===========================================
module Support =

    open System
    open System.IO
    open MdGen.Types 
    open MdGen.Templates
    open Newtonsoft.Json
    open CommonMark

    // converts a path to a source MD file to a target HTML file
    let srcPathToTargetPath (srcPath:string) (destPath:string) (srcFilePath : string) =
        srcFilePath.Replace(srcPath, destPath).Replace(".md", ".html")

    // converts a file to a markdown link in a list item with modified date
    let fileToMarkdownListLink file = 
        sprintf "* [%s](%s) *%s*" file.Title file.Href (file.LastModified.ToShortDateString())

    // converts a directory into a markdown link in a list item
    let directoryMarkdownLink dir =
        sprintf "* [%s](%s)" dir (Uri.EscapeUriString dir)

    // converts an object to json
    let toJson something = 
        let jsonSerializerSettings = new JsonSerializerSettings();
        let camel = Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        jsonSerializerSettings.ContractResolver <- camel
        JsonConvert.SerializeObject(something, jsonSerializerSettings)

    // converts a path into a list of relevant parent directory names
    let getParentDirectories (srcDirectoryParts: string Set) (filePath:string)  = 
        let filePathParts = filePath.Split(Path.DirectorySeparatorChar) |> Array.toList
        let isPartParentDir = (fun (part: string) -> 
            not (part.EndsWith(".md")) && not (srcDirectoryParts.Contains(part)))
        List.filter isPartParentDir filePathParts

    // converts a source file path to an escaped relative Href
    let srcFilePathToHref (sourceDir: string) (sourceFilePath:string) = 
        Uri.EscapeUriString(sourceFilePath.Replace(sourceDir, String.Empty)
            .Replace(".md", ".html").Replace("\\", "/"))

    // generates array of file summaries as JSON
    let filesJson (files: File list) = 
        files
        |> List.map (fun (f:File) -> 
                {
                    FileTitle = f.Title;
                    FileHref = f.Href
                })
         |> toJson

    // convert markdown to html
    let markdownToHtml markdown =
        CommonMark.CommonMarkConverter.Convert(markdown);

    // generates the search JS
    let generateSearchJs (files: File list) =
        SearchJs.Replace("{{ALL_FILES_JSON}}", filesJson files)

    // converts a list of parent dirs to a list of tuples e.g. ("/foo/bar", "bar")
    let parentDirsToRelativePaths parentDirs = 
        parentDirs
        |> List.mapFold (fun acc dir -> 
            let current = sprintf "%s/%s" acc dir
            (current, current)) ""
        |> fst
        |> List.zip parentDirs

    // generates HTML for parent directory links
    let parentDirsToHtmlLinks parentDirs =
        parentDirs
        |> parentDirsToRelativePaths
        |> List.map (fun d -> 
            sprintf @"<a href=""%s"">%s</a>" (Uri.EscapeUriString((snd d))) (fst d) )

    // generates HTML for parent directory links with special consideration for the Home link
    let renderParentDirLinks (file: File)  =
        if file.DisplayParentDirectoryLinks then
            let homeLinkHtml = @"<a href=""/"">Home</a>"
            match file.ParentDirectories.Length with
                | 0 -> homeLinkHtml
                | _ -> file.ParentDirectories
                        |> parentDirsToHtmlLinks
                        |> String.concat " &gt; "
                        |> sprintf @"%s &gt; %s" homeLinkHtml
        else 
            String.Empty
    
    // converts a file record into HTML
    let convertFileToHtml (templateHtml:string) (file:File) =
        let bodyHtml = markdownToHtml file.MarkdownContent
        templateHtml.Replace("{{BODY}}", bodyHtml)
            .Replace("{{TITLE}}", file.Title)
            .Replace("{{LAST_MODIFIED}}", file.LastModified.ToShortDateString())
            .Replace("{{PARENT_DIR_LINKS}}", renderParentDirLinks file)


    let trim (theString:string) =
        theString.Trim()