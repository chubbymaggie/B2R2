(*
  B2R2 - the Next-Generation Reversing Platform

  Author: Sang Kil Cha <sangkilc@kaist.ac.kr>
          Minkyu Jung <hestati@kaist.ac.kr>

  Copyright (c) SoftSec Lab. @ KAIST, since 2016

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*)

namespace B2R2

open System

/// A helper module for BitVector.
[<AutoOpen>]
module internal BitVectorHelper =
  exception ArithTypeMismatchException

  let nSizeErr t =
    failwithf "Invalid BitVector value for its type: %s" (t.ToString ())

  let bigNull = 0I

/// BitVector is the fundamental data type for binary code, which is essentially
/// a bit vector. We want the size of a bit vector to be less than or equal to
/// 64 bits because bigint operation is slow, and most arithmetics on modern
/// architectures are in 64 bits any ways. For example, SIMD operations can also
/// be divided into a set of 64-bit operations.
///
/// N.B. Num becomes zero when the Length becomes greater than 64. We
/// intentionally do not sync Num and BigNum.
[<NoComparison; CustomEquality>]
type BitVector =
  private
    {
      Num    : uint64
      Length : RegType
      BigNum : bigint
    }
  override __.Equals obj =
    match obj with
    | :? BitVector as obj ->
      __.Length = obj.Length && __.Num = obj.Num && __.BigNum = obj.BigNum
    | _ -> false

  override __.GetHashCode () =
    hash (__.Num, __.Length, __.BigNum)

  override __.ToString () =
    __.ValToString () + ":" + RegType.toString __.Length

  member __.ValToString () =
    if __.Length <= 64<rt> then "0x" + __.Num.ToString ("X")
    elif __.Num = 0UL && __.BigNum = 0I then "0x0"
    else "0x" + __.BigNum.ToString("X").TrimStart('0')

  static member inline BOp (v: BitVector) (b: uint64) op bigop =
    let n = v.Num
    match v.Length with
    | 1<rt> | 8<rt> -> { v with Num = op n b |> uint8 |> uint64 }
    | 16<rt> -> { v with Num = op n b |> uint16 |> uint64 }
    | 32<rt> -> { v with Num = op n b |> uint32 |> uint64 }
    | 64<rt> -> { v with Num = op n b }
    | _ ->
      let n1 = v.BigNum
      let n2 = bigint b
      let n = bigint.op_BitwiseAnd (bigop (n1, n2), RegType.getMask v.Length)
      { v with BigNum = n }

  static member (+) (v: BitVector, b : uint64) =
    BitVector.BOp v b (+) (bigint.Add)

  static member (-) (v: BitVector, b : uint64) =
    BitVector.BOp v b (-) (bigint.Subtract)

  static member (*) (v: BitVector, b : uint64) =
    BitVector.BOp v b (*) (bigint.Multiply)

  static member (&&&) (v: BitVector, b : uint64) =
    BitVector.BOp v b (&&&) (bigint.op_BitwiseAnd)

  static member (|||) (v: BitVector, b : uint64) =
    BitVector.BOp v b (|||) (bigint.op_BitwiseOr)

  static member (^^^) (v: BitVector, b : uint64) =
    BitVector.BOp v b (^^^) (bigint.op_BitwiseOr)

  static member (/) (v: BitVector, b : uint64) =
    BitVector.BOp v b (/) (bigint.Divide)

  static member (%) (v: BitVector, b : uint64) =
    BitVector.BOp v b (%) (bigint.op_Modulus)

  [<CompiledName("OfUInt64")>]
  static member ofUInt64 (i: uint64) typ =
    match typ with
    | 1<rt> when i = 1UL -> { Num = 1UL; Length = typ; BigNum = bigNull }
    | 1<rt> when i = 0UL -> { Num = 0UL; Length = typ; BigNum = bigNull }
    | 2<rt> -> { Num = i &&& 0x3UL; Length = typ; BigNum = bigNull }
    | 4<rt> -> { Num = i &&& 0xFUL; Length = typ; BigNum = bigNull }
    | 8<rt> -> { Num = uint8 i |> uint64; Length = typ; BigNum = bigNull }
    | 16<rt> -> { Num = uint16 i |> uint64; Length = typ; BigNum = bigNull }
    | 32<rt> -> { Num = uint32 i |> uint64; Length = typ; BigNum = bigNull }
    | 64<rt> -> { Num = i; Length = typ; BigNum = bigNull }
    | 80<rt> -> { Num = 0UL; Length = typ; BigNum = bigint i }
    | 128<rt> -> { Num = 0UL; Length = typ; BigNum = bigint i }
    | 256<rt> -> { Num = 0UL; Length = typ; BigNum = bigint i }
    | 512<rt> -> { Num = 0UL; Length = typ; BigNum = bigint i }
    | _ -> nSizeErr typ

  static member inline convNegs n t =
    match t with
    | 128<rt> -> bigint.Pow (2I, 128) - n
    | 256<rt> -> bigint.Pow (2I, 256) - n
    | 512<rt> -> bigint.Pow (2I, 512) - n
    | _ -> nSizeErr t

  [<CompiledName("OfInt64")>]
  static member ofInt64 (i: int64) typ =
    match typ with
    | 128<rt> | 256<rt> | 512<rt> ->
      if i < 0L then
        let n = BitVector.convNegs ((~-) i |> uint64 |> bigint) typ
        { Num = 0UL; Length = typ; BigNum = n }
      else
        { Num = 0UL; Length = typ; BigNum = bigint i }
    | _ -> BitVector.ofUInt64 (uint64 i) typ

  [<CompiledName("OfUInt32")>]
  static member ofUInt32 (i: uint32) typ = BitVector.ofUInt64 (uint64 i) typ

  [<CompiledName("OfInt32")>]
  static member ofInt32 (i: int32) typ = BitVector.ofInt64 (int64 i) typ

  [<CompiledName("OfUBInt")>]
  static member ofUBInt (i: bigint) typ =
    if typ <= 64<rt> then BitVector.ofUInt64 (uint64 i) typ
    else { Num = 0UL; Length = typ; BigNum = i }

  [<CompiledName("OfArr")>]
  static member ofArr (arr: byte []) =
    match Array.length arr with
    | 1 ->
      { Num = uint64 arr.[0]; Length = 8<rt>; BigNum = bigNull }
    | 2 ->
      let n = BitConverter.ToUInt16 (arr, 0) |> uint64
      { Num = n; Length = 16<rt>; BigNum = bigNull }
    | 4 ->
      let n = BitConverter.ToUInt32 (arr, 0) |> uint64
      { Num = n; Length = 32<rt>; BigNum = bigNull }
    | 8 ->
      let n = BitConverter.ToUInt64 (arr, 0)
      { Num = n; Length = 64<rt>; BigNum = bigNull }
    | 10 ->
      { Num = 0UL; Length = 80<rt>; BigNum = bigint arr }
    | 16 ->
      { Num = 0UL; Length = 128<rt>; BigNum = bigint arr }
    | 32 ->
      { Num = 0UL; Length = 256<rt>; BigNum = bigint arr }
    | 64 ->
      { Num = 0UL; Length = 512<rt>; BigNum = bigint arr }
    | sz when sz > 64 ->
      { Num = 0UL; Length = sz * 8<rt>; BigNum = bigint arr }
    | sz -> nSizeErr (sz * 8)

  [<CompiledName("OfBv")>]
  static member ofBv bv t = { bv with Length = t }

  [<CompiledName("ToUInt64")>]
  static member toUInt64 bv =
    if bv.Length <= 64<rt> then bv.Num
    else nSizeErr bv.Length

  [<CompiledName("ToInt64")>]
  static member toInt64 bv = BitVector.toUInt64 bv |> int64

  [<CompiledName("ToUInt32")>]
  static member toUInt32 bv = BitVector.toUInt64 bv |> uint32

  [<CompiledName("ToInt32")>]
  static member toInt32 bv = BitVector.toInt64 bv |> int32

  [<CompiledName("GetValue")>]
  static member getValue bv =
    if bv.Length <= 64<rt> then bigint bv.Num else bv.BigNum

  [<CompiledName("GetType")>]
  static member getType (bv: BitVector) = bv.Length

  [<CompiledName("Zero")>]
  static member zero (t: RegType) = { Num = 0UL; Length = t; BigNum = bigNull }

  [<CompiledName("One")>]
  static member one (t: RegType) = { Num = 1UL; Length = t; BigNum = bigNull }

  /// True.
  static member T = BitVector.one 1<rt>

  /// False.
  static member F = BitVector.zero 1<rt>

  static member inline shiftRightAndCheckOne n len =
    bigint.op_BitwiseAnd (bigint.op_RightShift (n, len), 1I) = 0I

  [<CompiledName("IsPositive")>]
  static member isPositive bv =
    let len = int bv.Length
    if len <= 64 then ((bv.Num >>> (len - 1)) &&& 1UL) = 0UL
    else BitVector.shiftRightAndCheckOne bv.BigNum (len - 1)

  [<CompiledName("IsNegative")>]
  static member isNegative bv = BitVector.isPositive bv |> not

  static member inline binOp (op: uint64 -> uint64 -> uint64) opBigFn bv1 bv2 =
    let n1 = bv1.Num
    let n2 = bv2.Num
    let len1 = bv1.Length
    let len2 = bv2.Length
    if len1 <> len2 then raise ArithTypeMismatchException else ()
    match len1 with
    | 1<rt> | 8<rt> ->
      { Num = op n1 n2 |> uint8 |> uint64; Length = len1; BigNum = bigNull }
    | 16<rt> ->
      { Num = op n1 n2 |> uint16 |> uint64; Length = len1; BigNum = bigNull }
    | 32<rt> ->
      { Num = op n1 n2 |> uint32 |> uint64; Length = len1; BigNum = bigNull }
    | 64<rt> ->
      { Num = op n1 n2; Length = len1; BigNum = bigNull }
    | _ ->
      let n1 = bv1.BigNum
      let n2 = bv2.BigNum
      let n = bigint.op_BitwiseAnd (opBigFn (n1, n2), RegType.getMask len1)
      { Num = 0UL; Length = len1; BigNum = n }

  [<CompiledName("Add")>]
  static member add v1 v2 = BitVector.binOp (+) (bigint.Add) v1 v2

  [<CompiledName("Sub")>]
  static member sub v1 v2 = BitVector.binOp (-) (bigint.Subtract) v1 v2

  [<CompiledName("Mul")>]
  static member mul v1 v2 = BitVector.binOp (*) (bigint.Multiply) v1 v2

  [<CompiledName("Neg")>]
  static member neg bv =
    match bv.Length with
    | 1<rt> -> bv
    | 8<rt> ->
      { bv with Num = (- (int64 bv.Num |> int8)) |> uint8 |> uint64 }
    | 16<rt> ->
      { bv with Num = (- (int64 bv.Num |> int16)) |> uint16 |> uint64 }
    | 32<rt> ->
      { bv with Num = (- (int64 bv.Num |> int32)) |> uint32 |> uint64 }
    | 64<rt> ->
      { bv with Num = (- (int64 bv.Num)) |> uint64 }
    | 128<rt> ->
      let n = ((bigint.Pow (2I, 128)) - bv.BigNum) &&& BigInteger.mask128
      { bv with BigNum = n }
    | 256<rt> ->
      let n = ((bigint.Pow (2I, 256)) - bv.BigNum) &&& BigInteger.mask256
      { bv with BigNum = n }
    | 512<rt> ->
      let n = ((bigint.Pow (2I, 512)) - bv.BigNum) &&& BigInteger.mask512
      { bv with BigNum = n }
    | len -> nSizeErr len

  [<CompiledName("BitwiseAnd")>]
  static member band v1 v2 = BitVector.binOp (&&&) (bigint.op_BitwiseAnd) v1 v2

  [<CompiledName("BitwiseOr")>]
  static member bor v1 v2 = BitVector.binOp (|||) (bigint.op_BitwiseOr) v1 v2

  [<CompiledName("BitwiseXor")>]
  static member bxor v1 v2 = BitVector.binOp (^^^) (bigint.op_ExclusiveOr) v1 v2

  [<CompiledName("BitwiseNot")>]
  static member bnot bv =
    match bv.Length with
    | 1<rt> -> { bv with Num = if bv.Num = 0UL then 1UL else 0UL }
    | 8<rt> -> { bv with Num = (~~~ (int64 bv.Num)) |> uint8 |> uint64 }
    | 16<rt> -> { bv with Num = (~~~ (int64 bv.Num)) |> uint16 |> uint64 }
    | 32<rt> -> { bv with Num = (~~~ (int64 bv.Num)) |> uint32 |> uint64 }
    | 64<rt> -> { bv with Num = (~~~ (int64 bv.Num)) |> uint64 }
    | 128<rt> | 256<rt> | 512<rt> ->
      { bv with BigNum = bigint.Pow (2I, int bv.Length) - bv.BigNum - 1I }
    | sz -> nSizeErr sz

  [<CompiledName("EQ")>]
  static member eq v1 v2 =
    if v1.Length = v2.Length && v1.Num = v2.Num && v1.BigNum = v2.BigNum then
      BitVector.T
    else BitVector.F

  [<CompiledName("NEQ")>]
  static member neq v1 v2 =
    if v1.Length = v2.Length && v1.Num = v2.Num && v1.BigNum = v2.BigNum then
      BitVector.F
    else BitVector.T

  static member inline unsignedComp v1 v2 op bigop =
    if v1.Length <> v2.Length then raise ArithTypeMismatchException
    elif v1.Length <= 64<rt> then
      if op v1.Num v2.Num then BitVector.T else BitVector.F
    else
      if bigop v1.BigNum v2.BigNum then BitVector.T else BitVector.F

  [<CompiledName("GT")>]
  static member gt v1 v2 = BitVector.unsignedComp v1 v2 (>) (>)

  [<CompiledName("GE")>]
  static member ge v1 v2 = BitVector.unsignedComp v1 v2 (>=) (>=)

  [<CompiledName("LT")>]
  static member lt v1 v2 = BitVector.unsignedComp v1 v2 (<) (<)

  [<CompiledName("LE")>]
  static member le v1 v2 = BitVector.unsignedComp v1 v2 (<=) (<=)

  static member inline signedComp v1 v2 op8 op16 op32 op64 =
    match v1.Length with
    | 8<rt> ->
      if op8 (uint8 v1.Num |> int8) (uint8 v2.Num |> int8) then BitVector.T
      else BitVector.F
    | 16<rt> ->
      if op16 (uint16 v1.Num |> int16) (uint16 v2.Num |> int16) then BitVector.T
      else BitVector.F
    | 32<rt> ->
      if op32 (uint32 v1.Num |> int32) (uint32 v2.Num |> int32) then BitVector.T
      else BitVector.F
    | 64<rt> ->
      if op64 (int64 v1.Num) (int64 v2.Num) then BitVector.T else BitVector.F
    | sz -> nSizeErr sz

  [<CompiledName("SLT")>]
  static member slt v1 v2 =
    let len = v1.Length
    if len <> v2.Length then raise ArithTypeMismatchException
    elif len <= 64<rt> then BitVector.signedComp v1 v2 (<) (<) (<) (<)
    elif BitVector.isPositive v1 && BitVector.isNegative v2 then BitVector.F
    elif BitVector.isNegative v1 && BitVector.isPositive v2 then BitVector.T
    elif BitVector.isNegative v1 && BitVector.isNegative v2 then
      if bigint.op_LessThan (v1.BigNum, v2.BigNum) then BitVector.F
      else BitVector.T
    else
      if bigint.op_LessThan (v1.BigNum, v2.BigNum) then BitVector.T
      else BitVector.F

  [<CompiledName("SLE")>]
  static member sle v1 v2 =
    let len = v1.Length
    if len <> v2.Length then raise ArithTypeMismatchException
    elif len <= 64<rt> then BitVector.signedComp v1 v2 (<=) (<=) (<=) (<=)
    elif BitVector.isPositive v1 && BitVector.isNegative v2 then BitVector.F
    elif BitVector.isNegative v1 && BitVector.isPositive v2 then BitVector.T
    elif BitVector.isNegative v1 && BitVector.isNegative v2 then
      if bigint.op_LessThanOrEqual (v1.BigNum, v2.BigNum) then BitVector.F
      else BitVector.T
    else
      if bigint.op_LessThanOrEqual (v1.BigNum, v2.BigNum) then BitVector.T
      else BitVector.F

  [<CompiledName("SGT")>]
  static member sgt v1 v2 = BitVector.slt v2 v1

  [<CompiledName("SGE")>]
  static member sge v1 v2 = BitVector.sle v2 v1

  static member inline castSmall n rt =
    match rt with
    | 1<rt> -> n &&& 1UL
    | 2<rt> -> n &&& 0x3UL
    | 4<rt> -> n &&& 0xFUL
    | 8<rt> -> uint8 n |> uint64
    | 16<rt> -> uint16 n |> uint64
    | 32<rt> -> uint32 n |> uint64
    | 64<rt> -> n
    | sz -> nSizeErr sz

  static member inline castBig n newLen =
    (RegType.getMask newLen) &&& n

  [<CompiledName("Cast")>]
  static member cast (bv: BitVector) newLen =
    if bv.Length = newLen then bv
    elif bv.Length <= 64<rt> && newLen <= 64<rt> then
      { bv with Num = BitVector.castSmall bv.Num newLen; Length = newLen }
    elif bv.Length <= 64<rt> && newLen > 64<rt> then
      { Num = 0UL; Length = newLen
        BigNum = BitVector.castBig (bigint bv.Num) newLen }
    elif bv.Length > 64<rt> && newLen <= 64<rt> then
      { Num = BitVector.castSmall (BitVector.castBig bv.BigNum newLen |> uint64)
                                  newLen;
        Length = newLen; BigNum = bigNull }
    else
      { bv with BigNum = BitVector.castBig bv.BigNum newLen; Length = newLen }

  [<CompiledName("Extract")>]
  static member extract (bv: BitVector) newLen pos =
    if bv.Length = newLen then bv
    elif bv.Length <= 64<rt> then
      { bv with Num = BitVector.castSmall (bv.Num >>> pos) newLen
                Length = newLen }
    elif bv.Length > 64<rt> && newLen <= 64<rt> then
      { Num = BitVector.castSmall (BitVector.castBig (bv.BigNum >>> pos) newLen
                                   |> uint64) newLen;
        Length = newLen; BigNum = bigNull }
    else
      { bv with
          BigNum = BitVector.castBig (bv.BigNum >>> pos) newLen
          Length = newLen }

  [<CompiledName("Div")>]
  static member div v1 v2 = BitVector.binOp (/) (bigint.Divide) v1 v2

  [<CompiledName("Sdiv")>]
  static member sdiv v1 v2 =
    if BitVector.isPositive v1 && BitVector.isNegative v2 then
      BitVector.binOp (/) (bigint.Divide) v1 (BitVector.neg v2) |> BitVector.neg
    elif BitVector.isNegative v1 && BitVector.isPositive v2 then
      BitVector.binOp (/) (bigint.Divide) (BitVector.neg v1) v2 |> BitVector.neg
    else BitVector.binOp (/) (bigint.Divide) v1 v2

  [<CompiledName("Modulo")>]
  static member modulo v1 v2 = BitVector.binOp (%) (bigint.op_Modulus) v1 v2

  [<CompiledName("SModulo")>]
  static member smodulo v1 v2 =
    if BitVector.isPositive v1 && BitVector.isNegative v2 then
      BitVector.binOp (%) (bigint.op_Modulus) v1 (BitVector.neg v2)
      |> BitVector.neg
    elif BitVector.isNegative v1 && BitVector.isPositive v2 then
      BitVector.binOp (%) (bigint.op_Modulus) (BitVector.neg v1) v2
      |> BitVector.neg
    else BitVector.binOp (%) (bigint.op_Modulus) v1 v2

  [<CompiledName("Shl")>]
  static member shl v1 v2 =
    let len = v1.Length
    if len <> v2.Length then raise ArithTypeMismatchException
    match len with
    | 8<rt> -> { v1 with Num = v1.Num <<< int v2.Num |> uint8 |> uint64 }
    | 16<rt> -> { v1 with Num = v1.Num <<< int v2.Num |> uint16 |> uint64 }
    | 32<rt> -> { v1 with Num = v1.Num <<< int v2.Num |> uint32 |> uint64 }
    | 64<rt> -> { v1 with Num = v1.Num <<< int v2.Num }
    | 128<rt> | 256<rt> | 512<rt> ->
      let v = bigint.op_LeftShift (v1.BigNum, int v2.BigNum)
      let m = RegType.getMask len
      { v1 with BigNum = bigint.op_BitwiseAnd (v, m) }
    | sz -> nSizeErr sz

  [<CompiledName("Shr")>]
  static member shr v1 v2 =
    let len = v1.Length
    if len <> v2.Length then raise ArithTypeMismatchException
    elif v1.Length <= 64<rt> then { v1 with Num = v1.Num >>> int v2.Num }
    else
      let v = bigint.op_RightShift (v1.BigNum, int v2.BigNum)
      let m = RegType.getMask v1.Length
      { v1 with BigNum = bigint.op_BitwiseAnd (v, m) }

  [<CompiledName("Sar")>]
  static member sar v1 v2 =
    match v1.Length with
    | 8<rt> ->
      { v1 with Num = (int8 v1.Num >>> int v2.Num) |> uint8 |> uint64 }
    | 16<rt> ->
      { v1 with Num = (int16 v1.Num >>> int v2.Num) |> uint16 |> uint64 }
    | 32<rt> ->
      { v1 with Num = (int32 v1.Num >>> int v2.Num) |> uint32 |> uint64 }
    | 64<rt> ->
      { v1 with Num = (int64 v1.Num >>> int v2.Num) |> uint64 }
    | 128<rt> | 256<rt> | 512<rt> ->
      let res = BitVector.shr v1 v2
      if BitVector.isPositive v1 then res
      else
        let pad = BigInteger.getMask (int v1.Length)
        let pad = pad - (BigInteger.getMask (int v1.Length - int v2.BigNum))
        { res with BigNum = (bigint.op_BitwiseOr (res.BigNum, pad)) }
    | sz -> nSizeErr sz

  [<CompiledName("Concat")>]
  static member concat v1 v2 =
    let len1 = v1.Length
    let len2 = v2.Length
    let targetLen = len1 + len2
    if targetLen <= 64<rt> then
      let n = (v1.Num <<< int len2) + v2.Num
      { Num = n; Length = targetLen; BigNum = bigNull }
    else
      let v1 = BitVector.getValue v1
      let v2 = BitVector.getValue v2
      let n = bigint.op_LeftShift (v1, int len2) + v2
      { Num = 0UL; Length = targetLen; BigNum = n }

  [<CompiledName("Sext")>]
  static member sext bv typ =
    let mask =
      BitVector.ofUBInt (RegType.getMask typ - RegType.getMask bv.Length) typ
    let bv' = BitVector.cast bv typ
    if BitVector.isPositive bv then bv' else BitVector.add mask bv'

  [<CompiledName("Zext")>]
  static member zext bv t = BitVector.cast bv t

  [<CompiledName("Abs")>]
  static member abs bv =
    if BitVector.isPositive bv then bv else BitVector.neg bv

  static member maxNum8 = BitVector.ofUInt64 255UL 8<rt>
  static member maxNum16 = BitVector.ofUInt64 65535UL 16<rt>
  static member maxNum32 = BitVector.ofUInt64 4294967295UL 32<rt>
  static member maxNum64 = BitVector.ofUInt64 18446744073709551615UL 64<rt>

  static member midNum8 =  bigint 0x80UL
  static member midNum16 = bigint 0x8000UL
  static member midNum32 = bigint 0x80000000UL
  static member midNum64 = bigint 0x8000000000000000UL

  static member midNum rt =
    match rt with
    | 8<rt> -> BitVector.midNum8
    | 16<rt> -> BitVector.midNum16
    | 32<rt> -> BitVector.midNum32
    | 64<rt> -> BitVector.midNum64
    | _ -> failwith "Invalid type for mid num"

  [<CompiledName("IsSignedMin")>]
  static member isSignedMin bv =
    let v = BitVector.getValue bv
    BitVector.midNum bv.Length = v

  [<CompiledName("IsZero")>]
  static member isZero bv =
    if bv.Length <= 64<rt> then bv.Num = 0UL
    else bv.BigNum = 0I

  [<CompiledName("IsOne")>]
  static member isOne bv =
    if bv.Length <= 64<rt> then bv.Num = 1UL
    else bv.BigNum = 1I

  [<CompiledName("IsNum")>]
  static member isNum bv n =
    if bv.Length <= 64<rt> then bv.Num = n
    else bv.BigNum = bigint n

  [<CompiledName("ValToString")>]
  static member valToString (n: BitVector) = n.ValToString ()

  [<CompiledName("ToString")>]
  static member toString (n: BitVector) = n.ToString ()

// vim: set tw=80 sts=2 sw=2:
