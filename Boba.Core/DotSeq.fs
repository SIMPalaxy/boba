﻿namespace Boba.Core

module DotSeq =

    open System.Diagnostics

    /// Represents a sequence in which any number of elements can be 'dotted'. Dots represent
    /// elements that can be expanded into more elements of the sequence, usually via substitution.
    [<DebuggerDisplay("{ToString()}")>]
    type DotSeq<'a> =
        | SInd of ty: 'a * rest: DotSeq<'a>
        | SDot of ty: 'a * rest: DotSeq<'a>
        | SEnd

        override this.ToString () =
            match this with
            | SInd (t, SEnd) -> $"{t}"
            | SInd (t, ts) -> $"{t},{ts}"
            | SDot (t, SEnd) -> $"{t}..."
            | SDot (t, ts) -> $"{t}...,{ts}"
            | SEnd -> ""

    /// Conses the given element onto the front of the sequence as an individual element.
    let ind elem seq = SInd (elem, seq)
    
    /// Conses the given element onto the front of the sequence as a dotted element.
    let dot elem seq = SDot (elem, seq)

    /// Create a sequence of non-dotted elements from a standard list.
    let rec ofList (ts : 'a list) = List.foldBack ind ts SEnd

    /// Generate a sequence containing only the dotted elements in the list.
    let rec dotted ts =
        match ts with
        | SInd (_, ts) -> dotted ts
        | SDot (t, ts) -> SDot (t, dotted ts)
        | SEnd -> SEnd

    /// Apply a function uniformly over the elements in the sequence.
    let rec map (f : 'a -> 'b) (ts : 'a DotSeq) =
        match ts with
        | SInd (b, rs) -> ind (f b) (map f rs)
        | SDot (b, rs) -> dot (f b) (map f rs)
        | SEnd -> SEnd

    /// Apply a function uniformly over elements in the sequence, with the index that each element occurs at starting from 0.
    let rec mapi (f : 'a -> int -> 'b) (ts : 'a DotSeq) =
        let rec mapiAcc i ts =
            match ts with
            | SInd (b, rs) -> ind (f b i) (mapiAcc (i + 1) rs)
            | SDot (b, rs) -> dot (f b i) (mapiAcc (i + 1) rs)
            | SEnd -> SEnd
        mapiAcc 0 ts

    /// Apply an aggregation function from left to right across the sequence, threading through an accumulated value.
    /// The final accumulated value is returned as the result.
    let rec fold (f : 'a -> 'b -> 'a) (ini : 'a) (ts : DotSeq<'b>) =
        match ts with
        | SEnd -> ini
        | SInd (t, ts) -> fold f (f ini t) ts
        | SDot (t, ts) -> fold f (f ini t) ts

    /// Apply an aggregation function from right to left across the sequence, threading through an accumulated value.
    /// The final accumulated value is returned as the result.
    let rec foldBack (f : 'b -> 'a -> 'a) (ini : 'a) (ts : DotSeq<'b>) =
        match ts with
        | SEnd -> ini
        | SInd (t, ts) -> f t (foldBack f ini ts)
        | SDot (t, ts) -> f t (foldBack f ini ts)

    /// Apply an aggregation function from left to right across a non-empty sequence, threading through an accumulated value.
    /// The initial value is the first element in the sequence, and the final accumulated value is returned as the result.
    let rec reduce (f : 'a -> 'a -> 'a) (ts : DotSeq<'a>) =
        match ts with
        | SEnd -> Option.None
        | SInd (t, ts) -> fold f t ts |> Option.Some
        | SDot (t, ts) -> fold f t ts |> Option.Some

    /// Apply an aggregation function from right to left across a non-empty sequence, threading through an accumulated value.
    /// The initial value is the last element in the sequence, and the final accumulated value is returned as the result.
    let rec reduceBack (f : 'a -> 'a -> 'a) (ts : DotSeq<'a>) =
        match ts with
        | SEnd -> Option.None
        | SInd (t, ts) -> foldBack f t ts |> Option.Some
        | SDot (t, ts) -> foldBack f t ts |> Option.Some

    /// Convert the sequence to a standard list, erasing the dots along the way.
    let rec toList (ts : DotSeq<'a>) = fold (fun acc i -> i :: acc) [] ts

    /// Get the length of the sequence. Dotted elements still count as one.
    let rec length (ts : 'a DotSeq) = fold (fun s _ -> 1 + s) 0 ts

    /// Determine if all the elements in the sequence pass the given predicate.
    let rec all (f : 'a -> bool) (ts : 'a DotSeq) = fold (fun c b -> c && (f b)) true ts

    /// Determine if at least one element in the sequence passes the given predicate.
    let rec any (f : 'a -> bool) (ts : 'a DotSeq) = fold (fun c b -> c || (f b)) false ts

    /// Whether the sequence contains any non-dotted elements.
    let rec anyIndInSeq (ts : 'a DotSeq) =
        match ts with
        | SInd _ -> true
        | SEnd -> false
        | SDot (_, rs) -> anyIndInSeq rs

    /// Join two DotSeqs together, such that the first element in rs follows the last element in ls.
    let rec append (ls : 'a DotSeq) (rs : 'a DotSeq) =
        match ls with
        | SEnd -> rs
        | SInd (t, sls) -> ind t (append sls rs)
        | SDot (t, sls) -> dot t (append sls rs)

    let rec concat (ts : DotSeq<DotSeq<'a>>) =
        match ts with
        | SEnd -> SEnd
        | SInd (sseq, ts) -> append sseq (concat ts)
        | SDot (sseq, ts) -> append sseq (concat ts)

    /// Retrieve the element at index ind in the given sequence. None if the index is outside the
    /// bounds of the sequence.
    let rec at (ind : int) (seq : 'a DotSeq) =
        match seq with
        | SInd (t, rs) -> if ind = 0 then Option.Some t else at (ind - 1) rs
        | SDot (t, rs) -> if ind = 0 then Option.Some t else at (ind - 1) rs
        | SEnd -> Option.None

    /// Combine two DotSeqs into one, using the given function as the joining operation.
    /// None if the given sequences are of different lengths.
    let rec zipWith (ls : 'a DotSeq) (rs : 'b DotSeq) (f : 'a -> 'b -> 'c) =
        match (ls, rs) with
        | (SInd (lb, ls), SInd (rb, rs)) -> zipWith ls rs f |> ind (f lb rb)
        | (SDot (lb, ls), SDot (rb, rs)) -> zipWith ls rs f |> dot (f lb rb)
        | (SDot (lb, ls), SInd (rb, rs)) -> zipWith ls rs f |> dot (f lb rb)
        | (SInd (lb, ls), SDot (rb, rs)) -> zipWith ls rs f |> dot (f lb rb)
        | (SEnd, SEnd) -> SEnd
        | _ -> invalidArg (nameof ls) "Sequence lengths must match when zipping dotted sequences"