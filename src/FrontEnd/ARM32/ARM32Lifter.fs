(*
  B2R2 - the Next-Generation Reversing Platform

  Author: Seung Il Jung <sijung@kaist.ac.kr>
          DongYeop Oh <oh51dy@kaist.ac.kr>

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

module internal B2R2.FrontEnd.ARM32.Lifter

open System
open B2R2
open B2R2.BinIR.LowUIR
open B2R2.BinIR.LowUIR.AST
open B2R2.FrontEnd
open B2R2.FrontEnd.ARM32

let inline private (<!) (builder: StmtBuilder) (s) = builder.Append (s)

let getRegVar (ctxt: TranslationContext) name =
  Register.toRegID name |> ctxt.GetRegVar

let getPC ctxt = getRegVar ctxt R.PC

let getRegNum = function
  | R.R0 -> 1u
  | R.R1 -> 2u
  | R.R2 -> 4u
  | R.R3 -> 8u
  | R.R4 -> 16u
  | R.R5 -> 32u
  | R.R6 -> 64u
  | R.R7 -> 128u
  | R.R8 -> 256u
  | R.SB -> 512u
  | R.SL -> 1024u
  | R.FP -> 2048u
  | R.IP -> 4096u
  | R.SP -> 8192u
  | R.LR -> 16384u
  | R.PC -> 32768u
  | _ -> raise InvalidRegisterException

let regsToUInt32 regs = List.fold (fun acc reg -> acc + getRegNum reg) 0u regs

let regsToExpr regs = num <| BitVector.ofUInt32 (regsToUInt32 regs) 16<rt>

let transOprToExpr ctxt = function
  | SpecReg (reg, _)
  | Register reg -> getRegVar ctxt reg
  | RegList regs -> regsToExpr regs
  | Immediate imm -> num <| BitVector.ofInt64 imm 32<rt>
  | _ -> raise InvalidOperandException

let transOneOpr insInfo ctxt =
  match insInfo.Operands with
  | OneOperand opr -> transOprToExpr ctxt opr
  | _ -> raise InvalidOperandException

let transTwoOprs insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (opr1, opr2) -> transOprToExpr ctxt opr1,
                                transOprToExpr ctxt opr2
  | _ -> raise InvalidOperandException

let transThreeOprs insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (opr1, opr2, opr3) -> transOprToExpr ctxt opr1,
                                        transOprToExpr ctxt opr2,
                                        transOprToExpr ctxt opr3
  | _ -> raise InvalidOperandException

let transFourOprs insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (o1, o2, o3, o4) -> transOprToExpr ctxt o1,
                                     transOprToExpr ctxt o2,
                                     transOprToExpr ctxt o3,
                                     transOprToExpr ctxt o4
  | _ -> raise InvalidOperandException

let bvOfBaseAddr addr = num <| BitVector.ofUInt64 addr 32<rt>

/// Gets the mask bits for fetching the RFR bit from the NSACR.
/// NSACR bit[19]
let maskNSACRForRFRbit = num <| BitVector.ofInt32 524288 32<rt>

let getNSACR ctxt nsacrType =
  let nsacr = getRegVar ctxt R.NSACR
  match nsacrType with
  | NSACR_RFR -> nsacr .& maskNSACRForRFRbit

let isSetNSACR_RFR ctxt = getNSACR ctxt NSACR_RFR == maskNSACRForRFRbit

/// Gets the mask bits for fetching the AW bit from the SCR.
/// SCR bit[5]
let maskSCRForAWbit = num <| BitVector.ofInt32 32 32<rt>

/// Gets the mask bits for fetching the FW bit from the SCR.
/// SCR bit[4]
let maskSCRForFWbit = num <| BitVector.ofInt32 16 32<rt>

/// Gets the mask bits for fetching the NS bit from the SCR.
/// SCR bit[0]
let maskSCRForNSbit = num1 32<rt>

let getSCR ctxt scrType =
  let scr = getRegVar ctxt R.SCR
  match scrType with
  | SCR_AW -> scr .& maskSCRForAWbit
  | SCR_FW -> scr .& maskSCRForFWbit
  | SCR_NS -> scr .& maskSCRForNSbit

let isSetSCR_AW ctxt = getSCR ctxt SCR_AW == maskSCRForAWbit
let isSetSCR_FW ctxt = getSCR ctxt SCR_FW == maskSCRForFWbit
let isSetSCR_NS ctxt = getSCR ctxt SCR_NS == maskSCRForNSbit

/// Gets the mask bits for fetching the NMFI bit from the SCTLR.
/// SCTLR bit[27]
let maskSCTLRForNMFIbit = num <| BitVector.ofUBInt 134217728I 32<rt>

let getSCTLR ctxt sctlrType =
  let sctlr = getRegVar ctxt R.SCTLR
  match sctlrType with
  | SCTLR_NMFI -> sctlr .& maskSCTLRForNMFIbit

let isSetSCTLR_NMFI ctxt = getSCTLR ctxt SCTLR_NMFI == maskSCTLRForNMFIbit

/// Gets the mask bits for fetching the condition flag bits from the PSR.
/// PSR bit[31:28]
let maskPSRForCondbits = num <| BitVector.ofUBInt 4026531840I 32<rt>

/// Gets the mask bits for fetching the N condition flag from the PSR.
/// PSR bit[31]
let maskPSRForNbit = num <| BitVector.ofUBInt 2147483648I 32<rt>

/// Gets the mask bits for fetching the Z condition flag from the PSR.
/// PSR bits[30]
let maskPSRForZbit = num <| BitVector.ofUBInt 1073741824I 32<rt>

/// Gets the mask bits for fetching the C condition flag from the PSR.
/// PSR bit[29]
let maskPSRForCbit = num <| BitVector.ofUBInt 536870912I 32<rt>

/// Gets the mask bits for fetching the V condition flag from the PSR.
/// PSR bit[28]
let maskPSRForVbit = num <| BitVector.ofUBInt 268435456I 32<rt>

/// Gets the mask bits for fetching the Q bit from the PSR.
/// PSR bit[27]
let maskPSRForQbit = num <| BitVector.ofUBInt 134217728I 32<rt>

/// Gets the mask bits for fetching the IT[1:0] bits from the PSR.
/// PSR bits[26:25]
let maskPSRForIT10bits = num <| BitVector.ofUBInt 100663296I 32<rt>

/// Gets the mask bits for fetching the J bit from the PSR.
/// PSR bit[24]
let maskPSRForJbit = num <| BitVector.ofUBInt 16777216I 32<rt>

/// Gets the mask bits for fetching the GE[3:0] bits from the PSR.
/// PSR bits[19:16]
let maskPSRForGEbits = num <| BitVector.ofUBInt 983040I 32<rt>

/// Gets the mask bits for fetching the IT[7:2] bits from the PSR.
/// PSR bits[15:10]
let maskPSRForIT72bits = num <| BitVector.ofUBInt 64512I 32<rt>

/// Gets the mask bits for fetching the E bit from the PSR.
/// PSR bit[9]
let maskPSRForEbit = num <| BitVector.ofUBInt 512I 32<rt>

/// Gets the mask bits for fetching the A bit from the PSR.
/// PSR bit[8]
let maskPSRForAbit = num <| BitVector.ofUBInt 256I 32<rt>

/// Gets the mask bits for fetching the I bit from the PSR.
/// PSR bit[7]
let maskPSRForIbit = num <| BitVector.ofUBInt 128I 32<rt>

/// Gets the mask bits for fetching the F bit from the PSR.
/// PSR bit[6]
let maskPSRForFbit = num <| BitVector.ofUBInt 64I 32<rt>

/// Gets the mask bits for fetching the T bit from the PSR.
/// PSR bit[5]
let maskPSRForTbit = num <| BitVector.ofUBInt 32I 32<rt>

/// Gets the mask bits for fetching the M[4:0] bits from the PSR.
/// PSR bits[4:0]
let maskPSRForMbits = num <| BitVector.ofUBInt 31I 32<rt>

let enablePSR ctxt reg psrType =
  let psr = getRegVar ctxt reg
  match psrType with
  | PSR_Cond -> psr .| maskPSRForCondbits
  | PSR_N -> psr .| maskPSRForNbit
  | PSR_Z -> psr .| maskPSRForZbit
  | PSR_C -> psr .| maskPSRForCbit
  | PSR_V -> psr .| maskPSRForVbit
  | PSR_Q -> psr .| maskPSRForQbit
  | PSR_IT10 -> psr .| maskPSRForIT10bits
  | PSR_J -> psr .| maskPSRForJbit
  | PSR_GE -> psr .| maskPSRForGEbits
  | PSR_IT72 -> psr .| maskPSRForIT72bits
  | PSR_E -> psr .| maskPSRForEbit
  | PSR_A -> psr .| maskPSRForAbit
  | PSR_I -> psr .| maskPSRForIbit
  | PSR_F -> psr .| maskPSRForFbit
  | PSR_T -> psr .| maskPSRForTbit
  | PSR_M -> psr .| maskPSRForMbits

let disablePSR ctxt reg psrType =
  let psr = getRegVar ctxt reg
  match psrType with
  | PSR_Cond -> psr .& not maskPSRForCondbits
  | PSR_N -> psr .& not maskPSRForNbit
  | PSR_Z -> psr .& not maskPSRForZbit
  | PSR_C -> psr .& not maskPSRForCbit
  | PSR_V -> psr .& not maskPSRForVbit
  | PSR_Q -> psr .& not maskPSRForQbit
  | PSR_IT10 -> psr .& not maskPSRForIT10bits
  | PSR_J -> psr .& not maskPSRForJbit
  | PSR_GE -> psr .& not maskPSRForGEbits
  | PSR_IT72 -> psr .& not maskPSRForIT72bits
  | PSR_E -> psr .& not maskPSRForEbit
  | PSR_A -> psr .& not maskPSRForAbit
  | PSR_I -> psr .& not maskPSRForIbit
  | PSR_F -> psr .& not maskPSRForFbit
  | PSR_T -> psr .& not maskPSRForTbit
  | PSR_M -> psr .& not maskPSRForMbits

let setPSR ctxt reg psrType expr =
  let shift expr =
    match psrType with
    | PSR_Cond -> expr << (num <| BitVector.ofInt32 28 32<rt>)
    | PSR_N -> expr << (num <| BitVector.ofInt32 31 32<rt>)
    | PSR_Z -> expr << (num <| BitVector.ofInt32 30 32<rt>)
    | PSR_C -> expr << (num <| BitVector.ofInt32 29 32<rt>)
    | PSR_V -> expr << (num <| BitVector.ofInt32 28 32<rt>)
    | PSR_Q -> expr << (num <| BitVector.ofInt32 27 32<rt>)
    | PSR_IT10 -> expr << (num <| BitVector.ofInt32 25 32<rt>)
    | PSR_J -> expr << (num <| BitVector.ofInt32 24 32<rt>)
    | PSR_GE -> expr << (num <| BitVector.ofInt32 16 32<rt>)
    | PSR_IT72 -> expr << (num <| BitVector.ofInt32 10 32<rt>)
    | PSR_E -> expr << (num <| BitVector.ofInt32 9 32<rt>)
    | PSR_A -> expr << (num <| BitVector.ofInt32 8 32<rt>)
    | PSR_I -> expr << (num <| BitVector.ofInt32 7 32<rt>)
    | PSR_F -> expr << (num <| BitVector.ofInt32 6 32<rt>)
    | PSR_T -> expr << (num <| BitVector.ofInt32 5 32<rt>)
    | PSR_M -> expr
  disablePSR ctxt reg psrType .| (zExt 32<rt> expr |> shift)

let getPSR ctxt reg psrType =
  let psr = getRegVar ctxt reg
  match psrType with
  | PSR_Cond -> psr .& maskPSRForCondbits
  | PSR_N -> psr .& maskPSRForNbit
  | PSR_Z -> psr .& maskPSRForZbit
  | PSR_C -> psr .& maskPSRForCbit
  | PSR_V -> psr .& maskPSRForVbit
  | PSR_Q -> psr .& maskPSRForQbit
  | PSR_IT10 -> psr .& maskPSRForIT10bits
  | PSR_J -> psr .& maskPSRForJbit
  | PSR_GE -> psr .& maskPSRForGEbits
  | PSR_IT72 -> psr .& maskPSRForIT72bits
  | PSR_E -> psr .& maskPSRForEbit
  | PSR_A -> psr .& maskPSRForAbit
  | PSR_I -> psr .& maskPSRForIbit
  | PSR_F -> psr .& maskPSRForFbit
  | PSR_T -> psr .& maskPSRForTbit
  | PSR_M -> psr .& maskPSRForMbits

let isSetCPSR_N ctxt = getPSR ctxt R.CPSR PSR_N == maskPSRForNbit
let isSetCPSR_Z ctxt = getPSR ctxt R.CPSR PSR_Z == maskPSRForZbit
let isSetCPSR_C ctxt = getPSR ctxt R.CPSR PSR_C == maskPSRForCbit
let isSetCPSR_V ctxt = getPSR ctxt R.CPSR PSR_V == maskPSRForVbit
let isSetCPSR_J ctxt = getPSR ctxt R.CPSR PSR_J == maskPSRForJbit
let isSetCPSR_T ctxt = getPSR ctxt R.CPSR PSR_T == maskPSRForTbit

let isSetAPSR_N ctxt = getPSR ctxt R.APSR PSR_N == maskPSRForNbit
let isSetAPSR_Z ctxt = getPSR ctxt R.APSR PSR_Z == maskPSRForZbit
let isSetAPSR_C ctxt = getPSR ctxt R.APSR PSR_C == maskPSRForCbit
let isSetAPSR_V ctxt = getPSR ctxt R.APSR PSR_V == maskPSRForVbit
let isSetAPSR_J ctxt = getPSR ctxt R.APSR PSR_J == maskPSRForJbit
let isSetAPSR_T ctxt = getPSR ctxt R.APSR PSR_T == maskPSRForTbit

let getCarryFlag ctxt =
  getPSR ctxt R.APSR PSR_C >> (num <| BitVector.ofInt32 29 32<rt>)

let maskAndOR e1 e2 regType maskSize =
  let mask = BitVector.ofUBInt (BigInteger.getMask maskSize) regType
             |> BitVector.bnot |> num
  let expr = e1 .& mask
  expr .| e2

let getOverflowFlagOnAdd e1 e2 r =
  let e1High = extractHigh 1<rt> e1
  let e2High = extractHigh 1<rt> e2
  let rHigh = extractHigh 1<rt> r
  (e1High == e2High) .& (e1High <+> rHigh)

let parseCond = function
  | Condition.EQ -> 0b000, 0
  | Condition.NE -> 0b000, 1
  | Condition.CS -> 0b001, 0
  | Condition.CC -> 0b001, 1
  | Condition.MI -> 0b010, 0
  | Condition.PL -> 0b010, 1
  | Condition.VS -> 0b011, 0
  | Condition.VC -> 0b011, 1
  | Condition.HI -> 0b100, 0
  | Condition.LS -> 0b100, 1
  | Condition.GE -> 0b101, 0
  | Condition.LT -> 0b101, 1
  | Condition.GT -> 0b110, 0
  | Condition.LE -> 0b110, 1
  | Condition.AL -> 0b111, 0
  | Condition.UN -> 0b111, 1
  | _ -> failwith "Invalid condition"

/// Returns TRUE if the current instruction passes its condition code check,
/// on page A8-289. function : ConditionPassed()
let conditionPassed ctxt cond =
  let cond1, cond2 = parseCond cond
  let result =
    match cond1 with
    | 0b000 -> isSetCPSR_Z ctxt
    | 0b001 -> isSetCPSR_C ctxt
    | 0b010 -> isSetCPSR_N ctxt
    | 0b011 -> isSetCPSR_V ctxt
    | 0b100 -> isSetCPSR_C ctxt .& not (isSetCPSR_Z ctxt)
    | 0b101 -> isSetCPSR_N ctxt == isSetCPSR_V ctxt
    | 0b110 -> isSetCPSR_N ctxt == isSetCPSR_V ctxt .& not (isSetCPSR_Z ctxt)
    | 0b111 -> b1
    | _ -> failwith "Invalid condition"
  if cond1 <> 0b111 && cond2 = 1 then not result else result

/// Logical shift left of a bitstring, with carry output, on page A2-41.
/// for Register amount. function : LSL_C()
let shiftLSLCForRegAmount value regType amount carryIn =
  let chkEQ = relop RelOpType.EQ amount (num (BitVector.ofUInt32 0u regType))
  let chkGT = relop RelOpType.GT amount (num (BitVector.ofUInt32 0u regType))
  let result = value << amount
  let result = ite chkGT result (Expr.Undefined (regType, "AssertError"))
  let carryOut = value << (amount .- num1 regType ) |> extractHigh 1<rt>
  let carryOut = ite chkGT carryOut (Expr.Undefined (1<rt>, "AssertError"))
  ite chkEQ value result, ite chkEQ carryIn carryOut

/// Logical shift left of a bitstring, on page A2-41. for Register amount.
/// function : LSL()
let shiftLSLForRegAmount value regType amount carryIn =
  shiftLSLCForRegAmount value regType amount carryIn |> fst

/// Logical shift right of a bitstring, with carry output, on page A2-41.
/// for Register amount. function : LSR_C()
let shiftLSRCForRegAmount value regType amount carryIn =
  let chkEQ = relop RelOpType.EQ amount (num (BitVector.ofUInt32 0u regType))
  let chkGT = relop RelOpType.GT amount (num (BitVector.ofUInt32 0u regType))
  let result = value >> amount
  let result = ite chkGT result (Expr.Undefined (regType, "AssertError"))
  let carryOut = value >> (amount .- num1 regType ) |> extractLow 1<rt>
  let carryOut = ite chkGT carryOut (Expr.Undefined (1<rt>, "AssertError"))
  ite chkEQ value result, ite chkEQ carryIn carryOut

/// Logical shift right of a bitstring, on page A2-41. for Register amount.
/// function : LSR()
let shiftLSRForRegAmount value regType amount carryIn =
  shiftLSRCForRegAmount value regType amount carryIn |> fst

/// Arithmetic shift right of a bitstring, with carry output, on page A2-41.
/// for Register amount. function : ASR_C()
let shiftASRCForRegAmount value regType amount carryIn =
  let chkEQ = relop RelOpType.EQ amount (num (BitVector.ofUInt32 0u regType))
  let chkGT = relop RelOpType.GT amount (num (BitVector.ofUInt32 0u regType))
  let result = value ?>> amount
  let result = ite chkGT result (Expr.Undefined (regType, "AssertError"))
  let carryOut = value ?>> (amount .- num1 regType ) |> extractLow 1<rt>
  let carryOut = ite chkGT carryOut (Expr.Undefined (1<rt>, "AssertError"))
  ite chkEQ value result, ite chkEQ carryIn carryOut

/// Logical shift right of a bitstring, on page A2-41. for Register amount.
/// function : ASR()
let shiftASRForRegAmount value regType amount carryIn =
  shiftASRCForRegAmount value regType amount carryIn|> fst

/// Rotate right of a bitstring, with carry output, on page A2-41.
/// for Register amount. function : ROR_C()
let shiftRORCForRegAmount value regType amount carryIn =
  let chkEQ = relop RelOpType.EQ amount (num (BitVector.ofUInt32 0u regType))
  let m = amount .% num (BitVector.ofInt32 (RegType.toBitWidth regType) regType)
  let result = shiftLSRForRegAmount value regType m carryIn .|
               shiftLSLForRegAmount value regType m carryIn
  let carryOut = extractHigh 1<rt> result
  ite chkEQ value result, ite chkEQ carryIn carryOut

/// Rotate right of a bitstring, on page A2-41. for Register amount.
/// function : ROR()
let shiftRORForRegAmount value regType amount carryIn =
  shiftRORCForRegAmount value regType amount carryIn |> fst

/// Rotate right with extend of a bitstring, with carry output, on page A2-41.
/// for Register amount. function : RRX_C()
let shiftRRXCForRegAmount value regType amount carryIn =
  let chkEQ = relop RelOpType.EQ amount (num (BitVector.ofUInt32 0u regType))
  let amount1 = num (BitVector.ofInt32 (RegType.toBitWidth regType) regType)
  let e1 = shiftLSLForRegAmount (zExt 32<rt> carryIn) regType
            (amount1 .- num1 regType) carryIn
  let e2 = shiftLSRForRegAmount value regType (num1 regType) carryIn
  ite chkEQ value (e1 .| e2), ite chkEQ carryIn (extractLow 1<rt> value)

/// Rotate right with extend of a bitstring, on page A2-41. for Register amount.
/// function : RRX()
let shiftRRXForRegAmount value regType amount carryIn =
  shiftRRXCForRegAmount value regType amount carryIn |> fst

/// Perform a specified shift by a specified amount on a bitstring,
/// with carry output, on page A8-292.
let shiftCForRegAmount value regType shiftType amount carryIn =
  let carryIn = extractLow 1<rt> carryIn
  match shiftType with
  | SRTypeLSL -> shiftLSLCForRegAmount value regType amount carryIn
  | SRTypeLSR -> shiftLSRCForRegAmount value regType amount carryIn
  | SRTypeASR -> shiftASRCForRegAmount value regType amount carryIn
  | SRTypeROR -> shiftRORCForRegAmount value regType amount carryIn
  | SRTypeRRX -> shiftRRXCForRegAmount value regType amount carryIn

/// Logical shift left of a bitstring, with carry output, on page A2-41.
/// function : LSL_C()
let shiftLSLC value regType amount =
  Utils.assertByCond (amount > 0u) InvalidShiftAmountException
  let amount = num (BitVector.ofUInt32 amount regType)
  value << amount, value << (amount .- num1 regType ) |> extractHigh 1<rt>

/// Logical shift left of a bitstring, on page A2-41. function : LSL()
let shiftLSL value regType amount =
  Utils.assertByCond (amount >= 0u) InvalidShiftAmountException
  if amount = 0u then value else shiftLSLC value regType amount |> fst

/// Logical shift right of a bitstring, with carry output, on page A2-41.
/// function : LSR_C()
let shiftLSRC value regType amount =
  Utils.assertByCond (amount > 0u) InvalidShiftAmountException
  let amount' = num (BitVector.ofUInt32 amount regType)
  value >> amount', extract value 1<rt> (amount - 1u |> Convert.ToInt32)

/// Logical shift right of a bitstring, on page A2-41. function : LSR()
let shiftLSR value regType amount =
  Utils.assertByCond (amount >= 0u) InvalidShiftAmountException
  if amount = 0u then value else shiftLSRC value regType amount |> fst

/// Arithmetic shift right of a bitstring, with carry output, on page A2-41.
/// function : ASR_C()
let shiftASRC value regType amount =
  Utils.assertByCond (amount > 0u) InvalidShiftAmountException
  let amount = num (BitVector.ofUInt32 amount regType)
  value ?>> amount, value ?>> (amount .- num1 regType ) |> extractLow 1<rt>

/// Logical shift right of a bitstring, on page A2-41. function : ASR()
let shiftASR value regType amount =
  Utils.assertByCond (amount >= 0u) InvalidShiftAmountException
  if amount = 0u then value else shiftASRC value regType amount |> fst

/// Rotate right of a bitstring, with carry output, on page A2-41.
/// function : ROR_C()
let shiftRORC value regType amount =
  Utils.assertByCond (amount <> 0u) InvalidShiftAmountException
  let m = amount % uint32 (RegType.toBitWidth regType)
  let result = shiftLSR value regType m .| shiftLSL value regType m
  result, extractHigh 1<rt> result

/// Rotate right of a bitstring, on page A2-41. function : ROR()
let shiftROR value regType amount =
  if amount = 0u then value else shiftRORC value regType amount |> fst

/// Rotate right with extend of a bitstring, with carry output, on page A2-41.
/// function : RRX_C()
let shiftRRXC value regType amount =
  let e1 = uint32 (RegType.toBitWidth regType) - 1u |> shiftLSL amount regType
  let e2 = shiftLSR value regType 1u
  e1 .| e2, extractLow 1<rt> value

/// Rotate right with extend of a bitstring, on page A2-41.
/// function : RRX()
let shiftRRX value regType amount = shiftRRXC value regType amount |> fst

/// Perform a specified shift by a specified amount on a bitstring,
/// with carry output, on page A8-292. function : Shift_C()
let shiftC value regType shiftType amount carryIn =
  if amount = 0u then value, carryIn
  else
    match shiftType with
    | SRTypeLSL -> shiftLSLC value regType amount
    | SRTypeLSR -> shiftLSRC value regType amount
    | SRTypeASR -> shiftASRC value regType amount
    | SRTypeROR -> shiftRORC value regType amount
    | SRTypeRRX -> shiftRRXC value regType carryIn

/// Perform a specified shift by a specified amount on a bitstring,
/// on page A8-292.
let shiftForRegAmount value regType shiftType amount carryIn =
  shiftCForRegAmount value regType shiftType amount carryIn |> fst

/// Perform a specified shift by a specified amount on a bitstring,
/// on page A8-292. function : Shift()
let shift value regType shiftType amount carryIn =
  shiftC value regType shiftType amount carryIn |> fst

/// Addition of bitstrings, with carry input and carry/overflow outputs,
/// on page A2-43. function : AddWithCarry()
let addWithCarry src1 src2 carryIn =
  let result = src1 .+ src2 .+ carryIn
  let carryOut = lt result src1
  let overflow = getOverflowFlagOnAdd src1 src2 result
  result, carryOut, overflow

/// Is this ARM instruction set, on page A2-51.
let isInstrSetARM ctxt = not (isSetAPSR_J ctxt) .& not (isSetAPSR_T ctxt)

/// Is this Thumb instruction set, on page A2-51.
let isInstrSetThumb ctxt = not (isSetAPSR_J ctxt) .& isSetAPSR_T ctxt

/// Sets the ARM instruction set, on page A2-51.
let selectARMInstrSet ctxt (builder: StmtBuilder) =
  let apsr = getRegVar ctxt R.APSR
  builder <! (apsr := disablePSR ctxt R.APSR PSR_J)
  builder <! (apsr := disablePSR ctxt R.APSR PSR_T)

/// Sets the ARM instruction set, on page A2-51.
let selectThumbInstrSet ctxt (builder: StmtBuilder) =
  let apsr = getRegVar ctxt R.APSR
  builder <! (apsr := disablePSR ctxt R.APSR PSR_J)
  builder <! (apsr := enablePSR ctxt R.APSR PSR_T)

/// Sets the instruction set currently in use, on page A2-51.
/// SelectInstrSet()
let selectInstrSet ctxt builder = function
  | ArchOperationMode.ARMMode -> selectARMInstrSet ctxt builder
  | ArchOperationMode.ThumbMode -> selectThumbInstrSet ctxt builder
  | _ -> failwith "Invalid ARMMode"

/// Write value to R.PC, without interworking, on page A2-47.
/// function : BranchWritePC()
let branchWritePC ctxt result =
  let resultClear2Bit = maskAndOR result (num0 32<rt>) 32<rt> 2
  let resultClear1Bit = maskAndOR result (num0 32<rt>) 32<rt> 1
  let newPC = ite (isInstrSetARM ctxt) resultClear2Bit resultClear1Bit
  InterJmp (getPC ctxt, newPC)

/// Write value to R.PC, with interworking, on page A2-47.
/// function : BXWritePC()
let bxWritePC ctxt result (builder: StmtBuilder) =
  let lblL0 = lblSymbol "bxWPCL0"
  let lblL1 = lblSymbol "bxWPCL1"
  let lblL2 = lblSymbol "bxWPCL2"
  let lblL3 = lblSymbol "bxWPCL3"
  let lblEnd = lblSymbol "bxWPCEnd"
  let num0 = num0 32<rt>
  let cond1 = extractLow 1<rt> result == b1
  let cond2 = extract result 1<rt> 1 == b1
  let pc = getPC ctxt
  builder <! (CJmp (cond1, Name lblL0, Name lblL1))
  builder <! (LMark lblL0)
  selectThumbInstrSet ctxt builder
  builder <! (InterJmp (pc, maskAndOR result num0 32<rt> 1))
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL1)
  builder <! (CJmp (cond2, Name lblL2, Name lblL3))
  builder <! (LMark lblL2)
  selectARMInstrSet ctxt builder
  builder <! (InterJmp (pc, result))
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL3)
  builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNPREDICTABLE)
  builder <! (LMark lblEnd)

/// Write value to R.PC, with interworking for ARM only from ARMv7
/// , on page A2-47. function : ALUWritePC()
let writePC ctxt result (builder: StmtBuilder) =
  let lblL0 = lblSymbol "writePCL0"
  let lblL1 = lblSymbol "writePCL1"
  let lblEnd = lblSymbol "writePCEnd"
  let cond = isInstrSetARM ctxt
  builder <! (CJmp (cond, Name lblL0, Name lblL1))
  builder <! (LMark lblL0)
  bxWritePC ctxt result builder
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL1)
  builder <! (branchWritePC ctxt result)
  builder <! (LMark lblEnd)

/// Write value to R.PC, with interworking (without it before ARMv5T),
/// on page A2-47. function : LoadWritePC()
let loadWritePC ctxt result = branchWritePC ctxt result

/// Position of rightmost 1 in a bitstring, on page AppxP-2653.
/// function : LowestSetBit()
let lowestSetBit b size =
  let rec loop = function
    | n when n = size -> n
    | n when (b >>> n) &&& 1u = 1u -> n
    | n -> loop (n + 1)
  loop 0

/// Position of leftmost 1 in a bitstring, on page AppxP-2653.
/// function : HighestSetBit()
let highestSetBit b size =
  let rec loop = function
    | n when n < 0 -> -1
    | n when b &&& (1u <<< n) <> 0u -> n
    | n -> loop (n - 1)
  loop (size - 1)

/// Count number of ones in a bitstring, on page AppxP-2653.
/// function : BitCount()
let bitCountFor16Bits expr =
  let numI16 n = BitVector.ofInt32 n 16<rt> |> num
  let n0 = num0 16<rt>
  let n1 = num1 16<rt>
  let res0 = ite (expr .& n1 == n1) n1 n0
  let res1 = ite ((expr >> n1) .& n1 == n1) n1 n0
  let res2 = ite ((expr >> (numI16 2)) .& n1 == n1) n1 n0
  let res3 = ite ((expr >> (numI16 3)) .& n1 == n1) n1 n0
  let res4 = ite ((expr >> (numI16 4)) .& n1 == n1) n1 n0
  let res5 = ite ((expr >> (numI16 5)) .& n1 == n1) n1 n0
  let res6 = ite ((expr >> (numI16 6)) .& n1 == n1) n1 n0
  let res7 = ite ((expr >> (numI16 7)) .& n1 == n1) n1 n0
  let res8 = ite ((expr >> (numI16 8)) .& n1 == n1) n1 n0
  let res9 = ite ((expr >> (numI16 9)) .& n1 == n1) n1 n0
  let res10 = ite ((expr >> (numI16 10)) .& n1 == n1) n1 n0
  let res11 = ite ((expr >> (numI16 11)) .& n1 == n1) n1 n0
  let res12 = ite ((expr >> (numI16 12)) .& n1 == n1) n1 n0
  let res13 = ite ((expr >> (numI16 13)) .& n1 == n1) n1 n0
  let res14 = ite ((expr >> (numI16 14)) .& n1 == n1) n1 n0
  let res15 = ite ((expr >> (numI16 15)) .& n1 == n1) n1 n0
  res0 .+ res1 .+ res2 .+ res3 .+ res4 .+ res5 .+ res6 .+ res7 .+ res8 .+
  res9 .+ res10 .+ res11 .+ res12 .+ res13 .+ res14 .+ res15

/// Count number of ones in a bitstring, on page AppxP-2653.
/// function : BitCount() (for uint32)
let bitCount num size =
  let rec loop cnt res =
    if cnt = size then res
    elif (num >>> cnt) &&& 1u = 1u then loop (cnt + 1) (res + 1)
    else loop (cnt + 1) res
  loop 0 0

/// Number of zeros at left end of bitstring, on page AppxP-2653.
/// function : CountLeadingZeroBits()
let countLeadingZeroBits b size = size - 1 - highestSetBit b size

/// Memory access that must be aligned, at specified privilege level,
/// on page B2-1294. function : MemA[]
let memAWithPriv addr size value = b0  // FIXME

/// Memory access that must be aligned, at current privilege level,
/// on page B2-1294. function : MemA_with_priv[]
let memA addr size value = memAWithPriv addr size value

/// Memory access that must be aligned, at specified privilege level,
/// on page B2-1294. function : MemU_with_priv[]
let memUWithPriv addr size value = b0  // FIXME

/// Memory access without alignment requirement, at current privilege level,
/// on page B2-1295. function : MemU[]
let memU addr size value = memUWithPriv addr size value

/// Value stored when an ARM instruction stores the R.PC, on page A2-47.
/// function : PCStoreValue()
let pcStoreValue ctxt = getPC ctxt

/// Returns TRUE if the implementation includes the Security Extensions,
/// on page B1-1157. function : HaveSecurityExt()
let haveSecurityExt () = b0

/// Returns TRUE if the implementation includes the Virtualization Extensions,
/// on page AppxP-2660. function : HaveVirtExt()
let haveVirtExt () = b0

/// Returns TRUE in Secure state or if no Security Extensions, on page B1-1157.
/// function : IsSecure()
let isSecure ctxt =
  not (haveSecurityExt ()) .| not (isSetSCR_NS ctxt) .|
  (getPSR ctxt R.CPSR PSR_M == (num <| BitVector.ofInt32 0b10110 32<rt>))

/// Test whether mode number is valid, on page B1-1142.
/// function : BadMode()
let isBadMode modeM =
  let cond1 = modeM == (num <| BitVector.ofInt32 0b10000 32<rt>)
  let cond2 = modeM == (num <| BitVector.ofInt32 0b10001 32<rt>)
  let cond3 = modeM == (num <| BitVector.ofInt32 0b10010 32<rt>)
  let cond4 = modeM == (num <| BitVector.ofInt32 0b10011 32<rt>)
  let cond5 = modeM == (num <| BitVector.ofInt32 0b10110 32<rt>)
  let cond6 = modeM == (num <| BitVector.ofInt32 0b10111 32<rt>)
  let cond7 = modeM == (num <| BitVector.ofInt32 0b11010 32<rt>)
  let cond8 = modeM == (num <| BitVector.ofInt32 0b11011 32<rt>)
  let cond9 = modeM == (num <| BitVector.ofInt32 0b11111 32<rt>)
  let ite1 = ite cond9 b0 b1
  let ite2 = ite cond8 b0 ite1
  let ite3 = ite cond7 (haveVirtExt () |> not) ite2
  let ite4 = ite cond6 b0 ite3
  let ite5 = ite cond5 (haveSecurityExt () |> not) ite4
  let ite6 = ite cond4 b0 ite5
  let ite7 = ite cond3 b0 ite6
  let ite8 = ite cond2 b0 ite7
  ite cond1 b0 ite8

/// Return TRUE if current mode is executes at PL1 or higher, on page B1-1142.
/// function : CurrentModeIsNotUser()
let currentModeIsNotUser ctxt =
  let modeM = getPSR ctxt R.CPSR PSR_M
  let modeCond = isBadMode modeM
  let ite1 = ite (modeM == (num <| BitVector.ofInt32 0b10000 32<rt>)) b0 b1
  ite modeCond (Expr.Undefined (1<rt>, "UNPREDICTABLE")) ite1

/// Returns TRUE if current mode is User or System mode, on page B1-1142.
/// function : CurrentModeIsUserOrSystem()
let currentModeIsUserOrSystem ctxt =
  let modeM = getPSR ctxt R.CPSR PSR_M
  let modeCond = isBadMode modeM
  let ite1 = modeM == (num <| BitVector.ofInt32 0b11111 32<rt>)
  let ite2 = ite (modeM == (num <| BitVector.ofInt32 0b10000 32<rt>)) b1 ite1
  ite modeCond (Expr.Undefined (1<rt>, "UNPREDICTABLE")) ite2

/// Returns TRUE if current mode is Hyp mode, on page B1-1142.
/// function : CurrentModeIsHyp()
let currentModeIsHyp ctxt =
  let modeM = getPSR ctxt R.CPSR PSR_M
  let modeCond = isBadMode modeM
  let ite1 = modeM == (num <| BitVector.ofInt32 0b11010 32<rt>)
  ite modeCond (Expr.Undefined (1<rt>, "UNPREDICTABLE")) ite1

/// Bitstring replication, on page AppxP-2652.
/// function : Replicate()
let replicate expr regType lsb width value =
  let v = BitVector.ofUBInt (BigInteger.getMask width <<< lsb) regType
  if value = 0 then expr .& (v |> BitVector.bnot |> num) else expr .| (v |> num)

let writeModeBits ctxt value isExcptReturn (builder: StmtBuilder) =
  let lblL8 = lblSymbol "L8"
  let lblL9 = lblSymbol "L9"
  let lblL10 = lblSymbol "L10"
  let lblL11 = lblSymbol "L11"
  let lblL12 = lblSymbol "L12"
  let lblL13 = lblSymbol "L13"
  let lblL14 = lblSymbol "L14"
  let lblL15 = lblSymbol "L15"
  let lblL16 = lblSymbol "L16"
  let lblL17 = lblSymbol "L17"
  let valueM = value .& maskPSRForMbits
  let cpsrM = getPSR ctxt R.CPSR PSR_M
  let num11010 = (num <| BitVector.ofInt32 0b11010 32<rt>)
  let chkSecure = not (isSecure ctxt)
  let cond1 = chkSecure .& (valueM == (num <| BitVector.ofInt32 0b10110 32<rt>))
  let cond2 = chkSecure .& isSetNSACR_RFR ctxt .&
              (valueM == (num <| BitVector.ofInt32 0b10001 32<rt>))
  let cond3 = chkSecure .& (valueM == num11010)
  let cond4 = chkSecure .& (cpsrM != num11010) .& (valueM == num11010)
  let cond5 = (cpsrM == num11010) .& (valueM != num11010)
  builder <! (CJmp (cond1, Name lblL8, Name lblL9))
  builder <! (LMark lblL8)
  builder <! (SideEffect UndefinedInstr)  // FIXME: (use UNPREDICTABLE)
  builder <! (LMark lblL9)
  builder <! (CJmp (cond2, Name lblL10, Name lblL11))
  builder <! (LMark lblL10)
  builder <! (SideEffect UndefinedInstr)  // FIXME: (use UNPREDICTABLE)
  builder <! (LMark lblL11)
  builder <! (CJmp (cond3, Name lblL12, Name lblL13))
  builder <! (LMark lblL12)
  builder <! (SideEffect UndefinedInstr)  // FIXME: (use UNPREDICTABLE)
  builder <! (LMark lblL13)
  builder <! (CJmp (cond4, Name lblL14, Name lblL15))
  builder <! (LMark lblL14)
  builder <! (SideEffect UndefinedInstr)  // FIXME: (use UNPREDICTABLE)
  builder <! (LMark lblL15)
  builder <! (CJmp (cond5, Name lblL16, Name lblL17))
  builder <! (LMark lblL16)
  if Operators.not isExcptReturn then
    builder <! (SideEffect UndefinedInstr)  // FIXME: (use UNPREDICTABLE)
  else ()
  builder <! (LMark lblL17)
  let mValue = value .& maskPSRForMbits
  builder <! (getRegVar ctxt R.CPSR := disablePSR ctxt R.CPSR PSR_M .| mValue)

/// R.CPSR write by an instruction, on page B1-1152.
/// function : CPSRWriteByInstr()
let cpsrWriteByInstr ctxt value bytemask isExcptReturn (builder: StmtBuilder) =
  let cpsr = getRegVar ctxt R.CPSR
  let privileged = currentModeIsNotUser ctxt
  if bytemask &&& 0b1000 = 0b1000 then
    let nzcvValue = value .& maskPSRForCondbits
    let qValue = value .& maskPSRForQbit
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_Cond .| nzcvValue)
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_Q .| qValue)
    if isExcptReturn then
      let itValue = value .& maskPSRForIT10bits
      let jValue = value .& maskPSRForJbit
      builder <! (cpsr := disablePSR ctxt R.CPSR PSR_IT10 .| itValue)
      builder <! (cpsr := disablePSR ctxt R.CPSR PSR_J .| jValue)
    else ()
  else ()

  if bytemask &&& 0b0100 = 0b0100 then
    let geValue = value .& maskPSRForGEbits
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_GE .| geValue)
  else ()

  if bytemask &&& 0b0010 = 0b0010 then
    let lblL0 = lblSymbol "cpsrWriteByInstrL0"
    let lblL1 = lblSymbol "cpsrWriteByInstrL1"
    if isExcptReturn then
      let itValue = value .& maskPSRForIT72bits
      builder <! (cpsr := disablePSR ctxt R.CPSR PSR_IT72 .| itValue)
    else ()
    let eValue = value .& maskPSRForEbit
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_E .| eValue)
    let cond =
      privileged .& (isSecure ctxt .| isSetSCR_AW ctxt .| haveVirtExt ())
    builder <! (CJmp (cond, Name lblL0, Name lblL1))
    builder <! (LMark lblL0)
    let aValue = value .& maskPSRForAbit
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_A .| aValue)
    builder <! (LMark lblL1)
  else ()

  if bytemask &&& 0b0001 = 0b0001 then
    let lblL2 = lblSymbol "cpsrWriteByInstrL2"
    let lblL3 = lblSymbol "cpsrWriteByInstrL3"
    let lblL4 = lblSymbol "cpsrWriteByInstrL4"
    let lblL5 = lblSymbol "cpsrWriteByInstrL5"
    let lblL6 = lblSymbol "cpsrWriteByInstrL6"
    let lblL7 = lblSymbol "cpsrWriteByInstrL7"
    let lblEnd = lblSymbol "cpsrWriteByInstrEnd"
    let nmfi = isSetSCTLR_NMFI ctxt
    builder <! (CJmp (privileged, Name lblL2, Name lblL3))
    builder <! (LMark lblL2)
    let iValue = value .& maskPSRForIbit
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_I .| iValue)
    builder <! (LMark lblL3)

    let chkValueF = (value .& maskPSRForFbit) == num0 32<rt>
    let cond = privileged .& (not nmfi .| chkValueF) .&
               (isSecure ctxt .| isSetSCR_FW ctxt .| haveVirtExt ())
    builder <! (CJmp (cond, Name lblL4, Name lblL5))
    builder <! (LMark lblL4)
    let fValue = value .& maskPSRForFbit
    builder <! (cpsr := disablePSR ctxt R.CPSR PSR_F .| fValue)
    builder <! (LMark lblL5)

    if isExcptReturn then
      let tValue = value .& maskPSRForTbit
      builder <! (cpsr := disablePSR ctxt R.CPSR PSR_T .| tValue)
    else ()

    builder <! (CJmp (privileged, Name lblL6, Name lblL7))
    builder <! (LMark lblL6)
    builder <! (SideEffect UndefinedInstr) // FIXME: (use UNPREDICTABLE)
    builder <! (Jmp (Name lblEnd))
    builder <! (LMark lblL7)
    writeModeBits ctxt value isExcptReturn builder
    builder <! (LMark lblEnd)
  else ()

let transShiftOprs ctxt opr1 opr2 =
  match opr1, opr2 with
  | Register _, Shift (typ, Imm imm) ->
    let e = transOprToExpr ctxt opr1
    let carryIn = getCarryFlag ctxt
    shift e 32<rt> typ imm carryIn
  | _ -> raise InvalidOperandException

let parseOprOfMVNS insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) -> transTwoOprs insInfo ctxt
  | ThreeOperands (opr1, opr2, opr3) ->
    transOprToExpr ctxt opr1, transShiftOprs ctxt opr2 opr3
  | _ -> raise InvalidOperandException

let transTwoOprsOfADC insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    e1, e1, shift e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let transThreeOprsOfADC insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (_, _, Immediate _) -> transThreeOprs insInfo ctxt
  | ThreeOperands (Register _, Register _, Register _) ->
    let carryIn = getCarryFlag ctxt
    let e1, e2, e3 = transThreeOprs insInfo ctxt
    e1, e2, shift e3 32<rt> SRTypeLSL 0u carryIn
  | _ -> raise InvalidOperandException

let transFourOprsOfADC insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (opr1, opr2, opr3 , (Shift (_, Imm _) as opr4)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    e1, e2, transShiftOprs ctxt opr3 opr4
  | FourOperands (opr1, opr2, opr3 , RegShift (typ, reg)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let amount = extractLow 8<rt> (getRegVar ctxt reg) |> zExt 32<rt>
    e1, e2, shiftForRegAmount e3 32<rt> typ amount (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let parseOprOfADC insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfADC insInfo ctxt
  | ThreeOperands _ -> transThreeOprsOfADC insInfo ctxt
  | FourOperands _ -> transFourOprsOfADC insInfo ctxt
  | _ -> raise InvalidOperandException

let isCondPassed (cond: Condition option) =
  if cond.IsNone then true
  else match Option.get cond with
       | Condition.AL | Condition.UN -> true
       | _ -> false

let startMark insInfo ctxt lblPass lblFail isCondPass builder =
  builder <! (ISMark (insInfo.Address, insInfo.NumBytes))
  if isCondPass then ()
  else
    let cond = conditionPassed ctxt (Option.get insInfo.Condition)
    builder <! (CJmp (cond, Name lblPass, Name lblFail))
    builder <! (LMark lblPass)

let endMark insInfo lblFail isCondPass builder =
  if isCondPass then ()
  else builder <! (LMark lblFail)
  builder <! (IEMark (uint64 insInfo.NumBytes + insInfo.Address))
  builder

let sideEffects insInfo addr name =
  let builder = new StmtBuilder (4)
  builder <! (ISMark (addr, insInfo.NumBytes))
  builder <! (SideEffect name)
  builder <! (IEMark (uint64 insInfo.NumBytes + insInfo.Address))
  builder

let nop insInfo addr =
  let builder = new StmtBuilder (4)
  builder <! (ISMark (addr, insInfo.NumBytes))
  builder <! (IEMark (uint64 insInfo.NumBytes + insInfo.Address))
  builder

let coprocOp insInfo addr = nop insInfo addr

let adc isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2 = parseOprOfADC insInfo ctxt
  let res, carryOut, overflow = addWithCarry src1 src2 (getCarryFlag ctxt)
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
      builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let transTwoOprsOfADD insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) ->
    let e1, e2 = transTwoOprs insInfo ctxt in e1, e1, e2
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    e1, e1, shift e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let transThreeOprsOfADD insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (_, _, Immediate _) -> transThreeOprs insInfo ctxt
  | ThreeOperands (Register _, Register _, Register _) ->
    let carryIn = getCarryFlag ctxt
    let e1, e2, e3 = transThreeOprs insInfo ctxt
    e1, e2, shift e3 32<rt> SRTypeLSL 0u carryIn
  | _ -> raise InvalidOperandException

let transFourOprsOfADD insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (opr1, opr2, opr3 , (Shift (_, Imm _) as opr4)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    e1, e2, transShiftOprs ctxt opr3 opr4
  | FourOperands (opr1, opr2, opr3 , RegShift (typ, reg)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let amount = extractLow 8<rt> (getRegVar ctxt reg) |> zExt 32<rt>
    e1, e2, shiftForRegAmount e3 32<rt> typ amount (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let parseOprOfADD insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfADD insInfo ctxt
  | ThreeOperands _ -> transThreeOprsOfADD insInfo ctxt
  | FourOperands _ -> transFourOprsOfADD insInfo ctxt
  | _ -> raise InvalidOperandException

let add isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2 = parseOprOfADD insInfo ctxt
  let res, carryOut, overflow = addWithCarry src1 src2 (num0 32<rt>)
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
      builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
    else ()
  endMark insInfo lblCondFail isCondPass builder

/// Align integer or bitstring to multiple of an integer, on page AppxP-2655
/// function : Align()
let align e1 e2 = e2 .* (e1 ./ e2)

let transLableOprsOfBL insInfo targetMode imm =
  let addr = bvOfBaseAddr insInfo.Address
  let pc = match targetMode with
           | ArchOperationMode.ARMMode -> align addr (num (BitVector.ofInt32 4 32<rt>))
           | ArchOperationMode.ThumbMode -> addr
           | _ -> raise InvalidTargetArchModeException
  pc .+ (num <| BitVector.ofInt64 imm 32<rt>)

let targetModeOfBL insInfo =
  match insInfo.Opcode, insInfo.Mode with
  | Op.BL, mode -> mode
  | Op.BLX, ArchOperationMode.ARMMode -> ArchOperationMode.ThumbMode
  | Op.BLX, ArchOperationMode.ThumbMode -> ArchOperationMode.ARMMode
  | _ -> failwith "Invalid ARMMode"

let parseOprOfBL insInfo =
  let targetMode = targetModeOfBL insInfo
  match insInfo.Operands with
  | OneOperand (Memory (LiteralMode imm)) ->
    transLableOprsOfBL insInfo targetMode imm, targetMode
  | _ -> raise InvalidOperandException

let blxWithReg insInfo reg ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let lr = getRegVar ctxt R.LR
  let addr = bvOfBaseAddr insInfo.Address
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if insInfo.Mode = ArchOperationMode.ARMMode then
    builder <! (lr := addr .- (num <| BitVector.ofInt32 4 32<rt>))
  else
    let addr = addr .- (num <| BitVector.ofInt32 2 32<rt>)
    builder <! (lr := maskAndOR addr (num1 32<rt>) 32<rt> 1)
  bxWritePC ctxt (getRegVar ctxt reg) builder
  endMark insInfo lblCondFail isCondPass builder

let bl insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let e, targetMode = parseOprOfBL insInfo
  let lr = getRegVar ctxt R.LR
  let addr = bvOfBaseAddr insInfo.Address
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if insInfo.Mode = ArchOperationMode.ARMMode then
    builder <! (lr := addr .- (num <| BitVector.ofInt32 4 32<rt>))
  else builder <! (lr := maskAndOR addr (num1 32<rt>) 32<rt> 1)
  selectInstrSet ctxt builder targetMode
  builder <! (branchWritePC ctxt e)
  endMark insInfo lblCondFail isCondPass builder

let branchWithLink insInfo ctxt =
  match insInfo.Operands with
  | OneOperand (Register reg) -> blxWithReg insInfo reg ctxt
  | _ -> bl insInfo ctxt

let parseOprOfPUSHPOP insInfo =
  match insInfo.Operands with
  | OneOperand (Register r) -> regsToUInt32 [ r ] //, true (unAlignedAllowed)
  | OneOperand (RegList regs) -> regsToUInt32 regs //, false (unAlignedAllowed)
  | _ -> raise InvalidOperandException

let pushLoop ctxt numOfReg addr (builder: StmtBuilder) =
  let loop addr count =
    if (numOfReg >>> count) &&& 1u = 1u then
      if count = 13 && count <> lowestSetBit numOfReg 32 then
        builder <! (loadLE 32<rt> addr := (Expr.Undefined (32<rt>, "UNKNOWN")))
      else
        let reg = count |> byte |> OperandHelper.getRegister
        builder <! (loadLE 32<rt> addr := getRegVar ctxt reg)
      addr .+ (num <| BitVector.ofInt32 4 32<rt>)
    else addr
  List.fold loop addr [ 0 .. 14 ]

let push insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let t0 = tmpVar 32<rt>
  let sp = getRegVar ctxt R.SP
  let numOfReg = parseOprOfPUSHPOP insInfo
  let stackWidth = 4 * bitCount numOfReg 16
  let addr = sp .- (num <| BitVector.ofInt32 stackWidth 32<rt>)
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := addr)
  let addr = pushLoop ctxt numOfReg t0 builder
  if (numOfReg >>> 15 &&& 1u) = 1u then
    builder <! (loadLE 32<rt> addr := pcStoreValue ctxt)
  else ()
  builder <! (sp := t0)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfSUB insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> let e1, e2 = transTwoOprs insInfo ctxt in e1, e1, e2
  | ThreeOperands _ -> transThreeOprsOfADD insInfo ctxt
  | FourOperands _ -> transFourOprsOfADD insInfo ctxt
  | _ -> raise InvalidOperandException

let sub isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2 = parseOprOfSUB insInfo ctxt
  let res, carryOut, overflow = addWithCarry src1 (not src2) (num1 32<rt>)
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
      builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
    else ()
  endMark insInfo lblCondFail isCondPass builder

/// B9.3.19 SUBS R.PC, R.LR (Thumb), on page B9-2008
let subsPCLRThumb insInfo ctxt =
  let builder = new StmtBuilder (64)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let lblL0 = lblSymbol "subsPCLRThumbL0"
  let lblL1 = lblSymbol "subsPCLRThumbL1"
  let lblL2 = lblSymbol "subsPCLRThumbL2"
  let lblL3 = lblSymbol "subsPCLRThumbL3"
  let lblEnd = lblSymbol "subsPCLRThumbEnd"
  let _, _, src2 = parseOprOfSUB insInfo ctxt
  let pc = getPC ctxt
  let result, _, _ = addWithCarry pc (not src2) (num1 32<rt>)
  let cond = getPSR ctxt R.CPSR PSR_M ==
             (num <| BitVector.ofInt32 0b11010 32<rt>)
             .& isSetCPSR_J ctxt .& isSetCPSR_T ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (CJmp (currentModeIsUserOrSystem ctxt, Name lblL0, Name lblL1))
  builder <! (LMark lblL0)
  builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNPREDICTABLE)
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL1)
  cpsrWriteByInstr ctxt (getRegVar ctxt R.SPSR) 0b1111 true builder
  builder <! (CJmp (cond, Name lblL2, Name lblL3))
  builder <! (LMark lblL2)
  builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNPREDICTABLE)
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL3)
  builder <! (branchWritePC ctxt result)
  builder <! (LMark lblEnd)
  endMark insInfo lblCondFail isCondPass builder

let parseResultOfSUBAndRela insInfo ctxt =
  match insInfo.Opcode with
  | Op.ANDS -> let _, src1, src2 = parseOprOfADC insInfo ctxt in src1.& src2
  | Op.EORS -> let _, src1, src2 = parseOprOfADC insInfo ctxt in src1 <+> src2
  | Op.SUBS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               let r, _, _ = addWithCarry src1 (not src2) (num1 32<rt>) in r
  | Op.RSBS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               let r, _, _ = addWithCarry (not src1) src2 (num1 32<rt>) in r
  | Op.ADDS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               let r, _, _ = addWithCarry src1 src2 (num0 32<rt>) in r
  | Op.ADCS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               let r, _, _ = addWithCarry src1 src2 (getCarryFlag ctxt) in r
  | Op.SBCS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               let r, _, _ = addWithCarry src1 (not src2) (getCarryFlag ctxt)
               r
  | Op.RSCS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               let r, _, _ = addWithCarry (not src1) src2 (getCarryFlag ctxt)
               r
  | Op.ORRS -> let _, src1, src2 = parseOprOfADC insInfo ctxt in src1 .| src2
  | Op.MOVS -> let _, src = transTwoOprs insInfo ctxt in src
  | Op.ASRS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               shiftForRegAmount src1 32<rt> SRTypeASR src2 (getCarryFlag ctxt)
  | Op.LSLS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               shiftForRegAmount src1 32<rt> SRTypeLSL src2 (getCarryFlag ctxt)
  | Op.LSRS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               shiftForRegAmount src1 32<rt> SRTypeLSR src2 (getCarryFlag ctxt)
  | Op.RORS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               shiftForRegAmount src1 32<rt> SRTypeROR src2 (getCarryFlag ctxt)
  | Op.RRXS ->
    let _, src = transTwoOprs insInfo ctxt
    shiftForRegAmount src 32<rt> SRTypeRRX (num1 32<rt>) (getCarryFlag ctxt)
  | Op.BICS -> let _, src1, src2 = parseOprOfADC insInfo ctxt
               src1 .& (not src2)
  | Op.MVNS -> let _, src = parseOprOfMVNS insInfo ctxt in not src
  | _ -> raise InvalidOperandException

/// B9.3.20 SUBS R.PC, R.LR and related instruction (ARM), on page B9-2010
let subsAndRelatedInstr insInfo ctxt =
  let builder = new StmtBuilder (64)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let lblL0 = lblSymbol "subsAndRelatedInstrL0"
  let lblL1 = lblSymbol "subsAndRelatedInstrL1"
  let lblL2 = lblSymbol "subsAndRelatedInstrL2"
  let lblL3 = lblSymbol "subsAndRelatedInstrL3"
  let lblL4 = lblSymbol "subsAndRelatedInstrL4"
  let lblL5 = lblSymbol "subsAndRelatedInstrL5"
  let lblEnd = lblSymbol "subsAndRelatedInstrEnd"
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (CJmp (currentModeIsHyp ctxt, Name lblL0, Name lblL1))
  builder <! (LMark lblL0)
  builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNDEFINED)
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL1)
  builder <! (CJmp (currentModeIsUserOrSystem ctxt, Name lblL2, Name lblL3))
  builder <! (LMark lblL2)
  builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNPREDICTABLE)
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL3)
  cpsrWriteByInstr ctxt (getRegVar ctxt R.SPSR) 0b1111 true builder
  let cond = getPSR ctxt R.CPSR PSR_M ==
             (num <| BitVector.ofInt32 0b11010 32<rt>)
             .& isSetCPSR_J ctxt .& isSetCPSR_T ctxt
  builder <! (CJmp (cond, Name lblL4, Name lblL5))
  builder <! (LMark lblL4)
  builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNPREDICTABLE)
  builder <! (Jmp (Name lblEnd))
  builder <! (LMark lblL5)
  builder <! (result := parseResultOfSUBAndRela insInfo ctxt)
  builder <! (branchWritePC ctxt result)
  builder <! (LMark lblEnd)
  endMark insInfo lblCondFail isCondPass builder

let transTwoOprsOfAND insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    let shifted, carryOut = shiftC e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    e1, e2, shifted, carryOut
  | _ -> raise InvalidOperandException

let transThreeOprsOfAND insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (_, _, Immediate _) ->
    let e1, e2, e3 = transThreeOprs insInfo ctxt
    e1, e2, e3, getCarryFlag ctxt
  | _ -> raise InvalidOperandException

let transFourOprsOfAND insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (opr1, opr2, opr3 , Shift (typ, Imm imm)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src1 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let shifted, carryOut = shiftC e3 32<rt> typ imm carryIn
    dst, src1, shifted, carryOut
  | FourOperands (opr1, opr2, opr3 , RegShift (typ, reg)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src1 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let amount = extractLow 8<rt> (getRegVar ctxt reg) |> zExt 32<rt>
    let shifted, carryOut = shiftCForRegAmount e3 32<rt> typ amount carryIn
    dst, src1, shifted, carryOut
  | _ -> raise InvalidOperandException

let parseOprOfAND insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfAND insInfo ctxt
  | ThreeOperands _ -> transThreeOprsOfAND insInfo ctxt
  | FourOperands _ -> transFourOprsOfAND insInfo ctxt
  | _ -> raise InvalidOperandException

let transAND isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2, carryOut = parseOprOfAND insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := src1 .& src2)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let mov isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, res = transTwoOprs insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let eor isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2, carryOut = parseOprOfAND insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := src1 <+> src2)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let transFourOprsOfRSB insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (opr1, opr2, opr3 , (Shift (_, Imm _) as opr4)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    e1, e2, transShiftOprs ctxt opr3 opr4
  | FourOperands (opr1, opr2, opr3 , RegShift (typ, reg)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let amount = extractLow 8<rt> (getRegVar ctxt reg) |> zExt 32<rt>
    e1, e2, shiftForRegAmount e3 32<rt> typ amount (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let parseOprOfRSB insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands _ -> transThreeOprs insInfo ctxt
  | FourOperands _ -> transFourOprsOfRSB insInfo ctxt
  | _ -> raise InvalidOperandException

let rsb isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2 = parseOprOfRSB insInfo ctxt
  let res, carryOut, overflow = addWithCarry (not src1) src2 (num1 32<rt>)
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
      builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let transTwoOprsOfSBC insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    e1, e1, shift e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let transFourOprsOfSBC insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (opr1, opr2, opr3 , (Shift (_, Imm _) as opr4)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    e1, e2, transShiftOprs ctxt opr3 opr4
  | FourOperands (opr1, opr2, opr3 , RegShift (typ, reg)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let amount = extractLow 8<rt> (getRegVar ctxt reg) |> zExt 32<rt>
    e1, e2, shiftForRegAmount e3 32<rt> typ amount (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let parseOprOfSBC insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfSBC insInfo ctxt
  | ThreeOperands _ -> transThreeOprs insInfo ctxt
  | FourOperands _ -> transFourOprsOfSBC insInfo ctxt
  | _ -> raise InvalidOperandException

let sbc isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2 = parseOprOfSBC insInfo ctxt
  let r, carryOut, overflow = addWithCarry src1 (not src2) (getCarryFlag ctxt)
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := r)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
      builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let transFourOprsOfRSC insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (opr1, opr2, opr3 , (Shift (_, Imm _) as opr4)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    e1, e2, transShiftOprs ctxt opr3 opr4
  | FourOperands (opr1, opr2, opr3 , RegShift (typ, reg)) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let e3 = transOprToExpr ctxt opr3
    let amount = extractLow 8<rt> (getRegVar ctxt reg) |> zExt 32<rt>
    e1, e2, shiftForRegAmount e3 32<rt> typ amount (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let parseOprOfRSC insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands _ -> transThreeOprs insInfo ctxt
  | FourOperands _ -> transFourOprsOfRSB insInfo ctxt
  | _ -> raise InvalidOperandException

let rsc isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2 = parseOprOfRSC insInfo ctxt
  let r, carryOut, overflow = addWithCarry (not src1) src2 (getCarryFlag ctxt)
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := r)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
      builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let orr isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2, carryOut = parseOprOfAND insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := src1 .| src2)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let bic isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src1, src2, carryOut = parseOprOfAND insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := src1 .& (not src2))
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let transTwoOprsOfMVN insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    e1, e2, getCarryFlag ctxt
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    let shifted, carryOut = shiftC e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    e1, shifted, carryOut
  | _ -> raise InvalidOperandException

let transThreeOprsOfMVN insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (opr1, opr2, Shift (typ, Imm imm)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let shifted, carryOut = shiftC src 32<rt> typ imm carryIn
    dst, shifted, carryOut
  | ThreeOperands (opr1, opr2, RegShift (typ, rs)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let amount = extractLow 8<rt> (getRegVar ctxt rs) |> zExt 32<rt>
    let shifted, carryOut = shiftCForRegAmount src 32<rt> typ amount carryIn
    dst, shifted, carryOut
  | _ -> raise InvalidOperandException

let parseOprOfMVN insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfMVN insInfo ctxt
  | ThreeOperands _ -> transThreeOprsOfMVN insInfo ctxt
  | _ -> raise InvalidOperandException

let mvn isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src, carryOut = parseOprOfMVN insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := not src)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let getImmShiftFromShiftType imm = function
  | SRTypeLSL | SRTypeROR -> imm
  | SRTypeLSR -> if imm = 0ul then 32ul else imm
  | SRTypeASR -> if imm = 0ul then 32ul else imm
  | SRTypeRRX -> 1ul

let transTwoOprsOfShiftInstr insInfo shiftTyp ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Register _) when shiftTyp = SRTypeRRX ->
    let carryIn = getCarryFlag ctxt
    let e1, e2 = transTwoOprs insInfo ctxt
    let result, carryOut = shiftC e2 32<rt> shiftTyp 1ul carryIn
    e1, result, carryOut
  | TwoOperands (Register _, Register _) ->
    let carryIn = getCarryFlag ctxt
    let e1, e2 = transTwoOprs insInfo ctxt
    let shiftN = extractLow 8<rt> e2 |> zExt 32<rt>
    let result, carryOut = shiftCForRegAmount e1 32<rt> shiftTyp shiftN carryIn
    e1, result, carryOut
  | _ -> raise InvalidOperandException

let transThreeOprsOfShiftInstr insInfo shiftTyp ctxt =
  match insInfo.Operands with
  | ThreeOperands (opr1, opr2, Immediate imm) ->
    let e1, e2 = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let shiftN = getImmShiftFromShiftType (uint32 imm) shiftTyp
    let shifted, carryOut = shiftC e2 32<rt> shiftTyp shiftN (getCarryFlag ctxt)
    e1, shifted, carryOut
  | ThreeOperands (_, _, Register _) ->
    let carryIn = getCarryFlag ctxt
    let e1, e2, e3 = transThreeOprs insInfo ctxt
    let amount = extractLow 8<rt> e3 |> zExt 32<rt>
    let shifted, carryOut = shiftCForRegAmount e2 32<rt> shiftTyp amount carryIn
    e1, shifted, carryOut
  | _ -> raise InvalidOperandException

let parseOprOfShiftInstr insInfo shiftTyp ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfShiftInstr insInfo shiftTyp ctxt
  | ThreeOperands _ -> transThreeOprsOfShiftInstr insInfo shiftTyp ctxt
  | _ -> raise InvalidOperandException

let shiftInstr isSetFlags insInfo typ ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let result = tmpVar 32<rt>
  let dst, res, carryOut = parseOprOfShiftInstr insInfo typ ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  if dst = getPC ctxt then writePC ctxt result builder
  else
    builder <! (dst := result)
    if isSetFlags then
      let apsr = getRegVar ctxt R.APSR
      builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
      builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
      builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
    else ()
  endMark insInfo lblCondFail isCondPass builder

let subs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
    when insInfo.Mode = ArchOperationMode.ThumbMode -> subsPCLRThumb insInfo ctxt
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> sub isSetFlags insInfo ctxt

let adds isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> add isSetFlags insInfo ctxt

let adcs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> adc isSetFlags insInfo ctxt

let ands isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> transAND isSetFlags insInfo ctxt

let movs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register R.PC, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> mov isSetFlags insInfo ctxt

let eors isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> eor isSetFlags insInfo ctxt

let rsbs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> rsb isSetFlags insInfo ctxt

let sbcs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> sbc isSetFlags insInfo ctxt

let rscs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> rsc isSetFlags insInfo ctxt

let orrs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> orr isSetFlags insInfo ctxt

let bics isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _)
  | FourOperands (Register R.PC, _, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> bic isSetFlags insInfo ctxt

let mvns isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register R.PC, _)
  | ThreeOperands (Register R.PC, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> mvn isSetFlags insInfo ctxt

let asrs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> shiftInstr isSetFlags insInfo SRTypeASR ctxt

let lsls isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> shiftInstr isSetFlags insInfo SRTypeLSL ctxt

let lsrs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> shiftInstr isSetFlags insInfo SRTypeLSR ctxt

let rors isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register R.PC, _, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> shiftInstr isSetFlags insInfo SRTypeROR ctxt

let rrxs isSetFlags insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register R.PC, _) -> subsAndRelatedInstr insInfo ctxt
  | _ -> shiftInstr isSetFlags insInfo SRTypeRRX ctxt

let clz insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src = transTwoOprs insInfo ctxt
  let lblL0 = lblSymbol "L0"
  let lblL1 = lblSymbol "L1"
  let lblL2 = lblSymbol "L2"
  let lblL3 = lblSymbol "L3"
  let lblL4 = lblSymbol "L4"
  let lblL5 = lblSymbol "L5"
  let numSize = (num <| BitVector.ofInt32 32 32<rt>)
  let numMinusOne = (num <| BitVector.ofInt32 -1 32<rt>)
  let t1, result = tmpVar 32<rt>, tmpVar 32<rt>
  let cond1 = lt t1 (num0 32<rt>)
  let cond2 = src .& ((num1 32<rt>) << t1) != (num0 32<rt>)
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t1 := numSize .- (num1 32<rt>))
  builder <! (LMark lblL0)
  builder <! (CJmp (cond1, Name lblL1, Name lblL2))
  builder <! (LMark lblL1)
  builder <! (result := numMinusOne)
  builder <! (Jmp (Name lblL5))
  builder <! (LMark lblL2)
  builder <! (CJmp (cond2, Name lblL3, Name lblL4))
  builder <! (LMark lblL3)
  builder <! (result := t1)
  builder <! (Jmp (Name lblL5))
  builder <! (LMark lblL4)
  builder <! (t1 := t1 .- (num1 32<rt>))
  builder <! (Jmp (Name lblL0))
  builder <! (LMark lblL5)
  builder <! (dst := numSize .- (num1 32<rt>) .- result)
  endMark insInfo lblCondFail isCondPass builder

let transTwoOprsOfCMN insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) -> transTwoOprs insInfo ctxt
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    let shifted = shift e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    e1, shifted
  | _ -> raise InvalidOperandException

let transThreeOprsOfCMN insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (opr1, opr2, Shift (typ, Imm imm)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let shifted = shift src 32<rt> typ imm carryIn
    dst, shifted
  | ThreeOperands (opr1, opr2, RegShift (typ, rs)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let amount = extractLow 8<rt> (getRegVar ctxt rs) |> zExt 32<rt>
    let shifted = shiftForRegAmount src 32<rt> typ amount carryIn
    dst, shifted
  | _ -> raise InvalidOperandException

let parseOprOfCMN insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfCMN insInfo ctxt
  | ThreeOperands _ -> transThreeOprsOfCMN insInfo ctxt
  | _ -> raise InvalidOperandException

let cmn insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, src = parseOprOfCMN insInfo ctxt
  let result = tmpVar 32<rt>
  let res, carryOut, overflow = addWithCarry dst src (num0 32<rt>)
  let apsr = getRegVar ctxt R.APSR
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
  builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
  builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
  builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
  endMark insInfo lblCondFail isCondPass builder

let mla isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rn, rm, ra = transFourOprs insInfo ctxt
  let r = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (r := extractLow 32<rt> (zExt 64<rt> rn .* zExt 64<rt> rm .+
                                     zExt 64<rt> ra))
  builder <! (rd := r)
  if isSetFlags then
    let apsr = getRegVar ctxt R.APSR
    builder <! (apsr := extractHigh 1<rt> r |> setPSR ctxt R.APSR PSR_N)
    builder <! (apsr := r == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
  else ()
  endMark insInfo lblCondFail isCondPass builder

let transTwoOprsOfCMP insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) -> transTwoOprs insInfo ctxt
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    e1, shift e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
  | _ -> raise InvalidOperandException

let transThreeOprsOfCMP insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (opr1, opr2, Shift (typ, Imm imm)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    dst, shift src 32<rt> typ imm carryIn
  | ThreeOperands (opr1, opr2, RegShift (typ, rs)) ->
    let carryIn = getCarryFlag ctxt
    let dst, src = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let amount = extractLow 8<rt> (getRegVar ctxt rs) |> zExt 32<rt>
    dst, shiftForRegAmount src 32<rt> typ amount carryIn
  | _ -> raise InvalidOperandException

let parseOprOfCMP insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands _ -> transTwoOprsOfCMP insInfo ctxt
  | ThreeOperands _ -> transThreeOprsOfCMP insInfo ctxt
  | _ -> raise InvalidOperandException

let cmp insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let e1, e2 = parseOprOfCMP insInfo ctxt
  let result = tmpVar 32<rt>
  let res, carryOut, overflow = addWithCarry e1 (not e2) (num1 32<rt>)
  let apsr = getRegVar ctxt R.APSR
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := res)
  builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
  builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
  builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
  builder <! (apsr := overflow |> setPSR ctxt R.APSR PSR_V)
  endMark insInfo lblCondFail isCondPass builder

let umlal isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rdLo, rdHi, rn, rm = transFourOprs insInfo ctxt
  let result = tmpVar 64<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := zExt 64<rt> rn .* zExt 64<rt> rm .+ concat rdLo rdHi)
  builder <! (rdHi := extractHigh 32<rt> result)
  builder <! (rdLo := extractLow 32<rt> result)
  if isSetFlags then
    let apsr = getRegVar ctxt R.APSR
    builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
    builder <! (apsr := result == num0 64<rt> |> setPSR ctxt R.APSR PSR_Z)
  else ()
  endMark insInfo lblCondFail isCondPass builder

let umull isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rdLo, rdHi, rn, rm = transFourOprs insInfo ctxt
  let result = tmpVar 64<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := zExt 64<rt> rn .* zExt 64<rt> rm)
  builder <! (rdHi := extractHigh 32<rt> result)
  builder <! (rdLo := extractLow 32<rt> result)
  if isSetFlags then
    let apsr = getRegVar ctxt R.APSR
    builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
    builder <! (apsr := result == num0 64<rt> |> setPSR ctxt R.APSR PSR_Z)
  else ()
  endMark insInfo lblCondFail isCondPass builder

let transOprsOfTEQ insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) ->
    let rn, imm = transTwoOprs insInfo ctxt
    rn, imm, getCarryFlag ctxt
  | ThreeOperands (opr1, opr2, Shift (typ, Imm imm)) ->
    let carryIn = getCarryFlag ctxt
    let rn, rm = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let shifted, carryOut = shiftC rm 32<rt> typ imm carryIn
    rn, shifted, carryOut
  | ThreeOperands (opr1, opr2, RegShift (typ, rs)) ->
    let carryIn = getCarryFlag ctxt
    let rn, rm = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let amount = extractLow 8<rt> (getRegVar ctxt rs) |> zExt 32<rt>
    let shifted, carryOut = shiftCForRegAmount rm 32<rt> typ amount carryIn
    rn, shifted, carryOut
  | _ -> raise InvalidOperandException

let teq insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let src1, src2, carryOut = transOprsOfTEQ insInfo ctxt
  let result = tmpVar 32<rt>
  let apsr = getRegVar ctxt R.APSR
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := src1 <+> src2)
  builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
  builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
  builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
  endMark insInfo lblCondFail isCondPass builder

let mul isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rn, rm = transThreeOprs insInfo ctxt
  let result = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := extractLow 32<rt> (zExt 64<rt> rn .* zExt 64<rt> rm))
  builder <! (rd := result)
  if isSetFlags then
    let apsr = getRegVar ctxt R.APSR
    builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
    builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
  else ()
  endMark insInfo lblCondFail isCondPass builder

let transOprsOfTST insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register _, Immediate _) ->
    let rn, imm = transTwoOprs insInfo ctxt
    rn, imm, getCarryFlag ctxt
  | TwoOperands (Register _, Register _) ->
    let e1, e2 = transTwoOprs insInfo ctxt
    let shifted, carryOut = shiftC e2 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    e1, shifted, carryOut
  | ThreeOperands (opr1, opr2, Shift (typ, Imm imm)) ->
    let carryIn = getCarryFlag ctxt
    let rn, rm = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let shifted, carryOut = shiftC rm 32<rt> typ imm carryIn
    rn, shifted, carryOut
  | ThreeOperands (opr1, opr2, RegShift (typ, rs)) ->
    let carryIn = getCarryFlag ctxt
    let rn, rm = transOprToExpr ctxt opr1, transOprToExpr ctxt opr2
    let amount = extractLow 8<rt> (getRegVar ctxt rs) |> zExt 32<rt>
    let shifted, carryOut = shiftCForRegAmount rm 32<rt> typ amount carryIn
    rn, shifted, carryOut
  | _ -> raise InvalidOperandException

let tst insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let src1, src2, carryOut = transOprsOfTST insInfo ctxt
  let result = tmpVar 32<rt>
  let apsr = getRegVar ctxt R.APSR
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := src1 .& src2)
  builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
  builder <! (apsr := result == num0 32<rt> |> setPSR ctxt R.APSR PSR_Z)
  builder <! (apsr := carryOut |> setPSR ctxt R.APSR PSR_C)
  endMark insInfo lblCondFail isCondPass builder

let smull isSetFlags insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rdLo, rdHi, rn, rm = transFourOprs insInfo ctxt
  let result = tmpVar 64<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (result := sExt 64<rt> rn .* sExt 64<rt> rm)
  builder <! (rdHi := extractHigh 32<rt> result)
  builder <! (rdLo := extractLow 32<rt> result)
  if isSetFlags then
    let apsr = getRegVar ctxt R.APSR
    builder <! (apsr := extractHigh 1<rt> result |> setPSR ctxt R.APSR PSR_N)
    builder <! (apsr := result == num0 64<rt> |> setPSR ctxt R.APSR PSR_Z)
  else ()
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfB insInfo =
  let pc = bvOfBaseAddr insInfo.Address
  match insInfo.Operands with
  | OneOperand (Memory (LiteralMode imm)) ->
    pc .+ (num <| BitVector.ofInt64 imm 32<rt>)
  | _ -> raise InvalidOperandException

let b insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let e = parseOprOfB insInfo
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (branchWritePC ctxt e)
  endMark insInfo lblCondFail isCondPass builder

let bx insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rm = transOneOpr insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  bxWritePC ctxt rm builder
  endMark insInfo lblCondFail isCondPass builder

let movtAssign dst src =
  let maskHigh16In32 = num <| BitVector.ofUBInt 4294901760I 32<rt>
  let clearHigh16In32 expr = expr .& not maskHigh16In32
  dst := clearHigh16In32 dst .|
         (src << (num <| BitVector.ofInt32 16 32<rt>))

let movt insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let dst, res = transTwoOprs insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (movtAssign dst res)
  endMark insInfo lblCondFail isCondPass builder

let popLoop ctxt numOfReg addr (builder: StmtBuilder) =
  let loop addr count =
    if (numOfReg >>> count) &&& 1u = 1u then
      let reg = count |> byte |> OperandHelper.getRegister
      builder <! (getRegVar ctxt reg := loadLE 32<rt> addr)
      (addr .+ (num <| BitVector.ofInt32 4 32<rt>))
    else addr
  List.fold loop addr [ 0 .. 14 ]

let pop insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let t0 = tmpVar 32<rt>
  let sp = getRegVar ctxt R.SP
  let numOfReg = parseOprOfPUSHPOP insInfo
  let stackWidth = 4 * bitCount numOfReg 16
  let addr = sp
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := addr)
  let addr = popLoop ctxt numOfReg t0 builder
  if (numOfReg >>> 15 &&& 1u) = 1u then
    builder <! (loadLE 32<rt> addr |> loadWritePC ctxt)
  else ()
  if (numOfReg >>> 13 &&& 1u) = 0u then
    builder <! (sp := sp .+ (num <| BitVector.ofInt32 stackWidth 32<rt>))
  else builder <! (sp := (Expr.Undefined (32<rt>, "UNKNOWN")))
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfLDM insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register reg, RegList regs) ->
    let struct (rn, bool) = ParseUtils.parseRegW reg
    getRegVar ctxt rn, getRegNum rn, bool, regsToUInt32 regs
  | _ -> raise InvalidOperandException

let getLDMStartAddr rn stackWidth = function
  | Op.LDM | Op.LDMIA -> rn
  | Op.LDMDA -> rn .- (num <| BitVector.ofInt32 (stackWidth + 4) 32<rt>)
  | Op.LDMDB -> rn .- (num <| BitVector.ofInt32 stackWidth 32<rt>)
  | Op.LDMIB -> rn .+ (num <| BitVector.ofInt32 4 32<rt>)
  | _ -> raise InvalidOpcodeException

let ldm opcode insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let t0 = tmpVar 32<rt>
  let rn, numOfRn, wback, numOfReg = parseOprOfLDM insInfo ctxt
  let stackWidth = 4 * bitCount numOfReg 16
  let addr = getLDMStartAddr rn stackWidth opcode
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := addr)
  let addr = popLoop ctxt numOfReg t0 builder
  if (numOfReg >>> 15 &&& 1u) = 1u then
    builder <! (loadLE 32<rt> addr |> loadWritePC ctxt)
  else ()
  if wback && (numOfReg &&& numOfRn) = 0u then
    builder <! (rn := rn .+ (num <| BitVector.ofInt32 stackWidth 32<rt>))
  else ()
  if wback && (numOfReg &&& numOfRn) = numOfRn then
    builder <! (rn := (Expr.Undefined (32<rt>, "UNKNOWN")))
  else ()
  endMark insInfo lblCondFail isCondPass builder

let getOffAddrWithExpr s r e = if s = Some Plus then r .+ e else r .- e

let getOffAddrWithImm s r imm =
  match s, imm with
  | Some Plus, Some i -> r .+ (num <| BitVector.ofInt64 i 32<rt>)
  | Some Minus, Some i -> r .- (num <| BitVector.ofInt64 i 32<rt>)
  | _, _ -> r

let parseMemOfLDR insInfo ctxt = function
  | Memory (OffsetMode (ImmOffset (rn , s, imm))) ->
    getOffAddrWithImm s (getRegVar ctxt rn) imm, None
  | Memory (PreIdxMode (ImmOffset (rn , s, imm))) ->
    let rn = getRegVar ctxt rn
    let offsetAddr = getOffAddrWithImm s rn imm
    offsetAddr, Some (rn := offsetAddr)
  | Memory (PostIdxMode (ImmOffset (rn , s, imm))) ->
    let rn = getRegVar ctxt rn
    rn, Some (rn := getOffAddrWithImm s rn imm)
  | Memory (LiteralMode imm) ->
    let addr = bvOfBaseAddr insInfo.Address
    let pc = align addr (num <| BitVector.ofInt32 4 32<rt>)
    pc .+ (num <| BitVector.ofInt64 imm 32<rt>), None
  | Memory (OffsetMode (RegOffset (n, _, m, None))) ->
    let offset = shift (getRegVar ctxt m) 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    getRegVar ctxt n .+ offset, None
  | Memory (PreIdxMode (RegOffset (n, s, m, None))) ->
    let rn = getRegVar ctxt n
    let offset = shift (getRegVar ctxt m) 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    let offsetAddr = getOffAddrWithExpr s rn offset
    offsetAddr, Some (rn := offsetAddr)
  | Memory (PostIdxMode (RegOffset (n, s, m, None))) ->
    let rn = getRegVar ctxt n
    let offset = shift (getRegVar ctxt m) 32<rt> SRTypeLSL 0u (getCarryFlag ctxt)
    rn, Some (rn := getOffAddrWithExpr s rn offset)
  | Memory (OffsetMode (RegOffset (n, s, m, Some (t, Imm i)))) ->
    let rn = getRegVar ctxt n
    let offset = shift (getRegVar ctxt m) 32<rt> t i (getCarryFlag ctxt)
    getOffAddrWithExpr s rn offset, None
  | Memory (PreIdxMode (RegOffset (n, s, m, Some (t, Imm i)))) ->
    let rn = getRegVar ctxt n
    let offset = shift (getRegVar ctxt m) 32<rt> t i (getCarryFlag ctxt)
    let offsetAddr = getOffAddrWithExpr s rn offset
    offsetAddr, Some (rn := offsetAddr)
  | Memory (PostIdxMode (RegOffset (n, s, m, Some (t, Imm i)))) ->
    let rn = getRegVar ctxt n
    let offset = shift (getRegVar ctxt m) 32<rt> t i (getCarryFlag ctxt)
    rn, Some (rn := getOffAddrWithExpr s rn offset)
  | _ -> raise InvalidOperandException

let parseOprOfLDR insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register rt, (Memory _ as mem)) ->
    let addr, stmt = parseMemOfLDR insInfo ctxt mem
    getRegVar ctxt rt, addr, stmt
  | _ -> raise InvalidOperandException

let ldr insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let lblL0 = lblSymbol "L0"
  let lblL1 = lblSymbol "L1"
  let lblEnd = lblSymbol "End"
  let data = tmpVar 32<rt>
  let rt, addr, stmt = parseOprOfLDR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (data := loadLE 32<rt> addr)
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  if rt = getPC ctxt then
    let cond = addr .& (num <| BitVector.ofInt32 3 32<rt>)
               == (num <| BitVector.ofInt32 0 32<rt>)
    builder <! (CJmp (cond, Name lblL0, Name lblL1))
    builder <! (LMark lblL0)
    builder <! (loadWritePC ctxt data)
    builder <! (Jmp (Name lblEnd))
    builder <! (LMark lblL1)
    builder <! (SideEffect UndefinedInstr)
    builder <! (LMark lblEnd)
  else builder <! (rt := data)
  endMark insInfo lblCondFail isCondPass builder

let ldrb insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, addr, stmt = parseOprOfLDR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (rt := loadLE 8<rt> addr |> zExt 32<rt>)
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  endMark insInfo lblCondFail isCondPass builder

let parseMemOfLDRD insInfo ctxt = function
  | Memory (OffsetMode (RegOffset (n, s, m, None))) ->
    getOffAddrWithExpr s (getRegVar ctxt n) (getRegVar ctxt m), None
  | Memory (PreIdxMode (RegOffset (n, s, m, None))) ->
    let rn = getRegVar ctxt n
    let offsetAddr = getOffAddrWithExpr s rn (getRegVar ctxt m)
    offsetAddr, Some (rn := offsetAddr)
  | Memory (PostIdxMode (RegOffset (n, s, m, None))) ->
    let rn = getRegVar ctxt n
    rn, Some (rn := getOffAddrWithExpr s rn (getRegVar ctxt m))
  | mem -> parseMemOfLDR insInfo ctxt mem

let parseOprOfLDRD insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register t, Register t2, (Memory _ as mem)) ->
    let addr, stmt = parseMemOfLDRD insInfo ctxt mem
    getRegVar ctxt t, getRegVar ctxt t2, addr, stmt
  | _ -> raise InvalidOperandException

let ldrd insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, rt2, addr, stmt = parseOprOfLDRD insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (rt := loadLE 32<rt> addr)
  builder <! (rt2 := loadLE 32<rt> (addr .+ (num (BitVector.ofInt32 4 32<rt>))))
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  endMark insInfo lblCondFail isCondPass builder

let ldrh insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, addr, stmt = parseOprOfLDR insInfo ctxt
  let data = tmpVar 16<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (data := loadLE 16<rt> addr)
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  builder <! (rt := data |> zExt 32<rt>)
  endMark insInfo lblCondFail isCondPass builder

let str insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, addr, stmt = parseOprOfLDR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (loadLE 32<rt> addr := rt)
  if rt = getPC ctxt then builder <! (loadLE 32<rt> addr := rt)
  else builder <! (loadLE 32<rt> addr := pcStoreValue ctxt)
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  endMark insInfo lblCondFail isCondPass builder

let strb insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, addr, stmt = parseOprOfLDR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (loadLE 8<rt> addr := extractLow 8<rt> rt)
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  endMark insInfo lblCondFail isCondPass builder

let strd insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, rt2, addr, stmt = parseOprOfLDRD insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (loadLE 32<rt> addr := rt)
  builder <! (loadLE 32<rt> (addr .+ (num <| BitVector.ofInt32 4 32<rt>)) := rt2)
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  endMark insInfo lblCondFail isCondPass builder

let strh insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, addr, stmt = parseOprOfLDR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  match stmt with
  | Some s -> builder <! s
  | None -> ()
  builder <! (loadLE 16<rt> addr := extractLow 16<rt> rt)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfSTM insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register reg, RegList regs) ->
    let struct (rn, bool) = ParseUtils.parseRegW reg
    getRegVar ctxt rn, bool, regsToUInt32 regs
  | _ -> raise InvalidOperandException

let getSTMStartAddr rn stackWidth = function
  | Op.STM | Op.STMIA | Op.STMEA -> rn
  | Op.STMDA -> rn .- (num <| BitVector.ofInt32 (stackWidth + 4) 32<rt>)
  | Op.STMIB -> rn .+ (num <| BitVector.ofInt32 4 32<rt>)
  | _ -> raise InvalidOpcodeException

let stmLoop ctxt numOfReg wback rn addr (builder: StmtBuilder) =
  let loop addr count =
    if (numOfReg >>> count) &&& 1u = 1u then
      let ri = count |> byte |> OperandHelper.getRegister |> getRegVar ctxt
      if ri = rn && wback && count <> lowestSetBit numOfReg 32 then
        builder <! (loadLE 32<rt> addr := (Expr.Undefined (32<rt>, "UNKNOWN")))
      else
        builder <! (loadLE 32<rt> addr := ri )
      addr .+ (num <| BitVector.ofInt32 4 32<rt>)
    else addr
  List.fold loop addr [ 0 .. 14 ]

let stm opcode insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let t0 = tmpVar 32<rt>
  let rn, wback, numOfReg = parseOprOfSTM insInfo ctxt
  let stackWidth = 4 * bitCount numOfReg 16
  let addr = getSTMStartAddr rn stackWidth opcode
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := addr)
  let addr = stmLoop ctxt numOfReg wback rn t0 builder
  if (numOfReg >>> 15 &&& 1u) = 1u then
    builder <! (loadLE 32<rt> addr := pcStoreValue ctxt)
  else ()
  if wback then
    builder <! (rn := rn .+ (num <| BitVector.ofInt32 stackWidth 32<rt>))
  else ()
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfCBZ insInfo ctxt =
  let pc = bvOfBaseAddr insInfo.Address
  match insInfo.Operands with
  | TwoOperands (Register rn, (Memory (LiteralMode imm))) ->
    getRegVar ctxt rn, pc .+ (num <| BitVector.ofInt64 imm 32<rt>)
  | _ -> raise InvalidOperandException

let cbz nonZero insInfo ctxt =
  let builder = new StmtBuilder (16)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let lblL0 = lblSymbol "L0"
  let lblL1 = lblSymbol "L1"
  let n = if nonZero then num1 1<rt> else num0 1<rt>
  let rn, pc = parseOprOfCBZ insInfo ctxt
  let cond = n <+> (rn == num0 32<rt>)
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (CJmp (cond, Name lblL0, Name lblL1))
  builder <! (LMark lblL0)
  builder <! (branchWritePC ctxt pc)
  builder <! (LMark lblL1)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfTableBranch insInfo ctxt =
  match insInfo.Operands with
  | OneOperand (Memory (OffsetMode (RegOffset (rn, None, rm, None)))) ->
    let addr = getRegVar ctxt rn .+ getRegVar ctxt rm
    loadLE 8<rt> addr |> zExt 32<rt>
  | OneOperand (Memory (OffsetMode (RegOffset (rn, None, rm, Some (_, Imm i)))))
    -> let addr = getRegVar ctxt rn .+ (shiftLSL (getRegVar ctxt rm) 32<rt> i)
       loadLE 16<rt> addr |> zExt 32<rt>
  | _ -> raise InvalidOperandException

let tableBranch insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let pc = bvOfBaseAddr insInfo.Address
  let halfwords = parseOprOfTableBranch insInfo ctxt
  let result = pc .+ ((num <| BitVector.ofInt32 2 32<rt>) .* halfwords)
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (branchWritePC ctxt result)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfBFC insInfo ctxt =
  match insInfo.Operands with
  | ThreeOperands (Register rd, Immediate lsb, Immediate width) ->
    getRegVar ctxt rd, Convert.ToInt32 lsb, Convert.ToInt32 width
  | _ -> raise InvalidOperandException

let bfc insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, lsb, width = parseOprOfBFC insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (rd := replicate rd 32<rt> lsb width 0)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfRdRnLsbWidth insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (Register rd, Register rn, Immediate lsb, Immediate width) ->
    getRegVar ctxt rd, getRegVar ctxt rn,
    Convert.ToInt32 lsb, Convert.ToInt32 width
  | _ -> raise InvalidOperandException

let bfi insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rn, lsb, width = parseOprOfRdRnLsbWidth insInfo ctxt
  let t0 = tmpVar 32<rt>
  let t1 = tmpVar 32<rt>
  let n = rn .&
          (BitVector.ofUBInt (BigInteger.getMask width) 32<rt> |> num)
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := n << (num <| BitVector.ofInt32 lsb 32<rt>))
  builder <! (t1 := replicate rd 32<rt> lsb width 0)
  builder <! (rd := t0 .| t1)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfUXTB insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register rd, Register rm) ->
    getRegVar ctxt rd, getRegVar ctxt rm, 0u
  | ThreeOperands (Register rd, Register rm, Shift (_, Imm i)) ->
    getRegVar ctxt rd, getRegVar ctxt rm, i
  | _ -> raise InvalidOperandException

let uxtb insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rm, rotation = parseOprOfUXTB insInfo ctxt
  let rotated = shiftROR rm 32<rt> rotation
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (rd := zExt 32<rt> (extractLow 8<rt> rotated))
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfUXTAB insInfo ctxt =
  match insInfo.Operands with
  | FourOperands (Register rd, Register rn, Register rm, Shift (_, Imm i)) ->
    getRegVar ctxt rd, getRegVar ctxt rn, getRegVar ctxt rm, i
  | _ -> raise InvalidOperandException

let uxtab insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rn, rm, rotation = parseOprOfUXTAB insInfo ctxt
  let rotated = shiftROR rm 32<rt> rotation
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (rd := rn .+ zExt 32<rt> (extractLow 8<rt> rotated))
  endMark insInfo lblCondFail isCondPass builder

let ubfx insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rn, lsb, width = parseOprOfRdRnLsbWidth insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if lsb + width - 1 <= 31 then
    let v = BitVector.ofUBInt (BigInteger.getMask width) 32<rt> |> num
    builder <! (rd := (rn >> (num <| BitVector.ofInt32 lsb 32<rt>)) .& v)
  else builder <! (SideEffect UndefinedInstr)  //FIXME  (use UNPREDICTABLE)
  endMark insInfo lblCondFail isCondPass builder

/// For ThumbMode (T1 case)
let parseOprOfADR insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (Register rd, Memory (LiteralMode imm)) ->
    let addr = bvOfBaseAddr insInfo.Address
    let pc = align addr (num <| BitVector.ofInt32 4 32<rt>)
    getRegVar ctxt rd, pc .+ (num <| BitVector.ofInt64 imm 32<rt>)
  | _ -> raise InvalidOperandException

let adr insInfo ctxt =
  let builder = new StmtBuilder (32)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, result = parseOprOfADR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if rd = getPC ctxt then writePC ctxt result builder
  else builder <! (rd := result)
  endMark insInfo lblCondFail isCondPass builder

let mls insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rn, rm, ra = transFourOprs insInfo ctxt
  let r = tmpVar 32<rt>
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (r := extractLow 32<rt> (zExt 64<rt> rn .- zExt 64<rt> rm .*
                                     zExt 64<rt> ra))
  builder <! (rd := r)
  endMark insInfo lblCondFail isCondPass builder

let sxth insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, rm, rotation = parseOprOfUXTB insInfo ctxt
  let rotated = shiftROR rm 32<rt> rotation
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (rd := sExt 32<rt> (extractLow 8<rt> rotated))
  endMark insInfo lblCondFail isCondPass builder

let checkSingleReg = function
  | R.S0 | R.S1 | R.S2 | R.S3 | R.S4 | R.S5 | R.S6 | R.S7 | R.S8 | R.S9
  | R.S10 | R.S11 | R.S12 | R.S13 | R.S14 | R.S15 | R.S16 | R.S17 | R.S18
  | R.S19 | R.S20 | R.S21 | R.S22 | R.S23 | R.S24 | R.S25 | R.S26 | R.S27
  | R.S28 | R.S29 | R.S30 | R.S31 -> true
  | _ -> false

let parseOprOfVLDR insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (SIMDOpr (SFReg (Vector d)),
                 Memory (OffsetMode (ImmOffset (rn , s, imm)))) ->
    let baseAddr =
      if rn = R.PC then
        let addr = bvOfBaseAddr insInfo.Address
        align addr (num <| BitVector.ofInt32 4 32<rt>)
      else getRegVar ctxt rn
    getRegVar ctxt d, getOffAddrWithImm s baseAddr imm, checkSingleReg d
  | _ -> raise InvalidOperandException

let vldr insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, addr, isSReg = parseOprOfVLDR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if isSReg then
    let data = tmpVar 32<rt>
    builder <! (data := loadLE 32<rt> addr)
    builder <! (rd := data)
  else
    let d1 = tmpVar 32<rt>
    let d2 = tmpVar 32<rt>
    builder <! (d1 := loadLE 32<rt> addr)
    builder <! (d2 := loadLE 32<rt> (addr .+ (num (BitVector.ofInt32 4 32<rt>))))
    builder <! (rd := concat d1 d2)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfVSTR insInfo ctxt =
  match insInfo.Operands with
  | TwoOperands (SIMDOpr (SFReg (Vector d)),
                 Memory (OffsetMode (ImmOffset (rn , s, imm)))) ->
    let baseAddr = getRegVar ctxt rn
    getRegVar ctxt d, getOffAddrWithImm s baseAddr imm, checkSingleReg d
  | _ -> raise InvalidOperandException

let vstr insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rd, addr, isSReg = parseOprOfVSTR insInfo ctxt
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if isSReg then builder <! (loadLE 32<rt> addr := rd)
  else
    let mem1 = loadLE 32<rt> addr
    let mem2 = loadLE 32<rt>  (addr .+ (num <| BitVector.ofInt32 4 32<rt>))
    builder <! (mem1 := extractHigh 32<rt> rd)
    builder <! (mem2 := extractLow 32<rt> rd)
  endMark insInfo lblCondFail isCondPass builder

let parseOprOfVPUSHVPOP insInfo =
  match insInfo.Operands with
  | OneOperand (RegList r) -> r
  | _ -> raise InvalidOperandException

let getVFPSRegisterToInt = function
  | R.S0 -> 0x00
  | R.S1 -> 0x01
  | R.S2 -> 0x02
  | R.S3 -> 0x03
  | R.S4 -> 0x04
  | R.S5 -> 0x05
  | R.S6 -> 0x06
  | R.S7 -> 0x07
  | R.S8 -> 0x08
  | R.S9 -> 0x09
  | R.S10 -> 0x0A
  | R.S11 -> 0x0B
  | R.S12 -> 0x0C
  | R.S13 -> 0x0D
  | R.S14 -> 0x0E
  | R.S15 -> 0x0F
  | R.S16 -> 0x10
  | R.S17 -> 0x11
  | R.S18 -> 0x12
  | R.S19 -> 0x13
  | R.S20 -> 0x14
  | R.S21 -> 0x15
  | R.S22 -> 0x16
  | R.S23 -> 0x17
  | R.S24 -> 0x18
  | R.S25 -> 0x19
  | R.S26 -> 0x1A
  | R.S27 -> 0x1B
  | R.S28 -> 0x1C
  | R.S29 -> 0x1D
  | R.S30 -> 0x1E
  | R.S31 -> 0x1F
  | _ -> raise InvalidRegisterException

let getVFPDRegisterToInt = function
  | R.D0 -> 0x00
  | R.D1 -> 0x01
  | R.D2 -> 0x02
  | R.D3 -> 0x03
  | R.D4 -> 0x04
  | R.D5 -> 0x05
  | R.D6 -> 0x06
  | R.D7 -> 0x07
  | R.D8 -> 0x08
  | R.D9 -> 0x09
  | R.D10 -> 0x0A
  | R.D11 -> 0x0B
  | R.D12 -> 0x0C
  | R.D13 -> 0x0D
  | R.D14 -> 0x0E
  | R.D15 -> 0x0F
  | R.D16 -> 0x10
  | R.D17 -> 0x11
  | R.D18 -> 0x12
  | R.D19 -> 0x13
  | R.D20 -> 0x14
  | R.D21 -> 0x15
  | R.D22 -> 0x16
  | R.D23 -> 0x17
  | R.D24 -> 0x18
  | R.D25 -> 0x19
  | R.D26 -> 0x1A
  | R.D27 -> 0x1B
  | R.D28 -> 0x1C
  | R.D29 -> 0x1D
  | R.D30 -> 0x1E
  | R.D31 -> 0x1F
  | R.FPINST2 -> 0x20
  | R.MVFR0 -> 0x21
  | R.MVFR1 -> 0x22
  | _ -> raise InvalidRegisterException

let parsePUSHPOPsubValue insInfo =
  let regs = parseOprOfVPUSHVPOP insInfo
  let isSReg = checkSingleReg regs.Head
  let imm = if isSReg then regs.Length else regs.Length * 2
  let d = if isSReg then getVFPSRegisterToInt regs.Head
          else getVFPDRegisterToInt regs.Head
  d, imm, isSReg

let vpopLoop ctxt d imm isSReg addr (builder: StmtBuilder) =
  let rec singleRegLoop r addr =
    if r < imm then
      let reg = d + r |> byte |> OperandHelper.getVFPSRegister
      let nextAddr = (addr .+ (num <| BitVector.ofInt32 4 32<rt>))
      builder <! (getRegVar ctxt reg := loadLE 32<rt> addr)
      singleRegLoop (r + 1) nextAddr
    else ()
  let rec nonSingleRegLoop r addr =
    if r < imm / 2 then
      let reg = d + r |> byte |> OperandHelper.getVFPDRegister
      let word1 = loadLE 32<rt> addr
      let word2 = loadLE 32<rt> (addr .+ (num <| BitVector.ofInt32 4 32<rt>))
      let nextAddr = addr .+ (num <| BitVector.ofInt32 8 32<rt>)
      builder <! (getRegVar ctxt reg := concat word1 word2)
      nonSingleRegLoop (r + 1) nextAddr
    else ()
  let loopFn = if isSReg then singleRegLoop else nonSingleRegLoop
  loopFn 0 addr

let vpop insInfo ctxt =
  let builder = new StmtBuilder (64) // FIXME
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let t0 = tmpVar 32<rt>
  let sp = getRegVar ctxt R.SP
  let d, imm, isSReg = parsePUSHPOPsubValue insInfo
  let addr = sp
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := addr)
  builder <! (sp := addr .+ (num <| BitVector.ofInt32 (imm <<< 2) 32<rt>))
  vpopLoop ctxt d imm isSReg t0 builder
  endMark insInfo lblCondFail isCondPass builder

let vpushLoop ctxt d imm isSReg addr (builder: StmtBuilder) =
  let rec singleRegLoop r addr =
    if r < imm then
      let reg = d + r |> byte |> OperandHelper.getVFPSRegister
      let nextAddr = (addr .+ (num <| BitVector.ofInt32 4 32<rt>))
      builder <! (loadLE 32<rt> addr := getRegVar ctxt reg)
      singleRegLoop (r + 1) nextAddr
    else ()
  let rec nonSingleRegLoop r addr =
    if r < imm / 2 then
      let reg = d + r |> byte |> OperandHelper.getVFPDRegister
      let mem1 = loadLE 32<rt> addr
      let mem2 = loadLE 32<rt> (addr .+ (num <| BitVector.ofInt32 4 32<rt>))
      let nextAddr = addr .+ (num <| BitVector.ofInt32 8 32<rt>)
      builder <! (mem1 := extractHigh 32<rt> (getRegVar ctxt reg))
      builder <! (mem2 := extractLow 32<rt> (getRegVar ctxt reg))
      nonSingleRegLoop (r + 1) nextAddr
    else ()
  let loopFn = if isSReg then singleRegLoop else nonSingleRegLoop
  loopFn 0 addr

let vpush insInfo ctxt =
  let builder = new StmtBuilder (64) // FIXME
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let t0 = tmpVar 32<rt>
  let sp = getRegVar ctxt R.SP
  let d, imm, isSReg = parsePUSHPOPsubValue insInfo
  let addr = sp .- (num <| BitVector.ofInt32 (imm <<< 2) 32<rt>)
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  builder <! (t0 := addr)
  builder <! (sp := addr .- (num <| BitVector.ofInt32 (imm <<< 2) 32<rt>))
  vpushLoop ctxt d imm isSReg t0 builder
  endMark insInfo lblCondFail isCondPass builder

let vmrs insInfo ctxt =
  let builder = new StmtBuilder (8)
  let lblCondPass = lblSymbol "CondCheckPassed"
  let lblCondFail = lblSymbol "CondCheckFailed"
  let rt, fpscr = transTwoOprs insInfo ctxt
  let apsr = getRegVar ctxt R.APSR
  let isCondPass = isCondPassed insInfo.Condition
  startMark insInfo ctxt lblCondPass lblCondFail isCondPass builder
  if rt <> apsr then builder <! (rt := fpscr)
  else builder <! (apsr := disablePSR ctxt R.APSR PSR_Cond .|
                           getPSR ctxt R.FPSCR PSR_Cond)
  endMark insInfo lblCondFail isCondPass builder

/// Translate IR.
let translate insInfo ctxt =
  let addr = insInfo.Address
  match insInfo.Opcode with
  | Op.ADC -> adc false insInfo ctxt
  | Op.ADCS -> adcs true insInfo ctxt
  | Op.ADD | Op.ADDW -> add false insInfo ctxt
  | Op.ADDS -> adds true insInfo ctxt
  | Op.BL -> bl insInfo ctxt
  | Op.BLX -> branchWithLink insInfo ctxt
  | Op.PUSH -> push insInfo ctxt
  | Op.SUB | Op.SUBW -> sub false insInfo ctxt
  | Op.SUBS -> subs true insInfo ctxt
  | Op.AND -> transAND false insInfo ctxt
  | Op.ANDS -> ands true insInfo ctxt
  | Op.MOV | Op.MOVW -> mov true insInfo ctxt
  | Op.MOVS -> movs true insInfo ctxt
  | Op.EOR -> eor false insInfo ctxt
  | Op.EORS -> eors true insInfo ctxt
  | Op.RSB -> rsb false insInfo ctxt
  | Op.RSBS -> rsbs true insInfo ctxt
  | Op.SBC -> sbc false insInfo ctxt
  | Op.SBCS -> sbcs true insInfo ctxt
  | Op.RSC -> rsc false insInfo ctxt
  | Op.RSCS -> rscs true insInfo ctxt
  | Op.ORR -> orr false insInfo ctxt
  | Op.ORRS -> orrs true insInfo ctxt
  | Op.BIC -> bic false insInfo ctxt
  | Op.BICS -> bics true insInfo ctxt
  | Op.MVN -> mvn false insInfo ctxt
  | Op.MVNS -> mvns true insInfo ctxt
  | Op.ASR -> shiftInstr false insInfo SRTypeASR ctxt
  | Op.ASRS -> asrs true insInfo ctxt
  | Op.LSL -> shiftInstr false insInfo SRTypeLSL ctxt
  | Op.LSLS -> lsls true insInfo ctxt
  | Op.LSR -> shiftInstr false insInfo SRTypeLSR ctxt
  | Op.LSRS -> lsrs true insInfo ctxt
  | Op.ROR -> shiftInstr false insInfo SRTypeROR ctxt
  | Op.RORS -> rors true insInfo ctxt
  | Op.RRX -> shiftInstr false insInfo SRTypeRRX ctxt
  | Op.RRXS -> rrxs true insInfo ctxt
  | Op.CLZ -> clz insInfo ctxt
  | Op.CMN -> cmn insInfo ctxt
  | Op.MLA -> mla false insInfo ctxt
  | Op.MLAS -> mla true insInfo ctxt
  | Op.CMP -> cmp insInfo ctxt
  | Op.UMLAL -> umlal false insInfo ctxt
  | Op.UMLALS -> umlal true insInfo ctxt
  | Op.UMULL -> umull false insInfo ctxt
  | Op.UMULLS -> umull true insInfo ctxt
  | Op.TEQ -> teq insInfo ctxt
  | Op.MUL -> mul false insInfo ctxt
  | Op.MULS -> mul true insInfo ctxt
  | Op.TST -> tst insInfo ctxt
  | Op.SMULL -> smull false insInfo ctxt
  | Op.SMULLS -> smull true insInfo ctxt
  | Op.B -> b insInfo ctxt
  | Op.BX -> bx insInfo ctxt
  | Op.IT | Op.ITT | Op.ITE | Op.ITTT | Op.ITET | Op.ITTE
  | Op.ITEE | Op.ITTTT | Op.ITETT | Op.ITTET | Op.ITEET
  | Op.ITTTE | Op.ITETE | Op.ITTEE | Op.ITEEE
  | Op.NOP -> nop insInfo addr
  | Op.MOVT -> movt insInfo ctxt
  | Op.POP -> pop insInfo ctxt
  | Op.LDM -> ldm Op.LDM insInfo ctxt
  | Op.LDMIB -> ldm Op.LDMIB insInfo ctxt
  | Op.LDMDA -> ldm Op.LDMDA insInfo ctxt
  | Op.LDMDB -> ldm Op.LDMDB insInfo ctxt
  | Op.LDR -> ldr insInfo ctxt
  | Op.LDRB -> ldrb insInfo ctxt
  | Op.LDRD -> ldrd insInfo ctxt
  | Op.LDRH -> ldrh insInfo ctxt
  | Op.STR -> str insInfo ctxt
  | Op.STRB -> strb insInfo ctxt
  | Op.STRD -> strd insInfo ctxt
  | Op.STRH -> strh insInfo ctxt
  | Op.STM -> stm Op.STM insInfo ctxt
  | Op.STMIB -> stm Op.STMIB insInfo ctxt
  | Op.STMDA -> stm Op.STMDA insInfo ctxt
  | Op.STCL | Op.SVC | Op.MRC2 | Op.LDCL -> coprocOp insInfo addr // coprocessor instruction
  | Op.CBNZ -> cbz true insInfo ctxt
  | Op.CBZ -> cbz false insInfo ctxt
  | Op.TBH | Op.TBB -> tableBranch insInfo ctxt
  | Op.BFC -> bfc insInfo ctxt
  | Op.BFI -> bfi insInfo ctxt
  | Op.UXTB -> uxtb insInfo ctxt
  | Op.UXTAB -> uxtab insInfo ctxt
  | Op.UBFX -> ubfx insInfo ctxt
  | Op.ADR -> adr insInfo ctxt // for Thumb mode
  | Op.MLS -> mls insInfo ctxt
  | Op.SXTH -> sxth insInfo ctxt
  | Op.VLDR -> vldr insInfo ctxt
  | Op.VSTR -> vstr insInfo ctxt
  | Op.VPOP -> vpop insInfo ctxt
  | Op.VPUSH -> vpush insInfo ctxt
  | Op.VMRS -> vmrs insInfo ctxt
  | Op.VCVT | Op.VCVTR | Op.VMLS | Op.VADD | Op.VMUL | Op.VDIV
  | Op.VMOV | Op.VCMP | Op.VCMPE -> sideEffects insInfo addr UnsupportedFP
  | o -> raise <| NotImplementedIRException (Disasm.opCodeToString o)
  |> fun builder -> builder.ToStmts ()

// vim: set tw=80 sts=2 sw=2:
