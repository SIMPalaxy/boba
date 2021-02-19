﻿namespace Boba.Core

module Types =

    open System.Diagnostics
    open Common
    open Kinds

    /// It is convenient throughout the implementation of the type system to be able to pattern match on some primitive type
    /// constructors. Using the standard type constructor, and making the primitives constants, would result in pattern matching
    /// on the string name of the primitive, which is bug prone and far less maintainable. However, we don't want to clutter the
    /// Type data structure with noisy type constants, so the primitives have been separated out here.
    type Prim =
        // Misc
        | PValue
        | PFunction
        | PRef
        | PState

        // Collection
        | PTuple
        | PList
        | PVector
        | PSlice

        // Structural
        | PRecord
        | PVariant

    let primKind prim =
        match prim with
        | PValue -> karrow KData (karrow KSharing KValue)
        | PFunction -> karrow (KRow KEffect) (karrow (kseq KValue) (karrow (kseq (KValue)) KData))
        | PRef -> karrow KHeap (karrow KValue KData)
        | PState -> karrow KHeap KEffect

        | PTuple -> karrow (kseq KValue) KData
        | PList -> karrow KValue KData
        | PVector -> karrow KFixed (karrow KValue KData)
        | PSlice -> karrow KFixed (karrow KValue KData)

        | PRecord -> karrow (KRow KField) KData
        | PVariant -> karrow (KRow KField) KData

    /// The type system of Boba extends a basic constructor-polymorphic capable Hindley-Milner type system with several 'base types' that
    /// essentially drive different unification algorithms, as well as 'dotted sequence types' which support variable arity polymorphism.
    /// Note that the interaction between variable-arity polymorphism and the 'shared' type is not currently as nice as it could be; we
    /// would need to add 'dot-to-disjunction' functionality to boolean equations for tuples to get proper sharing polymorphism in the
    /// presence of sharing attributes.
    /// 
    /// Types are built up from the base types using either TSeq or TApp. All source-code explicit primitive types should be
    /// desugared into this form before type inference is started.
    ///
    /// Unique/shared types appear as attributes on type constructors that represent 'complete' data types (i.e. not on unapplied type
    /// constructors. Shared types unify via Boolean unification, where true represents 'Unique/Linear' and false represents 'Shared'.
    /// The intent of sharing attributes on data types is to allow a program to specify that a type should not have been/should not
    /// be duplicated, and have this assertion tracked throughout the lifetime of the data/resource.
    type Type =
        /// A type variable stands in for a particular type, or a sequence of types. The indexes component allows this variable to
        /// select down n levels in a 'sequence substitution', where n is the length of the indexes list. This feature eliminates the
        /// need for generating fresh variables during substitution, which would otherwise greatly complicate sequence substitution
        /// during type inference.
        | TVar of name: string * kind: Kind
        /// A dotted type variable stands in for a sequence of types. The primary impetus for including this is in Boolean equation
        /// types that exist outside of sequences, but still reference variables that can be substituted with a sequence. Basically,
        /// a tuple type ((a^u)...)^u... has type variable u occuring inside the sequence in the tuple as a TVar, and outside the
        /// tuple (not in a sequence) as a TDotVar.
        | TDotVar of name: string * kind: Kind
        /// Represents a rigid type constructor with an explicit kind. Equality of type constructors is based on both name and kind.
        | TCon of name: string * kind: Kind
        | TPrim of prim: Prim

        | TTrue of kind: Kind
        | TFalse of kind: Kind
        | TAnd of left: Type * right: Type
        | TOr of left: Type * right: Type
        | TNot of body: Type

        | TAbelianOne of kind: Kind // identity type for any kind of abelian equation
        | TExponent of body: Type * power: int
        | TMultiply of left: Type * right: Type
        | TFixedConst of num: int // for the numeric constants of fixed size types

        /// Kind argument here is not the kind of the constructor, but the kind of the elements stored in the row.
        | TRowExtend of kind: Kind
        /// Kind argument here is not the kind of the constructor, but the kind of the elements stored in the row.
        | TEmptyRow of kind: Kind

        | TSeq of seq: DotSeq.DotSeq<Type>
        | TApp of cons: Type * arg: Type

    type Predicate = { Name: string; Argument: Type }

    type QualifiedType = { Context: List<Predicate>; Head: Type }

    type TypeScheme = { Quantified: List<(string * Kind)>; Body: QualifiedType }

    type RowType = { Elements: List<Type>; RowEnd: Option<string>; ElementKind: Kind }


    // Type sequence utilities
    let isSeq t =
        match t with
        | TSeq _ -> true
        | _ -> false

    let isInd t =
        match t with
        | TSeq _ -> false
        | _ -> true

    let getSeq t =
        match t with
        | TSeq ts -> ts
        | _ -> failwith "Called getSeq on non-TSeq"

    let emptySeqOrInd (t : Type) =
        match t with
        | TSeq (DotSeq.SEnd) -> true
        | TSeq (_) -> false
        | _ -> true


    // Functional constructors
    let typeVar name kind = TVar (name, kind)
    let typeDotVar name kind = TDotVar (name, kind)
    let typeCon name kind = TCon (name, kind)
    let typeApp cons arg = TApp (cons, arg)

    let typeNot n = TNot n
    let typeOr l r = TOr (l, r)
    let typeAnd l r = TAnd (l, r)

    let typeExp b n = TExponent (b, n)
    let typeMul l r = TMultiply (l, r)
 
    let predType name arg = { Name = name; Argument = arg }

    let qualType context head = { Context = context; Head = head }

    let schemeType quantified body = { Quantified = quantified; Body = body }

    let rec typeToBooleanEqn ty =
        match ty with
        | TTrue _ -> Boolean.BTrue
        | TFalse _ -> Boolean.BFalse
        | TVar (n, k) when isKindBoolean k -> Boolean.BVar n
        | TDotVar (n, k) when isKindBoolean k -> Boolean.BDotVar n
        | TAnd (l, r) -> Boolean.BAnd (typeToBooleanEqn l, typeToBooleanEqn r)
        | TOr (l, r) -> Boolean.BOr (typeToBooleanEqn l, typeToBooleanEqn r)
        | TNot n -> Boolean.BNot (typeToBooleanEqn n)
        | _ -> failwith "Tried to convert a non-Boolean type to a Boolean equation"

    let rec booleanEqnToType kind eqn =
        match eqn with
        | Boolean.BTrue -> TTrue kind
        | Boolean.BFalse -> TFalse kind
        | Boolean.BVar n -> TVar (n, kind)
        | Boolean.BDotVar n -> TDotVar (n, kind)
        | Boolean.BRigid n -> TVar (n, kind)
        | Boolean.BDotRigid n -> TDotVar (n, kind)
        | Boolean.BAnd (l, r) -> TAnd (booleanEqnToType kind l, booleanEqnToType kind r)
        | Boolean.BOr (l, r) -> TOr (booleanEqnToType kind l, booleanEqnToType kind r)
        | Boolean.BNot n -> TNot (booleanEqnToType kind n)
    
    let attrsToDisjunction attrs kind =
        List.map typeToBooleanEqn attrs
        |> List.fold (fun eqn tm -> Boolean.BOr (eqn, tm)) Boolean.BFalse
        |> Boolean.simplify
        |> booleanEqnToType kind

    let rec unitEqnToType (eqn : Abelian.Equation<string, string>) =
        typeMul
            (Map.fold (fun ty var exp -> typeMul ty (typeExp (typeVar var KUnit) exp)) (TAbelianOne KUnit) eqn.Variables)
            (Map.fold (fun ty unit exp -> typeMul ty (typeExp (typeCon unit KUnit) exp)) (TAbelianOne KUnit) eqn.Constants)

    let rec typeToUnitEqn ty =
        match ty with
        | TAbelianOne _ -> new Abelian.Equation<string, string>()
        | TVar (n, KUnit) -> new Abelian.Equation<string, string>(n)
        | TCon (n, KUnit) -> new Abelian.Equation<string, string>(Map.empty, Map.add n 1 Map.empty)
        | TMultiply (l, r) -> (typeToUnitEqn l).Add(typeToUnitEqn r)
        | TExponent (b, n) -> (typeToUnitEqn b).Scale(n)
        | _ -> failwith "Tried to convert a non-Abelian type to a unit equation"

    let rec fixedEqnToType (eqn: Abelian.Equation<string, int>) =
        typeMul
            (Map.fold (fun ty var exp -> typeMul ty (typeExp (typeVar var KUnit) exp)) (TAbelianOne KFixed) eqn.Variables)
            (Map.fold (fun ty fix exp -> typeMul ty (typeExp (TFixedConst fix) exp)) (TAbelianOne KFixed) eqn.Constants)

    let rec typeToFixedEqn ty =
        match ty with
        | TAbelianOne _ -> new Abelian.Equation<string, int>()
        | TVar (n, KUnit) -> new Abelian.Equation<string, int>(n)
        | TFixedConst n -> new Abelian.Equation<string, int>(Map.empty, Map.add n 1 Map.empty)
        | TMultiply (l, r) -> (typeToFixedEqn l).Add(typeToFixedEqn r)
        | TExponent (b, n) -> (typeToFixedEqn b).Scale(n)
        | _ -> failwith "Tried to convert a non-Abelian type to a unit equation"

    let rec typeToRow ty =
        match ty with
        | TApp (TApp (TRowExtend k, elem), row) ->
            let { Elements = elems; RowEnd = rowEnd; ElementKind = elemKind } = typeToRow row
            if elemKind = k
            then { Elements = elem :: elems; RowEnd = rowEnd; ElementKind = elemKind }
            else failwith "Mismatch in row kinds"
        | TVar (row, KRow k) -> { Elements = []; RowEnd = Some row; ElementKind = k }
        | TEmptyRow k -> { Elements = []; RowEnd = None; ElementKind = k }
        | _ -> failwith "Tried to convert a non-row type to a field row."

    let rec rowToType row =
        match row.Elements with
        | e :: es -> typeApp (typeApp (TRowExtend row.ElementKind) e) (rowToType { row with Elements = es })
        | [] ->
            match row.RowEnd with
            | Some r -> typeVar r (KRow row.ElementKind)
            | None -> TEmptyRow row.ElementKind

    let rec rowElementHead rowElem =
        match rowElem with
        | TApp (spine, _) -> rowElementHead spine
        | TCon (head, _) -> head
        | _ -> failwith "Improperly structured row element head"


    // Free variable computations
    let rec typeFree t =
        match t with
        | TVar (n, _) -> Set.singleton n
        | TDotVar (n, _) -> Set.singleton n
        | TSeq ts -> DotSeq.toList ts |> List.map typeFree |> Set.unionMany
        | TApp (l, r) -> Set.union (typeFree l) (typeFree r)

        | TAnd (l, r) -> Set.union (typeFree l) (typeFree r)
        | TOr (l, r) -> Set.union (typeFree l) (typeFree r)
        | TNot n -> typeFree n

        | TExponent (b, _) -> typeFree b
        | TMultiply (l, r) -> Set.union (typeFree l) (typeFree r)

        | _ -> Set.empty

    let predFree p = typeFree p.Argument

    let contextFree c = List.map predFree c |> Set.unionMany

    let qualFree q = contextFree q.Context |> Set.union (typeFree q.Head)

    let schemeFree s = Set.difference (qualFree s.Body) (Set.ofList (List.map fst s.Quantified))


    // Kind computations
    exception MixedDataAndNestedSequences of DotSeq.DotSeq<Type>
    exception KindNotExpected of Kind * List<Kind>
    exception KindInvalidInContext of Kind

    let expectKindsExn pred expected test =
        if pred expected
        then 
            if List.forall (fun t -> t = expected && pred t) test
            then expected
            else raise (KindNotExpected (expected, test))
        else raise (KindInvalidInContext expected)

    let expectKindPredExn pred test =
        if pred test
        then test
        else raise (KindInvalidInContext test)

    let rec typeKindExn t =
        match t with
        | TVar (_, k) -> k
        | TDotVar (_, k) -> k
        | TCon (_, k) -> k
        | TPrim p -> primKind p

        | TTrue k -> k
        | TFalse k -> k
        | TAnd (l, r) -> expectKindsExn isKindBoolean (typeKindExn l) [(typeKindExn r)]
        | TOr (l, r) -> expectKindsExn isKindBoolean (typeKindExn l) [(typeKindExn r)]
        | TNot n -> expectKindPredExn isKindBoolean (typeKindExn n)

        | TAbelianOne k -> k
        | TExponent (b, _) -> expectKindPredExn isKindAbelian (typeKindExn b)
        | TMultiply (l, r) -> expectKindsExn isKindAbelian (typeKindExn l) [(typeKindExn r)]
        | TFixedConst _ -> KFixed

        | TRowExtend k -> karrow k (karrow (KRow k) (KRow k))
        | TEmptyRow k -> KRow k

        | TSeq ts ->
            match ts with
            | ts when DotSeq.all isInd ts -> KData
            | ts when DotSeq.any isSeq ts && DotSeq.any isInd ts -> raise (MixedDataAndNestedSequences ts)
            | ts -> DotSeq.map typeKindExn ts |> maxKindsExn
        | TApp (l, r) -> applyKindExn (typeKindExn l) (typeKindExn r)

    let predKindExn p = typeKindExn p.Argument


    /// Perform many basic simplification steps to minimize the Boolean equations in a type as much as possible, and minimize
    /// integer constants in fixed-size equation types.
    let rec simplifyType ty =
        let k = typeKindExn ty
        if isKindBoolean k
        then typeToBooleanEqn ty |> Boolean.simplify |> booleanEqnToType k
        elif k = KFixed
        then
            let eqn = typeToFixedEqn ty
            let simplified = Map.toSeq eqn.Constants |> Seq.map (fun (b, e) -> b * e) |> Seq.sum
            fixedEqnToType (new Abelian.Equation<string, int>(eqn.Variables, if simplified = 0 then Map.empty else Map.empty.Add(simplified, 1)))
        else
            match ty with
            | TApp (l, r) -> typeApp (simplifyType l) (simplifyType r)
            | TSeq ts -> DotSeq.map simplifyType ts |> TSeq
            | b -> b


    // Substitution computations
    let zipExtendRest (ts : Type) =
        match ts with
        | TSeq (DotSeq.SInd (_, rs)) -> TSeq rs
        | TSeq (DotSeq.SDot (_, rs)) -> TSeq rs
        | TSeq (DotSeq.SEnd) -> failwith "Tried to zipExtendRest an empty sequence."
        | any -> any

    let zipExtendHeads (ts : Type) =
        match ts with
        | TSeq (DotSeq.SInd (b, _)) -> b
        | TSeq (DotSeq.SDot (b, _)) -> b
        | TSeq (DotSeq.SEnd) -> failwith "Tried to zipExtendHeads an empty sequence."
        | any -> any

    let rec dotOrInd (ts : DotSeq.DotSeq<Type>) =
        match ts with
        | DotSeq.SInd (TSeq (DotSeq.SDot _), _) -> DotSeq.SDot
        | DotSeq.SDot (TSeq (DotSeq.SDot _), _) -> DotSeq.SDot
        | DotSeq.SInd (_, rs) -> dotOrInd rs
        | DotSeq.SDot (_, rs) -> dotOrInd rs
        | DotSeq.SEnd -> DotSeq.SInd

    let rec spliceDots (ts : DotSeq.DotSeq<Type>) =
        match ts with
        | DotSeq.SDot (TSeq ts, rs) ->
            if DotSeq.any isSeq ts
            then DotSeq.SDot (TSeq ts, spliceDots rs)
            else DotSeq.append ts (spliceDots rs)
        | DotSeq.SDot (d, rs) -> DotSeq.SDot (d, spliceDots rs)
        | DotSeq.SInd (i, rs) -> DotSeq.SInd (i, spliceDots rs)
        | DotSeq.SEnd -> DotSeq.SEnd

    let rec zipExtend (ts : DotSeq.DotSeq<Type>) =
        let rec zipExtendInc ts =
            if DotSeq.any isSeq ts
            then if DotSeq.all (fun t -> emptySeqOrInd t) ts
                 then DotSeq.SEnd
                 else if DotSeq.any (fun t -> isSeq t && emptySeqOrInd t) ts
                 then failwith "zipExtend sequences were of different length."
                 else (dotOrInd ts) (TSeq (zipExtend (DotSeq.map zipExtendHeads ts)), zipExtendInc (DotSeq.map zipExtendRest ts))
            else ts

        if DotSeq.all isSeq ts && DotSeq.anyIndInSeq ts
        then DotSeq.map (fun t -> getSeq t |> zipExtend |> TSeq) ts
        else zipExtendInc (spliceDots ts)

    let rec fixApp (t : Type) =
        match t with
        | TApp (TSeq ls, TSeq rs) -> DotSeq.zipWith ls rs typeApp |> DotSeq.map fixApp |> TSeq
        | TApp (TSeq ls, r) -> DotSeq.zipWith ls (DotSeq.map (constant r) ls) typeApp |> DotSeq.map fixApp |> TSeq
        | TApp (l, TSeq rs) ->
            // special case for type constructors that take sequences as arguments: don't bubble last nested substitution sequence up!
            // instead, the constructor takes those most nested sequences as an argument
            let canApplySeq =
                match typeKindExn l with
                | KArrow (arg, _) -> arg = typeKindExn (TSeq rs)
                | _ -> false
            if canApplySeq
            then typeApp l (TSeq rs)
            else DotSeq.zipWith (DotSeq.map (constant l) rs) rs typeApp |> DotSeq.map fixApp |> TSeq
        | TApp _ -> t
        | _ -> invalidArg (nameof t) "Called fixApp on non TApp type"

    let rec fixAnd (t : Type) =
        match t with
        | TAnd (TSeq ls, TSeq rs) -> DotSeq.zipWith ls rs typeAnd |> DotSeq.map fixAnd |> TSeq
        | TAnd (TSeq ls, r) -> DotSeq.zipWith ls (DotSeq.map (constant r) ls) typeAnd |> DotSeq.map fixAnd |> TSeq
        | TAnd (l, TSeq rs) -> DotSeq.zipWith (DotSeq.map (constant l) rs) rs typeAnd |> DotSeq.map fixAnd |> TSeq
        | TAnd _ -> t
        | _ -> invalidArg (nameof t) "Called fixAnd on non TAnd type"

    let rec fixOr (t : Type) =
        match t with
        | TOr (TSeq ls, TSeq rs) -> DotSeq.zipWith ls rs typeOr |> DotSeq.map fixOr |> TSeq
        | TOr (TSeq ls, r) -> DotSeq.zipWith ls (DotSeq.map (constant r) ls) typeOr |> DotSeq.map fixOr |> TSeq
        | TOr (l, TSeq rs) -> DotSeq.zipWith (DotSeq.map (constant l) rs) rs typeOr |> DotSeq.map fixOr |> TSeq
        | TOr _ -> t
        | _ -> invalidArg (nameof t) "Called fixAnd on non TOr type"

    let rec fixNot (t : Type) =
        match t with
        | TNot (TSeq ns) -> DotSeq.map (fun b -> typeNot b) ns |> TSeq
        | TNot _ -> t
        | _ -> invalidArg (nameof t) "Called fixExp on non TExponent type"

    let rec fixExp (t : Type) =
        match t with
        | TExponent (TSeq bs, n) -> DotSeq.map (fun b -> typeExp b n) bs |> TSeq
        | TExponent _ -> t
        | _ -> invalidArg (nameof t) "Called fixExp on non TExponent type"

    let rec fixMul (t : Type) =
        match t with
        | TMultiply (TSeq ls, TSeq rs) -> DotSeq.zipWith ls rs typeMul |> DotSeq.map fixMul |> TSeq
        | TMultiply (TSeq ls, r) -> DotSeq.zipWith ls (DotSeq.map (constant r) ls) typeMul |> DotSeq.map fixMul |> TSeq
        | TMultiply (l, TSeq rs) -> DotSeq.zipWith (DotSeq.map (constant l) rs) rs typeMul |> DotSeq.map fixMul |> TSeq
        | TMultiply _ -> t
        | _ -> invalidArg (nameof t) "Called fixAnd on non TMultiply type"

    let rec seqToDisjunctions seq kind =
        match seq with
        | DotSeq.SEnd -> TFalse kind
        | DotSeq.SInd (e, DotSeq.SEnd) -> e
        | DotSeq.SDot (e, DotSeq.SEnd) -> e
        | DotSeq.SInd (e, ds) -> TOr (e, seqToDisjunctions ds kind)
        | DotSeq.SDot (e, ds) -> TOr (e, seqToDisjunctions ds kind)

    let rec lowestSequencesToDisjunctions kind sub =
        match sub with
        | TSeq ts when DotSeq.all isSeq ts -> DotSeq.map (lowestSequencesToDisjunctions kind) ts |> TSeq
        | TSeq ts -> seqToDisjunctions ts kind
        | _ -> sub

    let rec typeSubstExn subst target =
        match target with
        | TVar (n, _) -> if Map.containsKey n subst then subst.[n] else target
        // special case for handling dotted variables inside boolean equations, necessary for allowing polymorphic sharing of tuples based
        // on the sharing status of their elements (i.e. one unique element requires whole tuple to be unique)
        | TDotVar (n, k) ->
            if Map.containsKey n subst
            then lowestSequencesToDisjunctions k subst.[n]
            else target
        | TApp (l, r) -> TApp (typeSubstExn subst l, typeSubstExn subst r) |> fixApp
        | TSeq ts ->
            if Set.isEmpty (Set.difference (typeFree (TSeq ts)) (mapKeys subst))
            then DotSeq.map (typeSubstExn subst) ts |> zipExtend |> TSeq
            else invalidOp "Potentially unsound operation: trying to substitute for only a portion of sequence"
        | TAnd (l, r) -> TAnd (typeSubstExn subst l, typeSubstExn subst r) |> fixAnd
        | TOr (l, r) -> TOr (typeSubstExn subst l, typeSubstExn subst r) |> fixOr
        | TNot n -> TNot (typeSubstExn subst n) |> fixNot
        | TExponent (b, n) -> TExponent (typeSubstExn subst b, n) |> fixExp
        | TMultiply (l, r) -> TMultiply (typeSubstExn subst l, typeSubstExn subst r) |> fixMul
        | _ -> target

    let predSubstExn subst pred = { Name = pred.Name; Argument = typeSubstExn subst pred.Argument }

    let applySubstContextExn subst context = List.map (predSubstExn subst) context
    
    let qualSubstExn subst qual = { Context = applySubstContextExn subst qual.Context; Head = typeSubstExn subst qual.Head }

    let composeSubstExn subl subr = Map.map (fun _ v -> typeSubstExn subl v) subr |> mapUnion fst subl
    
    let mergeSubstExn (s1 : Map<string, Type>) (s2 : Map<string, Type>) =
        let agree = Set.forall (fun v -> s1.[v] = s2.[v]) (Set.intersect (mapKeys s1) (mapKeys s2))
        if agree then mapUnion fst s1 s2 else invalidOp "Substitutions could not be merged"


    // Head noraml form computations
    let rec typeHeadNormalForm t =
        match t with
        | TVar _ -> true
        | TDotVar _ -> true
        | TApp (l, _) -> typeHeadNormalForm l
        | _ -> false

    let predHeadNoramlForm p = typeHeadNormalForm p.Argument