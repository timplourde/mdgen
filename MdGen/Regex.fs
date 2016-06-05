namespace MdGen

/// ===========================================
/// Regular Expressions
/// ===========================================
module Regex = 

    open System
    open System.Text.RegularExpressions

    // find an instance of a token in a regex (first group only)
    let find pattern input =
        let m = Regex.Match(input,pattern);
        if (m.Success) then Some m.Groups.[1].Value else None  

    // finds something matching a pattern on the default
    let findOrDefault pattern defaultIfEmpty input =
        match find  pattern input with
            | Some s -> s
            | _ -> defaultIfEmpty

    let regexRemove pattern (input:string) = 
        let m = new Regex(pattern)
        m.Replace(input, String.Empty)