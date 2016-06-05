namespace MdGen

/// ===========================================
/// Common types
/// ===========================================
module Types = 

    open System

    type File = {
        SourcePath : string
        Title: string
        Href : string
        LastModified: DateTime
        MarkdownContent: string
        ParentDirectories: string list
        DisplayParentDirectoryLinks: bool
    }

    type FileSummary = {     
        FileTitle : string
        FileHref : string
    }