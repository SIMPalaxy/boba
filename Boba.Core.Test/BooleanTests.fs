﻿module BooleanTests

open Xunit
open Boba.Core.Boolean

[<Fact>]
let ``Unify succeed: true ~ true`` () =
    Assert.StrictEqual(Some Map.empty, unify BTrue BTrue)

[<Fact>]
let ``Unify succeed: false ~ false`` () =
    Assert.StrictEqual(Some Map.empty, unify BFalse BFalse)

[<Fact>]
let ``Unify fail: true ~ false`` () =
    Assert.StrictEqual(None, unify BTrue BFalse)

[<Fact>]
let ``Unify fail: a ~ !a`` () =
    Assert.StrictEqual(None, unify (BVar "a") (BNot (BVar "a")))

[<Fact>]
let ``Unify succeed: a ∧ b ~ True`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BTrue).Add("b", BTrue)), unify (BAnd (BVar "a", BVar "b")) BTrue)

[<Fact>]
let ``Unify succeed: a ∧ b ~ False`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BAnd (BVar "a", BNot (BVar "b"))).Add("b", BVar "b")), unify (BAnd (BVar "a", BVar "b")) BFalse)

[<Fact>]
let ``Unify succeed: a ∨ b ~ True`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BOr (BNot (BVar "b"), BVar "a")).Add("b", BVar "b")), unify (BOr (BVar "a", BVar "b")) BTrue)

[<Fact>]
let ``Unify succeed: a ∨ b ~ False`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BFalse).Add("b", BFalse)), unify (BOr (BVar "a", BVar "b")) BFalse)

[<Fact>]
let ``Unify succeed: a ∨b ~ a ∧ b`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BVar "b").Add("b", BVar "b")), unify (BAnd (BVar "a", BVar "b")) (BOr (BVar "a", BVar "b")))

[<Fact>]
let ``Match succeed: b ~> b`` () =
    Assert.StrictEqual(Some (Map.empty.Add("b", BRigid "b")), unify (BVar "b") (BRigid "b"))

[<Fact>]
let ``Match succeed: a ~> b`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BRigid "b")), unify (BVar "a") (BRigid "b"))

[<Fact>]
let ``Match succeed: a ~> b ∧ c`` () =
    Assert.StrictEqual(Some (Map.empty.Add("a", BAnd (BRigid "b", BRigid "c"))), unify (BVar "a") (BAnd (BRigid "b", BRigid "c")))

//TODO: re-enable once better simplification is implemented
//[<Fact>]
//let ``Match succeed: b ∧ c ~> a`` () =
//    let bsub = BOr (BAnd (BVar "b", BNot (BVar "c")), BRigid "a")
//    let csub = BOr (BVar "c", BRigid "a")
//    Assert.StrictEqual(Some (Map.empty.Add("b", bsub).Add("c", csub)), unify (BAnd (BVar "b", BVar "c")) (BRigid "a"))